using System.IO.Pipelines;
using System.Text.Json;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.Support;
using Cysharp.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public class ChatInteractionHub : Hub<IChatInteractionHubClient>
{
    private readonly ILogger<ChatInteractionHub> _logger;

    protected readonly IStringLocalizer S;

    public ChatInteractionHub(
        ILogger<ChatInteractionHub> logger,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
    {
        _logger = logger;
        S = stringLocalizer;
    }

    public ChannelReader<CompletionPartialMessage> SendMessage(string itemId, string prompt, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        _ = HandlePromptAsync(channel.Writer, itemId, prompt, cancellationToken);

        return channel.Reader;
    }

    public async Task LoadInteraction(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(itemId)].Value);

            return;
        }

        // SignalR connections share a single DI scope for the entire WebSocket lifetime,
        // but OrchardCore scoped services (ISession, IDocumentStore) expect per-request
        // lifetimes. A child scope gives each hub invocation its own services with
        // proper commit/rollback lifecycle on disposal.
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var services = scope.ServiceProvider;
            var interactionManager = services.GetRequiredService<ISourceCatalogManager<ChatInteraction>>();
            var authorizationService = services.GetRequiredService<IAuthorizationService>();
            var promptStore = services.GetRequiredService<IChatInteractionPromptStore>();

            var httpContext = Context.GetHttpContext();

            var interaction = await interactionManager.FindByIdAsync(itemId);

            if (interaction is null)
            {
                await Clients.Caller.ReceiveError(S["Interaction not found."].Value);

                return;
            }

            if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
            {
                await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);

                return;
            }

            var prompts = await promptStore.GetPromptsAsync(itemId);

            await Clients.Caller.LoadInteraction(new
            {
                interaction.ItemId,
                interaction.Title,
                interaction.ConnectionName,
                DeploymentId = interaction.ChatDeploymentId,
                Messages = prompts.Select(message => new AIChatResponseMessageDetailed
                {
                    Id = message.ItemId,
                    Role = message.Role.Value,
                    IsGeneratedPrompt = message.IsGeneratedPrompt,
                    Title = message.Title,
                    Content = message.Text,
                    References = message.References,
                })
            });
        });
    }

    public async Task SaveSettings(string itemId, JsonElement settings)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(itemId)].Value);

            return;
        }

        // Each hub invocation gets its own child scope for proper ISession/IDocumentStore lifecycle.
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var services = scope.ServiceProvider;
            var interactionManager = services.GetRequiredService<ISourceCatalogManager<ChatInteraction>>();
            var authorizationService = services.GetRequiredService<IAuthorizationService>();
            var settingsHandlers = services.GetRequiredService<IEnumerable<IChatInteractionSettingsHandler>>();

            var interaction = await interactionManager.FindByIdAsync(itemId);

            if (interaction == null)
            {
                await Clients.Caller.ReceiveError(S["Interaction not found."].Value);

                return;
            }

            var httpContext = Context.GetHttpContext();

            if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
            {
                await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);

                return;
            }

            // Let module-specific handlers bind their own properties first.
            foreach (var handler in settingsHandlers)
            {
                await handler.UpdatingAsync(interaction, settings);
            }

            // Apply core properties from the settings payload.
            interaction.Title = GetString(settings, "title") ?? "Untitled";
            interaction.OrchestratorName = GetString(settings, "orchestratorName");
            interaction.ConnectionName = GetString(settings, "connectionName");
            interaction.ChatDeploymentId = GetString(settings, "deploymentId");
            interaction.SystemMessage = GetString(settings, "systemMessage");
            interaction.Temperature = GetFloat(settings, "temperature");
            interaction.TopP = GetFloat(settings, "topP");
            interaction.FrequencyPenalty = GetFloat(settings, "frequencyPenalty");
            interaction.PresencePenalty = GetFloat(settings, "presencePenalty");
            interaction.MaxTokens = GetInt(settings, "maxTokens");
            interaction.PastMessagesCount = GetInt(settings, "pastMessagesCount");
            interaction.ToolNames = GetStringArray(settings, "toolNames");
            interaction.McpConnectionIds = GetStringArray(settings, "mcpConnectionIds");
            interaction.AgentNames = GetStringArray(settings, "agentNames");

            var dataSourceId = GetString(settings, "dataSourceId");

            if (!string.IsNullOrWhiteSpace(dataSourceId))
            {
                var dataSourceStore = services.GetService<ICatalog<AIDataSource>>();
                if (dataSourceStore is not null)
                {
                    var dataSource = await dataSourceStore.FindByIdAsync(dataSourceId);

                    if (dataSource is not null)
                    {
                        interaction.Put(new DataSourceMetadata()
                        {
                            DataSourceId = dataSource.ItemId,
                        });

                        interaction.Put(new AIDataSourceRagMetadata()
                        {
                            Strictness = GetInt(settings, "strictness"),
                            TopNDocuments = GetInt(settings, "topNDocuments"),
                            IsInScope = GetBool(settings, "isInScope") ?? true,
                            Filter = GetString(settings, "filter"),
                        });
                    }
                }
            }
            else
            {
                interaction.Put(new DataSourceMetadata());
                interaction.Put(new AIDataSourceRagMetadata());
            }

            await interactionManager.UpdateAsync(interaction);

            // Let handlers react after the interaction has been persisted.
            foreach (var handler in settingsHandlers)
            {
                await handler.UpdatedAsync(interaction, settings);
            }

            await Clients.Caller.SettingsSaved(interaction.ItemId, interaction.Title);
        });
    }

    private static string GetString(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static float? GetFloat(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetSingle();
            }

            if (prop.ValueKind == JsonValueKind.String && float.TryParse(prop.GetString(), out var f))
            {
                return f;
            }
        }

        return null;
    }

    private static int? GetInt(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var i))
            {
                return i;
            }
        }

        return null;
    }

    private static bool? GetBool(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (prop.ValueKind == JsonValueKind.String &&
                bool.TryParse(prop.GetString(), out var b))
            {
                return b;
            }
        }

        return null;
    }

    private static List<string> GetStringArray(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();

            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();

                    if (!string.IsNullOrEmpty(value))
                    {
                        list.Add(value);
                    }
                }
            }

            return list;
        }

        return [];
    }

    /// <summary>
    /// Clears the chat history (prompts) while keeping documents, parameters, and tools intact.
    /// </summary>
    public async Task ClearHistory(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(itemId)].Value);

            return;
        }

        // Each hub invocation gets its own child scope for proper ISession/IDocumentStore lifecycle.
        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var services = scope.ServiceProvider;
            var interactionManager = services.GetRequiredService<ISourceCatalogManager<ChatInteraction>>();
            var authorizationService = services.GetRequiredService<IAuthorizationService>();
            var promptStore = services.GetRequiredService<IChatInteractionPromptStore>();

            var interaction = await interactionManager.FindByIdAsync(itemId);

            if (interaction == null)
            {
                await Clients.Caller.ReceiveError(S["Interaction not found."].Value);

                return;
            }

            var httpContext = Context.GetHttpContext();

            if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
            {
                await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);

                return;
            }

            // Clear prompts using the prompt store.
            await promptStore.DeleteAllPromptsAsync(itemId);

            await Clients.Caller.HistoryCleared(interaction.ItemId);
        });
    }

    private async Task HandlePromptAsync(ChannelWriter<CompletionPartialMessage> writer, string itemId, string prompt, CancellationToken cancellationToken)
    {
        try
        {
            // Each hub invocation gets its own child scope for proper ISession/IDocumentStore lifecycle.
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                using var invocationScope = AIInvocationScope.Begin();
                var services = scope.ServiceProvider;

                try
                {
                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        await Clients.Caller.ReceiveError(S["Interaction ID is required."].Value);

                        return;
                    }

                    var interactionManager = services.GetRequiredService<ISourceCatalogManager<ChatInteraction>>();
                    var authorizationService = services.GetRequiredService<IAuthorizationService>();

                    var interaction = await interactionManager.FindByIdAsync(itemId);

                    if (interaction == null)
                    {
                        await Clients.Caller.ReceiveError(S["Interaction not found."].Value);

                        return;
                    }

                    var httpContext = Context.GetHttpContext();

                    if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
                    {
                        await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);

                        return;
                    }

                    if (string.IsNullOrWhiteSpace(prompt))
                    {
                        await Clients.Caller.ReceiveError(S["{0} is required.", nameof(prompt)].Value);

                        return;
                    }

                    prompt = prompt.Trim();

                    var promptStore = services.GetRequiredService<IChatInteractionPromptStore>();
                    var orchestrationContextBuilder = services.GetRequiredService<IOrchestrationContextBuilder>();
                    var orchestratorResolver = services.GetRequiredService<IOrchestratorResolver>();
                    var citationCollector = services.GetRequiredService<CitationReferenceCollector>();
                    var clock = services.GetRequiredService<IClock>();

                    // Create and save user prompt
                    var userPrompt = new ChatInteractionPrompt
                    {
                        ItemId = IdGenerator.GenerateId(),
                        ChatInteractionId = itemId,
                        Role = ChatRole.User,
                        Text = prompt,
                        CreatedUtc = clock.UtcNow,
                    };

                    await promptStore.CreateAsync(userPrompt);

                    var needsTitleUpdate = string.IsNullOrEmpty(interaction.Title);
                    if (needsTitleUpdate)
                    {
                        interaction.Title = Str.Truncate(prompt, 255);
                    }

                    // Load all prompts for building transcript
                    var existingPrompts = await promptStore.GetPromptsAsync(itemId);

                    var assistantPrompt = new ChatInteractionPrompt
                    {
                        ItemId = IdGenerator.GenerateId(),
                        ChatInteractionId = itemId,
                        Role = ChatRole.Assistant,
                        CreatedUtc = clock.UtcNow,
                    };

                    var builder = ZString.CreateStringBuilder();

                    // Build the orchestration context using the handler pipeline.
                    var orchestratorContext = await orchestrationContextBuilder.BuildAsync(interaction, ctx =>
                    {
                        ctx.UserMessage = prompt;
                        ctx.ConversationHistory = existingPrompts
                            .Where(x => !x.IsGeneratedPrompt)
                            .Select(p => new ChatMessage(p.Role, p.Text))
                            .ToList();
                    });

                    AIInvocationScope.Current.DataSourceId = orchestratorContext.CompletionContext.DataSourceId;

                    // Resolve the orchestrator for this interaction and execute the completion.
                    var orchestrator = orchestratorResolver.Resolve(interaction.OrchestratorName);

                    var contentItemIds = new HashSet<string>();
                    var references = new Dictionary<string, AICompletionReference>();

                    // Collect preemptive RAG references before streaming so the first chunk
                    // already contains any references from data sources and documents.
                    citationCollector.CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);

                    await foreach (var chunk in orchestrator.ExecuteStreamingAsync(orchestratorContext, cancellationToken))
                    {
                        if (string.IsNullOrEmpty(chunk.Text))
                        {
                            continue;
                        }

                        builder.Append(chunk.Text);

                        // Incrementally collect any new tool references that appeared during streaming.
                        citationCollector.CollectToolReferences(references, contentItemIds);

                        var partialMessage = new CompletionPartialMessage
                        {
                            SessionId = interaction.ItemId,
                            MessageId = assistantPrompt.ItemId,
                            ResponseId = chunk.ResponseId,
                            Content = chunk.Text,
                            References = references,
                        };

                        await writer.WriteAsync(partialMessage, cancellationToken);
                    }

                    // Final pass to collect any tool references added by the last tool call.
                    citationCollector.CollectToolReferences(references, contentItemIds);

                    if (builder.Length > 0)
                    {
                        assistantPrompt.Text = builder.ToString();
                        assistantPrompt.References = references;

                        if (contentItemIds.Count > 0)
                        {
                            assistantPrompt.Put(new ChatInteractionPromptContentMetadata
                            {
                                ContentItemIds = contentItemIds.ToList(),
                            });
                        }

                        await promptStore.CreateAsync(assistantPrompt);
                    }

                    // Update the interaction title after streaming is done to avoid holding
                    // database locks for the duration of the AI response, which can cause
                    // deadlocks with concurrent SaveSettings calls.
                    if (needsTitleUpdate)
                    {
                        await interactionManager.UpdateAsync(interaction);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException || (ex is TaskCanceledException && cancellationToken.IsCancellationRequested))
                    {
                        _logger.LogDebug("Chat interaction processing was cancelled.");
                        return;
                    }

                    _logger.LogError(ex, "An error occurred while processing the chat interaction.");

                    try
                    {
                        var errorMessage = new CompletionPartialMessage
                        {
                            SessionId = itemId,
                            MessageId = IdGenerator.GenerateId(),
                            Content = AIHubErrorMessageHelper.GetFriendlyErrorMessage(ex, S).Value,
                        };

                        await writer.WriteAsync(errorMessage, CancellationToken.None);
                    }
                    catch (Exception writeEx)
                    {
                        _logger.LogWarning(writeEx, "Failed to write error message to the channel.");
                    }
                }
            });
        }
        finally
        {
            writer.Complete();
        }
    }

    public async Task SendAudioStream(string itemId, IAsyncEnumerable<string> audioChunks, string audioFormat = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(itemId)].Value);
            return;
        }

        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var services = scope.ServiceProvider;
                var interactionManager = services.GetRequiredService<ISourceCatalogManager<ChatInteraction>>();
                var authorizationService = services.GetRequiredService<IAuthorizationService>();
                var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
                var clientFactory = services.GetRequiredService<IAIClientFactory>();
                var siteService = services.GetRequiredService<ISiteService>();

                var interaction = await interactionManager.FindByIdAsync(itemId);

                if (interaction is null)
                {
                    await Clients.Caller.ReceiveError(S["Interaction not found."].Value);
                    return;
                }

                var httpContext = Context.GetHttpContext();

                if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
                {
                    await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);
                    return;
                }

                var site = await siteService.GetSiteSettingsAsync();
                var deploymentSettings = site.As<DefaultAIDeploymentSettings>();

                if (string.IsNullOrEmpty(deploymentSettings.DefaultSpeechToTextDeploymentId))
                {
                    await Clients.Caller.ReceiveError(S["No speech-to-text deployment is configured."].Value);
                    return;
                }

                var deployment = await deploymentManager.FindByIdAsync(deploymentSettings.DefaultSpeechToTextDeploymentId);

                if (deployment is null)
                {
                    await Clients.Caller.ReceiveError(S["The configured speech-to-text deployment was not found."].Value);
                    return;
                }

#pragma warning disable MEAI001
                var sttClient = await clientFactory.CreateSpeechToTextClientAsync(deployment);
#pragma warning restore MEAI001

                await StreamTranscriptionAsync(sttClient, itemId, audioChunks, audioFormat, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                _logger.LogDebug("Audio transcription was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred while transcribing audio.");

            try
            {
                await Clients.Caller.ReceiveError(S["An error occurred while transcribing the audio. Please try again."].Value);
            }
            catch (Exception writeEx)
            {
                _logger.LogWarning(writeEx, "Failed to write transcription error message.");
            }
        }
    }

#pragma warning disable MEAI001
    private async Task StreamTranscriptionAsync(
        ISpeechToTextClient sttClient,
        string itemId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        CancellationToken cancellationToken = default)
    {
        var pipe = new Pipe();

        // Cancellation source to break the audio chunk loop when transcription fails.
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start streaming transcription in the background.
        var transcriptionTask = TranscribeAsync(itemId, pipe, audioFormat, sttClient, errorCts, cancellationToken);

        // Write audio chunks to the pipe as they arrive from SignalR.
        try
        {
            await foreach (var base64Chunk in audioChunks.WithCancellation(errorCts.Token))
            {
                try
                {
                    var bytes = Convert.FromBase64String(base64Chunk);
                    await pipe.Writer.WriteAsync(bytes, errorCts.Token);
                }
                catch (FormatException)
                {
                    continue;
                }
            }
        }
        catch (OperationCanceledException) when (errorCts.IsCancellationRequested)
        {
            // Transcription failed; stop consuming audio chunks.
        }

        // Signal that all audio has been sent.
        await pipe.Writer.CompleteAsync();

        // Wait for the transcription to finish processing all audio.
        await transcriptionTask;
    }

    private async Task TranscribeAsync(string itemId, Pipe pipe, string audioFormat, ISpeechToTextClient sttClient, CancellationTokenSource errorCts, CancellationToken cancellationToken)
    {
        try
        {
            await using var readerStream = pipe.Reader.AsStream();

            using var committedText = ZString.CreateStringBuilder();

            var sttOptions = new SpeechToTextOptions
            {
                SpeechLanguage = "en-US",
            };

            if (!string.IsNullOrWhiteSpace(audioFormat))
            {
                sttOptions.AdditionalProperties ??= [];
                sttOptions.AdditionalProperties["audioFormat"] = audioFormat;
            }

            await foreach (var update in sttClient.GetStreamingTextAsync(readerStream, sttOptions, cancellationToken))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                var isPartial = update.AdditionalProperties?.TryGetValue("isPartial", out var p) == true && p is true;

                if (isPartial)
                {
                    var display = committedText.ToString() + update.Text;
                    await Clients.Caller.ReceiveTranscript(itemId, display, false);
                }
                else
                {
                    if (committedText.Length > 0)
                    {
                        committedText.Append(' ');
                    }

                    committedText.Append(update.Text);
                    await Clients.Caller.ReceiveTranscript(itemId, committedText.ToString(), false);
                }
            }

            var finalText = committedText.ToString().TrimEnd();

            if (!string.IsNullOrEmpty(finalText))
            {
                await Clients.Caller.ReceiveTranscript(itemId, finalText, true);
            }
        }
        catch (Exception)
        {
            // Cancel the audio chunk loop so the error surfaces immediately.
            await errorCts.CancelAsync();
            throw;
        }
    }
#pragma warning restore MEAI001

}
