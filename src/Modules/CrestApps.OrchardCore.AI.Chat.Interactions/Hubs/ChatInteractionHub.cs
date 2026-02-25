using System.Text;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Data.Documents;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public class ChatInteractionHub : Hub<IChatInteractionHubClient>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISourceCatalogManager<ChatInteraction> _interactionManager;
    private readonly IChatInteractionPromptStore _promptStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOrchestrationContextBuilder _orchestrationContextBuilder;
    private readonly IOrchestratorResolver _orchestratorResolver;
    private readonly IEnumerable<IChatInteractionSettingsHandler> _settingsHandlers;
    private readonly CitationReferenceCollector _citationCollector;
    private readonly IClock _clock;
    private readonly ILogger<ChatInteractionHub> _logger;
    private readonly IDocumentStore _documentStore;

    protected readonly IStringLocalizer S;

    public ChatInteractionHub(
        IAuthorizationService authorizationService,
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IChatInteractionPromptStore promptStore,
        IServiceProvider serviceProvider,
        IOrchestrationContextBuilder orchestrationContextBuilder,
        IOrchestratorResolver orchestratorResolver,
        IEnumerable<IChatInteractionSettingsHandler> settingsHandlers,
        CitationReferenceCollector citationCollector,
        IClock clock,
        ILogger<ChatInteractionHub> logger,
        IDocumentStore documentStore,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _interactionManager = interactionManager;
        _promptStore = promptStore;
        _serviceProvider = serviceProvider;
        _orchestrationContextBuilder = orchestrationContextBuilder;
        _orchestratorResolver = orchestratorResolver;
        _settingsHandlers = settingsHandlers;
        _citationCollector = citationCollector;
        _clock = clock;
        _logger = logger;
        _documentStore = documentStore;
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

        var httpContext = Context.GetHttpContext();

        var interaction = await _interactionManager.FindByIdAsync(itemId);

        if (interaction is null)
        {
            await Clients.Caller.ReceiveError(S["Interaction not found."].Value);

            return;
        }

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
        {
            await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);

            return;
        }

        var prompts = await _promptStore.GetPromptsAsync(itemId);

        await Clients.Caller.LoadInteraction(new
        {
            interaction.ItemId,
            interaction.Title,
            interaction.ConnectionName,
            interaction.DeploymentId,
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
    }

    public async Task SaveSettings(string itemId, JsonElement settings)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            await Clients.Caller.ReceiveError(S["{0} is required.", nameof(itemId)].Value);

            return;
        }

        var interaction = await _interactionManager.FindByIdAsync(itemId);

        if (interaction == null)
        {
            await Clients.Caller.ReceiveError(S["Interaction not found."].Value);

            return;
        }

        var httpContext = Context.GetHttpContext();

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
        {
            await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);

            return;
        }

        // Let module-specific handlers bind their own properties first.
        foreach (var handler in _settingsHandlers)
        {
            await handler.UpdatingAsync(interaction, settings);
        }

        // Apply core properties from the settings payload.
        interaction.Title = GetString(settings, "title") ?? "Untitled";
        interaction.OrchestratorName = GetString(settings, "orchestratorName");
        interaction.ConnectionName = GetString(settings, "connectionName");
        interaction.DeploymentId = GetString(settings, "deploymentId");
        interaction.SystemMessage = GetString(settings, "systemMessage");
        interaction.Temperature = GetFloat(settings, "temperature");
        interaction.TopP = GetFloat(settings, "topP");
        interaction.FrequencyPenalty = GetFloat(settings, "frequencyPenalty");
        interaction.PresencePenalty = GetFloat(settings, "presencePenalty");
        interaction.MaxTokens = GetInt(settings, "maxTokens");
        interaction.PastMessagesCount = GetInt(settings, "pastMessagesCount");
        interaction.ToolNames = GetStringArray(settings, "toolNames");
        interaction.McpConnectionIds = GetStringArray(settings, "mcpConnectionIds");

        var dataSourceId = GetString(settings, "dataSourceId");

        if (!string.IsNullOrWhiteSpace(dataSourceId))
        {
            var dataSourceStore = _serviceProvider.GetService<ICatalog<AIDataSource>>();
            if (dataSourceStore is not null)
            {
                var dataSource = await dataSourceStore.FindByIdAsync(dataSourceId);

                if (dataSource is not null)
                {
                    interaction.Put(new ChatInteractionDataSourceMetadata()
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
            interaction.Put(new ChatInteractionDataSourceMetadata());
            interaction.Put(new AIDataSourceRagMetadata());
        }

        await _interactionManager.UpdateAsync(interaction);
        await _documentStore.CommitAsync();

        // Let handlers react after the interaction has been persisted.
        foreach (var handler in _settingsHandlers)
        {
            await handler.UpdatedAsync(interaction, settings);
        }

        await Clients.Caller.SettingsSaved(interaction.ItemId, interaction.Title);
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

        var interaction = await _interactionManager.FindByIdAsync(itemId);

        if (interaction == null)
        {
            await Clients.Caller.ReceiveError(S["Interaction not found."].Value);

            return;
        }

        var httpContext = Context.GetHttpContext();

        if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
        {
            await Clients.Caller.ReceiveError(S["You are not authorized to access chat interactions."].Value);

            return;
        }

        // Clear prompts using the prompt store
        await _promptStore.DeleteAllPromptsAsync(itemId);
        await _documentStore.CommitAsync();

        await Clients.Caller.HistoryCleared(interaction.ItemId);
    }

    private async Task HandlePromptAsync(ChannelWriter<CompletionPartialMessage> writer, string itemId, string prompt, CancellationToken cancellationToken)
    {
        using var invocationScope = AIInvocationScope.Begin();

        try
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                await Clients.Caller.ReceiveError(S["Interaction ID is required."].Value);

                return;
            }

            var interaction = await _interactionManager.FindByIdAsync(itemId);

            if (interaction == null)
            {
                await Clients.Caller.ReceiveError(S["Interaction not found."].Value);

                return;
            }

            var httpContext = Context.GetHttpContext();

            if (!await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.EditChatInteractions, interaction))
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

            // Create and save user prompt
            var userPrompt = new ChatInteractionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                ChatInteractionId = itemId,
                Role = ChatRole.User,
                Text = prompt,
                CreatedUtc = _clock.UtcNow,
            };

            await _promptStore.CreateAsync(userPrompt);

            var needsTitleUpdate = string.IsNullOrEmpty(interaction.Title);
            if (needsTitleUpdate)
            {
                interaction.Title = Str.Truncate(prompt, 255);
            }

            // Load all prompts for building transcript
            var existingPrompts = await _promptStore.GetPromptsAsync(itemId);

            var assistantPrompt = new ChatInteractionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                ChatInteractionId = itemId,
                Role = ChatRole.Assistant,
                CreatedUtc = _clock.UtcNow,
            };

            var builder = new StringBuilder();

            // Build the orchestration context using the handler pipeline.
            var orchestratorContext = await _orchestrationContextBuilder.BuildAsync(interaction, ctx =>
            {
                ctx.UserMessage = prompt;
                ctx.ConversationHistory = existingPrompts
                    .Where(x => !x.IsGeneratedPrompt)
                    .Select(p => new ChatMessage(p.Role, p.Text))
                    .ToList();
            });

            // Resolve the orchestrator for this interaction and execute the completion.
            var orchestrator = _orchestratorResolver.Resolve(interaction.OrchestratorName);

            var contentItemIds = new HashSet<string>();
            var references = new Dictionary<string, AICompletionReference>();

            // Collect preemptive RAG references before streaming so the first chunk
            // already contains any references from data sources and documents.
            _citationCollector.CollectPreemptiveReferences(orchestratorContext, references, contentItemIds);

            await foreach (var chunk in orchestrator.ExecuteStreamingAsync(orchestratorContext, cancellationToken))
            {
                if (string.IsNullOrEmpty(chunk.Text))
                {
                    continue;
                }

                builder.Append(chunk.Text);

                // Incrementally collect any new tool references that appeared during streaming.
                _citationCollector.CollectToolReferences(references, contentItemIds);

                var partialMessage = new CompletionPartialMessage
                {
                    SessionId = interaction.ItemId,
                    MessageId = assistantPrompt.ItemId,
                    Content = chunk.Text,
                    References = references,
                };

                await writer.WriteAsync(partialMessage, cancellationToken);
            }

            // Final pass to collect any tool references added by the last tool call.
            _citationCollector.CollectToolReferences(references, contentItemIds);

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

                await _promptStore.CreateAsync(assistantPrompt);
            }

            // Update the interaction title after streaming is done to avoid holding
            // database locks for the duration of the AI response, which can cause
            // deadlocks with concurrent SaveSettings calls.
            if (needsTitleUpdate)
            {
                await _interactionManager.UpdateAsync(interaction);
            }

            await _documentStore.CommitAsync();
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

}
