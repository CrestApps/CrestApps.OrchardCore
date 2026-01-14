using System.Text;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
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
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public class ChatInteractionHub : Hub<IChatInteractionHubClient>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISourceCatalogManager<ChatInteraction> _interactionManager;
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IAICompletionService _completionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatInteractionHub> _logger;
    private readonly ISession _session;

    protected readonly IStringLocalizer S;

    public ChatInteractionHub(
        IAuthorizationService authorizationService,
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IAIDataSourceStore dataSourceStore,
        IAICompletionService completionService,
        IServiceProvider serviceProvider,
        ILogger<ChatInteractionHub> logger,
        ISession session,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _interactionManager = interactionManager;
        _dataSourceStore = dataSourceStore;
        _completionService = completionService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _session = session;
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

        await Clients.Caller.LoadInteraction(new
        {
            interaction.ItemId,
            interaction.Title,
            interaction.ConnectionName,
            interaction.DeploymentId,
            Messages = interaction.Prompts.Select(message => new AIChatResponseMessageDetailed
            {
                Id = message.Id,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
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
        string[] toolNames)
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
        interaction.DataSourceId = dataSourceId;
        interaction.ToolNames = toolNames?.ToList() ?? [];

        if (!string.IsNullOrWhiteSpace(interaction.DataSourceId))
        {
            var dataSource = await _dataSourceStore.FindByIdAsync(interaction.DataSourceId);

            if (dataSource is not null)
            {
                interaction.DataSourceId = dataSource.ItemId;
                interaction.DataSourceType = dataSource.Type;
            }
        }

        await _interactionManager.UpdateAsync(interaction);
        await _session.SaveChangesAsync();

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

        // Clear prompts but keep everything else
        interaction.Prompts.Clear();

        await _interactionManager.UpdateAsync(interaction);
        await _session.SaveChangesAsync();

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

            interaction.Prompts.Add(new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.User,
                Content = prompt,
            });

            if (string.IsNullOrEmpty(interaction.Title))
            {
                interaction.Title = Str.Truncate(prompt, 255);
            }

            var transcript = interaction.Prompts
                .Where(x => !x.IsGeneratedPrompt)
                .Select(p => new ChatMessage(p.Role, p.Content));

            var assistantMessage = new AIChatSessionPrompt
            {
                Id = IdGenerator.GenerateId(),
                Role = ChatRole.Assistant,
            };

            var builder = new StringBuilder();

            // Process documents using intent-aware, strategy-based approach
            var documentProcessingResult = await ProcessDocumentsAsync(interaction, prompt, cancellationToken);

            var systemMessage = interaction.SystemMessage ?? string.Empty;
            if (documentProcessingResult != null && documentProcessingResult.IsSuccess && documentProcessingResult.HasContext)
            {
                // Append document context to the system message
                systemMessage = systemMessage + "\n\n" + documentProcessingResult.GetCombinedContext();
            }

            var completionContext = new AICompletionContext
            {
                ConnectionName = interaction.ConnectionName,
                DeploymentId = interaction.DeploymentId,
                SystemMessage = systemMessage,
                Temperature = interaction.Temperature,
                TopP = interaction.TopP,
                FrequencyPenalty = interaction.FrequencyPenalty,
                PresencePenalty = interaction.PresencePenalty,
                MaxTokens = interaction.MaxTokens,
                PastMessagesCount = interaction.PastMessagesCount,
                ToolNames = interaction.ToolNames?.ToArray(),
                InstanceIds = interaction.ToolInstanceIds?.ToArray(),
                McpConnectionIds = interaction.McpConnectionIds?.ToArray(),
                UserMarkdownInResponse = true,
                DataSourceId = interaction.DataSourceId,
                DataSourceType = interaction.DataSourceType,
            };

            var contentItemIds = new HashSet<string>();
            var references = new Dictionary<string, AICompletionReference>();

            await foreach (var chunk in _completionService.CompleteStreamingAsync(interaction.Source, transcript, completionContext, cancellationToken))
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
                    MessageId = assistantMessage.Id,
                    Content = chunk.Text,
                    References = references,
                };

                await writer.WriteAsync(partialMessage, cancellationToken);
            }

            if (builder.Length > 0)
            {
                assistantMessage.Content = builder.ToString();
                assistantMessage.ContentItemIds = contentItemIds.ToList();
                assistantMessage.References = references;

                interaction.Prompts.Add(assistantMessage);
            }

            await _interactionManager.UpdateAsync(interaction);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException || (ex is TaskCanceledException && cancellationToken.IsCancellationRequested))
            {
                _logger.LogDebug("Chat interaction processing was cancelled.");
                return;
            }

            _logger.LogError(ex, "An error occurred while processing the chat interaction.");

            var errorMessage = new CompletionPartialMessage
            {
                SessionId = itemId,
                MessageId = IdGenerator.GenerateId(),
                Content = GetFriendlyErrorMessage(ex).Value,
            };

            await writer.WriteAsync(errorMessage, cancellationToken);
        }
        finally
        {
            writer.Complete();
        }
    }

    /// <summary>
    /// Processes documents using intent-aware, strategy-based approach.
    /// First detects the user's intent, then routes to the appropriate processing strategy.
    /// </summary>
    private async Task<DocumentProcessingResult> ProcessDocumentsAsync(ChatInteraction interaction, string prompt, CancellationToken cancellationToken)
    {
        // Check if there are documents attached
        if (interaction.Documents == null || interaction.Documents.Count == 0)
        {
            return null;
        }

        // Get the intent detector (required for document processing)
        var intentDetector = _serviceProvider.GetService<IDocumentIntentDetector>();
        if (intentDetector == null)
        {
            _logger.LogWarning("Document intent detector is not available. Document processing will be skipped.");

            return null;
        }

        // Get the strategy provider (required for document processing)
        var strategyProvider = _serviceProvider.GetService<IDocumentProcessingStrategyProvider>();
        if (strategyProvider == null)
        {
            _logger.LogWarning("Document processing strategy provider is not available. Document processing will be skipped.");

            return null;
        }

        try
        {
            // Detect user intent
            var intentContext = new DocumentIntentDetectionContext
            {
                Prompt = prompt,
                Interaction = interaction,
                CancellationToken = cancellationToken,
                ServiceProvider = _serviceProvider,
            };

            var intentResult = await intentDetector.DetectAsync(intentContext);

            _logger.LogDebug("Detected document intent: {Intent} with confidence {Confidence}. Reason: {Reason}",
                intentResult.Intent, intentResult.Confidence, intentResult.Reason);

            // Process documents using the appropriate strategy
            var processingContext = new DocumentProcessingContext
            {
                Prompt = prompt,
                Interaction = interaction,
                IntentResult = intentResult,
                CancellationToken = cancellationToken,
                ServiceProvider = _serviceProvider,
            };

            await strategyProvider.ProcessAsync(processingContext);

            return processingContext.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document intent detection or processing.");
            return null;
        }
    }

    private LocalizedString GetFriendlyErrorMessage(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode is { } code)
            {
                return code switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                      => S["Authentication failed. Please check your API credentials."],

                    System.Net.HttpStatusCode.BadRequest
                      => S["Invalid request. Please verify your connection settings."],

                    System.Net.HttpStatusCode.NotFound
                      => S["The provider endpoint could not be found. Please verify the API URL."],

                    System.Net.HttpStatusCode.TooManyRequests
                      => S["Rate limit reached. Please wait and try again later."],

                    >= System.Net.HttpStatusCode.InternalServerError
                      => S["The provider service is currently unavailable. Please try again later."],

                    _ => S["An error occurred while communicating with the provider."]
                };
            }

            return S["Unable to reach the provider. Please check your connection or endpoint URL."];
        }

        return S["Our service is currently unavailable. Please try again later."];
    }
}
