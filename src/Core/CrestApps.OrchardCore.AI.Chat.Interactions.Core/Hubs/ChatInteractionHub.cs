using System.Diagnostics;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Chat.Core;
using CrestApps.OrchardCore.AI.Chat.Core.Hubs;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Settings;
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

#pragma warning disable MEAI001 // Text-to-speech APIs from Microsoft.Extensions.AI are preview and require explicit opt-in at each usage site.
namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public class ChatInteractionHub : ChatHubBase<IChatInteractionHubClient>
{
    private readonly ILogger<ChatInteractionHub> _logger;

    public ChatInteractionHub(
        ILogger<ChatInteractionHub> logger,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
        : base(logger, stringLocalizer)
    {
        _logger = logger;
    }

    protected override ChatContextType GetChatType()
        => ChatContextType.ChatInteraction;

    public ChannelReader<CompletionPartialMessage> SendMessage(string itemId, string prompt, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        // Create a child scope for proper ISession/IDocumentStore lifecycle.
        _ = ShellScope.UsingChildScopeAsync(async scope =>
        {
            await HandlePromptAsync(channel.Writer, scope.ServiceProvider, itemId, prompt, cancellationToken);
        });

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
            var interactionManager = services.GetRequiredService<ICatalogManager<ChatInteraction>>();
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

            // Join the SignalR group for this interaction so deferred responses
            // (e.g., from an external agent via webhook) can reach this client.
            await Groups.AddToGroupAsync(Context.ConnectionId, GetInteractionGroupName(interaction.ItemId));

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
                    Appearance = message.As<AssistantMessageAppearance>(),
                })
            });
        });
    }

    /// <summary>
    /// Gets the SignalR group name for a chat interaction. Clients in this group
    /// receive deferred responses delivered via webhook or external callback.
    /// </summary>
    public static string GetInteractionGroupName(string itemId)
        => $"chat-interaction-{itemId}";

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
            var interactionManager = services.GetRequiredService<ICatalogManager<ChatInteraction>>();
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
            var interactionManager = services.GetRequiredService<ICatalogManager<ChatInteraction>>();
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

    private async Task HandlePromptAsync(ChannelWriter<CompletionPartialMessage> writer, IServiceProvider services, string itemId, string prompt, CancellationToken cancellationToken)
    {
        try
        {
            using var invocationScope = AIInvocationScope.Begin();

            if (string.IsNullOrWhiteSpace(itemId))
            {
                await Clients.Caller.ReceiveError(S["Interaction ID is required."].Value);

                return;
            }

            var interactionManager = services.GetRequiredService<ICatalogManager<ChatInteraction>>();
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

            // Ensure the caller joins the interaction group before any deferred webhook
            // notifications or live-agent messages are delivered.
            await Groups.AddToGroupAsync(Context.ConnectionId, GetInteractionGroupName(interaction.ItemId), cancellationToken);

            var promptStore = services.GetRequiredService<IChatInteractionPromptStore>();
            var handlerResolver = services.GetRequiredService<IChatResponseHandlerResolver>();
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

            var conversationHistory = existingPrompts
                .Where(x => !x.IsGeneratedPrompt)
                .Select(p => new ChatMessage(p.Role, p.Text))
                .ToList();

            // Resolve the chat response handler for this interaction.
            // In conversation mode, always use the AI handler for TTS/STT integration.
            var siteService = services.GetRequiredService<ISiteService>();
            var site = await siteService.GetSiteSettingsAsync();
            var chatMode = site.As<ChatInteractionChatModeSettings>().ChatMode;
            var handler = handlerResolver.Resolve(interaction.ResponseHandlerName, chatMode);

            var handlerContext = new ChatResponseHandlerContext
            {
                Prompt = prompt,
                ConnectionId = Context.ConnectionId,
                SessionId = interaction.ItemId,
                ChatType = ChatContextType.ChatInteraction,
                ConversationHistory = conversationHistory,
                Services = services,
                Interaction = interaction,
            };

            var handlerResult = await handler.HandleAsync(handlerContext, cancellationToken);

            if (handlerResult.IsDeferred)
            {
                // Deferred response: save user prompt (already done) and update title.
                // The response will arrive later via webhook or external callback.
                await Groups.AddToGroupAsync(Context.ConnectionId, GetInteractionGroupName(interaction.ItemId), cancellationToken);

                if (needsTitleUpdate)
                {
                    await interactionManager.UpdateAsync(interaction);
                }

                return;
            }

            // Streaming response: enumerate the response stream with citation collection.
            var assistantPrompt = new ChatInteractionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                ChatInteractionId = itemId,
                Role = ChatRole.Assistant,
                CreatedUtc = clock.UtcNow,
            };

            if (handlerContext.AssistantAppearance is not null)
            {
                assistantPrompt.Put(handlerContext.AssistantAppearance);
            }

            var builder = ZString.CreateStringBuilder();

            var contentItemIds = new HashSet<string>();
            var references = new Dictionary<string, AICompletionReference>();

            // Collect preemptive RAG references if the handler produced an OrchestrationContext.
            if (handlerContext.Properties.TryGetValue("OrchestrationContext", out var ctxObj) && ctxObj is OrchestrationContext orchestratorContext)
            {
                citationCollector.CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);
            }

            await foreach (var chunk in handlerResult.ResponseStream.WithCancellation(cancellationToken))
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
                    Appearance = handlerContext.AssistantAppearance,
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
        finally
        {
            writer.Complete();
        }
    }

    public async Task StartConversation(string itemId, IAsyncEnumerable<string> audioChunks, string audioFormat = null, string language = null)
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
                var interactionManager = services.GetRequiredService<ICatalogManager<ChatInteraction>>();
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
                var chatModeSettings = site.As<ChatInteractionChatModeSettings>();

                if (chatModeSettings.ChatMode != ChatMode.Conversation)
                {
                    await Clients.Caller.ReceiveError(S["Conversation mode is not enabled for chat interactions."].Value);
                    return;
                }

                var deploymentSettings = site.As<DefaultAIDeploymentSettings>();
                var speechToTextDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.SpeechToText);

                if (speechToTextDeployment is null)
                {
                    await Clients.Caller.ReceiveError(S["No speech-to-text deployment is configured or available."].Value);
                    return;
                }

                var textToSpeechDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.TextToSpeech);

                if (textToSpeechDeployment is null)
                {
                    await Clients.Caller.ReceiveError(S["No text-to-speech deployment is configured or available."].Value);
                    return;
                }

                using var speechToTextClient = await clientFactory.CreateSpeechToTextClientAsync(speechToTextDeployment);
                using var textToSpeechClient = await clientFactory.CreateTextToSpeechClientAsync(textToSpeechDeployment);

                var effectiveVoiceName = deploymentSettings.DefaultTextToSpeechVoiceId;
                var speechLanguage = !string.IsNullOrWhiteSpace(language) ? language : "en-US";

                using var conversationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Context.Items[ConversationCtsKey] = conversationCts;

                try
                {
                    await RunConversationLoopAsync(
                        itemId, audioChunks, audioFormat, speechLanguage,
                        speechToTextClient, textToSpeechClient, effectiveVoiceName, services, conversationCts.Token);
                }
                finally
                {
                    Context.Items.Remove(ConversationCtsKey);
                }

            });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                _logger.LogDebug("Conversation was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred during conversation mode.");

            try
            {
                await Clients.Caller.ReceiveError(S["An error occurred during the conversation. Please try again."].Value);
            }
            catch (Exception writeEx)
            {
                _logger.LogWarning(writeEx, "Failed to write conversation error message.");
            }
        }
    }

#pragma warning disable MEAI001
    private async Task RunConversationLoopAsync(
        string itemId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient speechToTextClient,
        ITextToSpeechClient textToSpeechClient,
        string voiceName,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var pipe = new Pipe();

        // CTS to break the audio chunk loop on transcription failure.
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start the transcription pipeline. No Task.Run needed because TranscribeConversationAsync
        // is async and returns at its first await, allowing the caller to proceed to the audio loop.
        var transcriptionTask = TranscribeConversationAsync(
            pipe.Reader, itemId, audioFormat, speechLanguage,
            speechToTextClient, textToSpeechClient, voiceName, services, errorCts, cancellationToken);

        // Write audio chunks to the pipe as they arrive.
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
            // Transcription error or connection aborted.
        }

        await pipe.Writer.CompleteAsync();
        await transcriptionTask;
    }

    private async Task TranscribeConversationAsync(
        PipeReader pipeReader,
        string itemId,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient speechToTextClient,
        ITextToSpeechClient textToSpeechClient,
        string voiceName,
        IServiceProvider services,
        CancellationTokenSource errorCts,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource currentResponseCts = null;
        Task currentResponseTask = null;

        try
        {
            await using var readerStream = pipeReader.AsStream();

            using var committedText = ZString.CreateStringBuilder();
            var sttOptions = new SpeechToTextOptions
            {
                SpeechLanguage = speechLanguage,
            };

            if (!string.IsNullOrWhiteSpace(audioFormat))
            {
                sttOptions.AdditionalProperties ??= [];
                sttOptions.AdditionalProperties["audioFormat"] = audioFormat;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("TranscribeConversationAsync: Starting STT stream. Language={Language}, Format={Format}.", speechLanguage, audioFormat);
            }

            await foreach (var update in speechToTextClient.GetStreamingTextAsync(readerStream, sttOptions, cancellationToken))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                var isPartial = update.AdditionalProperties?.TryGetValue("isPartial", out var p) == true && p is true;

                if (isPartial)
                {
                    var display = committedText.Length > 0
                        ? committedText.ToString() + update.Text
                        : update.Text;
                    await Clients.Caller.ReceiveTranscript(itemId, display, false);
                }
                else
                {
                    // User produced a complete utterance. Cancel any in-progress AI response
                    // so we can process the new prompt.
                    if (currentResponseCts != null)
                    {
                        _logger.LogDebug("TranscribeConversationAsync: New utterance received, cancelling previous AI response.");
                        await currentResponseCts.CancelAsync();

                        if (currentResponseTask != null)
                        {
                            try
                            {
                                await currentResponseTask;
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                                _logger.LogDebug("AI response was interrupted by new user speech.");
                            }
                        }

                        currentResponseCts.Dispose();
                        currentResponseCts = null;
                        currentResponseTask = null;
                    }

                    if (committedText.Length > 0)
                    {
                        committedText.Append(' ');
                    }

                    committedText.Append(update.Text);
                    var fullText = committedText.ToString().TrimEnd();

                    await Clients.Caller.ReceiveTranscript(itemId, fullText, true);
                    await Clients.Caller.ReceiveConversationUserMessage(itemId, fullText);

                    committedText.Clear();

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("TranscribeConversationAsync: Final utterance received: '{Text}'. Dispatching AI response.", fullText);
                    }

                    // Start the AI response as a non-blocking task so the STT loop continues
                    // reading and the user can interrupt the AI by speaking again.
                    currentResponseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    currentResponseTask = ProcessConversationPromptAsync(
                        itemId, fullText, textToSpeechClient, voiceName, services, currentResponseCts.Token);
                }
            }

            _logger.LogDebug("TranscribeConversationAsync: STT stream ended.");

            // Wait for any pending AI response after the audio stream ends.
            if (currentResponseTask != null)
            {
                try
                {
                    await currentResponseTask;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Interrupted.
                }

                currentResponseCts?.Dispose();
                currentResponseCts = null;
                currentResponseTask = null;
            }

            var remainingText = committedText.ToString().TrimEnd();

            if (!string.IsNullOrEmpty(remainingText))
            {
                await Clients.Caller.ReceiveConversationUserMessage(itemId, remainingText);

                try
                {
                    await ProcessConversationPromptAsync(
                        itemId, remainingText, textToSpeechClient, voiceName, services, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Interrupted.
                }
            }
        }
        catch (Exception)
        {
            await errorCts.CancelAsync();
            throw;
        }
    }

    private async Task ProcessConversationPromptAsync(
        string itemId,
        string prompt,
        ITextToSpeechClient textToSpeechClient,
        string voiceName,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("ProcessConversationPromptAsync: Starting for prompt length={PromptLength}.", prompt.Length);
        }

        var channel = Channel.CreateUnbounded<CompletionPartialMessage>();

        var handleTask = HandlePromptAsync(channel.Writer, services, itemId, prompt, cancellationToken);

        var sentenceChannel = Channel.CreateUnbounded<string>();
        string messageId = null;
        string responseId = null;

        // Start TTS consumer that sends audio per sentence (text is sent immediately below).
        var ttsTask = StreamSentencesAsSpeechAsync(textToSpeechClient, () => itemId, sentenceChannel.Reader, voiceName, cancellationToken);

        var sentenceBuffer = ZString.CreateStringBuilder();

        try
        {
            // Stream text tokens to the client IMMEDIATELY as they arrive from the AI model,
            // and also accumulate into sentences for parallel TTS synthesis.
            await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
            {
                messageId ??= chunk.MessageId;
                responseId ??= chunk.ResponseId;

                if (string.IsNullOrEmpty(chunk.Content))
                {
                    continue;
                }

                // Send text token to the client immediately so the user sees it right away.
                await Clients.Caller.ReceiveConversationAssistantToken(itemId, messageId ?? string.Empty, chunk.Content, responseId ?? string.Empty, chunk.Appearance);

                sentenceBuffer.Append(chunk.Content);

                // Queue completed sentences for TTS synthesis.
                if (SentenceBoundaryDetector.EndsWithSentenceBoundary(chunk.Content))
                {
                    var sentence = sentenceBuffer.ToString().Trim();

                    if (!string.IsNullOrEmpty(sentence))
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("ProcessConversationPromptAsync: Queuing sentence for TTS ({Length} chars).", sentence.Length);
                        }

                        await sentenceChannel.Writer.WriteAsync(sentence, cancellationToken);
                        sentenceBuffer.Dispose();
                        sentenceBuffer = ZString.CreateStringBuilder();
                    }
                }
            }

            await handleTask;

            // Flush any remaining text as the final sentence.
            var remaining = sentenceBuffer.ToString().Trim();
            sentenceBuffer.Dispose();

            if (!string.IsNullOrEmpty(remaining))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("ProcessConversationPromptAsync: Queuing final partial sentence for TTS ({Length} chars).", remaining.Length);
                }

                await sentenceChannel.Writer.WriteAsync(remaining, cancellationToken);
            }

            sentenceChannel.Writer.Complete();

            // Wait for all TTS sentences to finish streaming audio.
            await ttsTask;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("ProcessConversationPromptAsync: Completed. ItemId={ItemId}.", itemId);
            }
        }
        finally
        {
            sentenceChannel.Writer.TryComplete();
            sentenceBuffer.Dispose();

            // Always notify the client that the assistant response finished (or was
            // interrupted/cancelled) so the spinner stops even on error or cancellation.
            if (!string.IsNullOrEmpty(messageId))
            {
                try
                {
                    await Clients.Caller.ReceiveConversationAssistantComplete(itemId, messageId);
                }
                catch
                {
                    // Best-effort — the client may have disconnected.
                }
            }
        }
    }
#pragma warning restore MEAI001

    public async Task SendAudioStream(string itemId, IAsyncEnumerable<string> audioChunks, string audioFormat = null, string language = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(itemId)].Value);
            return;
        }

        var traceId = Guid.NewGuid().ToString("N")[..8];
        var sw = Stopwatch.StartNew();

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms SendAudioStream START. ItemId={ItemId}, Format={Format}",
                traceId, sw.ElapsedMilliseconds, itemId, audioFormat);
        }

        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var services = scope.ServiceProvider;
                var interactionManager = services.GetRequiredService<ICatalogManager<ChatInteraction>>();
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

                var speechToTextDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.SpeechToText);

                if (speechToTextDeployment is null)
                {
                    await Clients.Caller.ReceiveError(S["No speech-to-text deployment is configured or available."].Value);
                    return;
                }

#pragma warning disable MEAI001
                using var speechToTextClient = await clientFactory.CreateSpeechToTextClientAsync(speechToTextDeployment);
#pragma warning restore MEAI001

                var speechLanguage = !string.IsNullOrWhiteSpace(language) ? language : "en-US";

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms Scope resolved, STT client created. Starting StreamTranscriptionAsync...",
                        traceId, sw.ElapsedMilliseconds);
                }

                await StreamTranscriptionAsync(traceId, sw, speechToTextClient, itemId, audioChunks, audioFormat, speechLanguage, cancellationToken);

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms SendAudioStream COMPLETE.", traceId, sw.ElapsedMilliseconds);
                }
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
        string traceId,
        Stopwatch sw,
        ISpeechToTextClient speechToTextClient,
        string itemId,
        IAsyncEnumerable<string> audioChunks,
        string audioFormat,
        string speechLanguage,
        CancellationToken cancellationToken = default)
    {
        var pipe = new Pipe();
        var chunkCount = 0;
        var totalBytes = 0L;

        // CTS to break the audio chunk loop when transcription fails.
        using var errorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start streaming transcription in the background.
        var transcriptionTask = TranscribeAudioInputAsync(traceId, sw, itemId, pipe, audioFormat, speechLanguage, speechToTextClient, errorCts, cancellationToken);

        // Write audio chunks to the pipe as they arrive from SignalR.
        try
        {
            await foreach (var base64Chunk in audioChunks.WithCancellation(errorCts.Token))
            {
                try
                {
                    var bytes = Convert.FromBase64String(base64Chunk);
                    await pipe.Writer.WriteAsync(bytes, errorCts.Token);
                    chunkCount++;
                    totalBytes += bytes.Length;

                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms Pipe.Write chunk #{ChunkCount}: {Bytes} bytes (total={TotalBytes})",
                            traceId, sw.ElapsedMilliseconds, chunkCount, bytes.Length, totalBytes);
                    }
                }
                catch (FormatException)
                {
                    continue;
                }
            }
        }
        catch (OperationCanceledException) when (errorCts.IsCancellationRequested)
        {
            // Transcription failed or connection aborted.
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms All audio chunks received. Chunks={ChunkCount}, TotalBytes={TotalBytes}. Completing pipe...",
                traceId, sw.ElapsedMilliseconds, chunkCount, totalBytes);
        }

        // Signal that all audio has been sent.
        await pipe.Writer.CompleteAsync();

        // Wait for the transcription to finish processing all audio.
        await transcriptionTask;

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms StreamTranscriptionAsync DONE.", traceId, sw.ElapsedMilliseconds);
        }
    }

    private async Task TranscribeAudioInputAsync(
        string traceId,
        Stopwatch sw,
        string itemId,
        Pipe pipe,
        string audioFormat,
        string speechLanguage,
        ISpeechToTextClient speechToTextClient,
        CancellationTokenSource errorCts,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var readerStream = pipe.Reader.AsStream();

            using var committedText = ZString.CreateStringBuilder();
            var sttOptions = new SpeechToTextOptions
            {
                SpeechLanguage = speechLanguage,
            };

            if (!string.IsNullOrWhiteSpace(audioFormat))
            {
                sttOptions.AdditionalProperties ??= [];
                sttOptions.AdditionalProperties["audioFormat"] = audioFormat;
            }

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms TranscribeAudioInputAsync: calling GetStreamingTextAsync...",
                    traceId, sw.ElapsedMilliseconds);
            }

            var updateCount = 0;

            await foreach (var update in speechToTextClient.GetStreamingTextAsync(readerStream, sttOptions, cancellationToken))
            {
                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                updateCount++;
                var isPartial = update.AdditionalProperties?.TryGetValue("isPartial", out var p) == true && p is true;

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms Received update #{UpdateCount}: isPartial={IsPartial}, text='{Text}'",
                        traceId, sw.ElapsedMilliseconds, updateCount, isPartial, update.Text);
                }

                if (isPartial)
                {
                    var display = committedText.Length > 0
                        ? committedText.ToString() + update.Text
                        : update.Text;
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

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[HUB:{TraceId}] +{Elapsed}ms STT stream ended. Updates={UpdateCount}, FinalText='{FinalText}'",
                    traceId, sw.ElapsedMilliseconds, updateCount, finalText);
            }

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

    public async Task SynthesizeSpeech(string itemId, string text, string voiceName = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(itemId)].Value);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(text)].Value);
            return;
        }

        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var services = scope.ServiceProvider;
                var interactionManager = services.GetRequiredService<ICatalogManager<ChatInteraction>>();
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
                var chatModeSettings = site.As<ChatInteractionChatModeSettings>();

                if (chatModeSettings.ChatMode != ChatMode.Conversation)
                {
                    await Clients.Caller.ReceiveError(S["Text-to-speech is not enabled for chat interactions."].Value);
                    return;
                }

                var deploymentSettings = site.As<DefaultAIDeploymentSettings>();
                var textToSpeechDeployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.TextToSpeech);

                if (textToSpeechDeployment is null)
                {
                    await Clients.Caller.ReceiveError(S["No text-to-speech deployment is configured or available."].Value);
                    return;
                }

                using var textToSpeechClient = await clientFactory.CreateTextToSpeechClientAsync(textToSpeechDeployment);

                var effectiveVoiceName = !string.IsNullOrWhiteSpace(voiceName)
                    ? voiceName
                    : deploymentSettings.DefaultTextToSpeechVoiceId;

                await StreamSpeechAsync(textToSpeechClient, itemId, text, effectiveVoiceName, cancellationToken);

            });
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                _logger.LogDebug("Speech synthesis was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred while synthesizing speech.");

            try
            {
                await Clients.Caller.ReceiveError(S["An error occurred while synthesizing speech. Please try again."].Value);
            }
            catch (Exception writeEx)
            {
                _logger.LogWarning(writeEx, "Failed to write speech synthesis error message.");
            }
        }
    }
}
