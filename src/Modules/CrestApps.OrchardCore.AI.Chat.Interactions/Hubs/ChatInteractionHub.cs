using System.Text;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
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
        IClock clock,
        ILogger<ChatInteractionHub> logger,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        IDocumentStore documentStore,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _interactionManager = interactionManager;
        _promptStore = promptStore;
        _serviceProvider = serviceProvider;
        _orchestrationContextBuilder = orchestrationContextBuilder;
        _orchestratorResolver = orchestratorResolver;
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

    public async Task SaveSettings(
        string itemId,
        string title,
        string connectionName,
        string deploymentId,
        string systemMessage,
        float? temperature,
        float? topP,
        float? frequencyPenalty,
        float? presencePenalty,
        int? maxTokens,
        int? pastMessagesCount,
        string dataSourceId,
        int? strictness,
        int? topNDocuments,
        string filter,
        bool? isInScope,
        string[] toolNames,
        string[] mcpConnectionIds)
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

        interaction.Title = title ?? "Untitled";
        interaction.ConnectionName = connectionName;
        interaction.DeploymentId = deploymentId;
        interaction.SystemMessage = systemMessage;
        interaction.Temperature = temperature;
        interaction.TopP = topP;
        interaction.FrequencyPenalty = frequencyPenalty;
        interaction.PresencePenalty = presencePenalty;
        interaction.MaxTokens = maxTokens;
        interaction.PastMessagesCount = pastMessagesCount;
        interaction.ToolNames = toolNames?.ToList() ?? [];
        interaction.McpConnectionIds = mcpConnectionIds?.ToList() ?? [];

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
                        Strictness = strictness,
                        TopNDocuments = topNDocuments,
                        IsInScope = isInScope ?? true,
                        Filter = filter,
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

        await Clients.Caller.SettingsSaved(interaction.ItemId, interaction.Title);
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

            if (string.IsNullOrEmpty(interaction.Title))
            {
                interaction.Title = Str.Truncate(prompt, 255);
                await _interactionManager.UpdateAsync(interaction);
            }

            // Load all prompts for building transcript
            var existingPrompts = await _promptStore.GetPromptsAsync(itemId);

            var assistantPrompt = new ChatInteractionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                ChatInteractionId = itemId,
                Role = ChatRole.Assistant,
                CreatedUtc = DateTime.UtcNow,
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

            await foreach (var chunk in orchestrator.ExecuteStreamingAsync(orchestratorContext, cancellationToken))
            {
                if (chunk.AdditionalProperties is not null)
                {
                    if (chunk.AdditionalProperties.TryGetValue<IList<string>>("ContentItemIds", out var ids))
                    {
                        foreach (var id in ids)
                        {
                            contentItemIds.Add(id);
                        }
                    }

                    if (chunk.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
                    {
                        foreach (var (key, value) in referenceItems)
                        {
                            references[key] = value;
                        }
                    }
                }

                if (string.IsNullOrEmpty(chunk.Text))
                {
                    continue;
                }

                builder.Append(chunk.Text);

                var partialMessage = new CompletionPartialMessage
                {
                    SessionId = interaction.ItemId,
                    MessageId = assistantPrompt.ItemId,
                    Content = chunk.Text,
                    References = references,
                };

                await writer.WriteAsync(partialMessage, cancellationToken);
            }

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
