using System.Text;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;
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
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public class ChatInteractionHub : Hub<IChatInteractionHubClient>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISourceCatalogManager<ChatInteraction> _interactionManager;
    private readonly IAICompletionService _completionService;
    private readonly ISiteService _siteService;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IAIClientFactory _aIClientFactory;
    private readonly IOptions<AIProviderOptions> _providerOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatInteractionHub> _logger;

    protected readonly IStringLocalizer S;

    public ChatInteractionHub(
        IAuthorizationService authorizationService,
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IAICompletionService completionService,
        ISiteService siteService,
        IIndexProfileStore indexProfileStore,
        IAIClientFactory aIClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        IServiceProvider serviceProvider,
        ILogger<ChatInteractionHub> logger,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _interactionManager = interactionManager;
        _completionService = completionService;
        _siteService = siteService;
        _indexProfileStore = indexProfileStore;
        _aIClientFactory = aIClientFactory;
        _providerOptions = providerOptions;
        _serviceProvider = serviceProvider;
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
        int? pastMessagesCount)
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

        await _interactionManager.UpdateAsync(interaction);

        await Clients.Caller.SettingsSaved(interaction.ItemId, interaction.Title);
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

            // Retrieve document context for RAG if documents are attached
            var documentContext = await GetDocumentContextAsync(interaction, prompt, cancellationToken);

            var systemMessage = interaction.SystemMessage ?? string.Empty;
            if (!string.IsNullOrEmpty(documentContext))
            {
                // Prepend document context to the system message
                var contextPrefix = "The following is relevant context from uploaded documents. Use this information to answer the user's question:\n\n" + documentContext + "\n\n";
                systemMessage = contextPrefix + systemMessage;
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

    /// <summary>
    /// Retrieves relevant document context for RAG by embedding the user prompt and searching for similar chunks.
    /// </summary>
    private async Task<string> GetDocumentContextAsync(ChatInteraction interaction, string prompt, CancellationToken cancellationToken)
    {
        // Check if there are documents attached
        if (interaction.Documents == null || interaction.Documents.Count == 0)
        {
            return null;
        }

        try
        {
            // Get the document settings to find the index profile
            var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();

            if (string.IsNullOrEmpty(settings.IndexProfileName))
            {
                _logger.LogWarning("Documents are attached but no index profile is configured. Document context will not be used.");
                return null;
            }

            // Find the index profile
            var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);

            if (indexProfile == null)
            {
                _logger.LogWarning("Index profile '{IndexProfileName}' not found. Document context will not be used.", settings.IndexProfileName);
                return null;
            }

            // Get the embedding search service for this provider
            var searchService = _serviceProvider.GetKeyedService<IEmbeddingSearchService>(indexProfile.ProviderName);

            if (searchService == null)
            {
                _logger.LogWarning("No embedding search service registered for provider '{ProviderName}'. Document context will not be used.", indexProfile.ProviderName);
                return null;
            }

            // Get embedding for the user's prompt
            var providerName = interaction.Source;
            var connectionName = interaction.ConnectionName;
            string deploymentName = null;

            if (_providerOptions.Value.Providers.TryGetValue(providerName, out var provider))
            {
                if (string.IsNullOrEmpty(connectionName))
                {
                    connectionName = provider.DefaultConnectionName;
                }

                if (!string.IsNullOrEmpty(connectionName) && provider.Connections.TryGetValue(connectionName, out var connection))
                {
                    deploymentName = connection.GetDefaultEmbeddingDeploymentName(false);
                }
            }

            if (string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("No embedding deployment configured. Document context will not be used.");
                return null;
            }

            var embeddingGenerator = await _aIClientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);

            if (embeddingGenerator == null)
            {
                _logger.LogWarning("Failed to create embedding generator. Document context will not be used.");
                return null;
            }

            // Generate embedding for the prompt
            var embedding = await embeddingGenerator.GenerateAsync(prompt, cancellationToken: cancellationToken);

            if (embedding?.Vector == null || embedding.Vector.Length == 0)
            {
                _logger.LogWarning("Failed to generate embedding for prompt. Document context will not be used.");
                return null;
            }

            // Search for similar document chunks
            var topN = interaction.DocumentTopN ?? settings.TopN;

            if (topN <= 0)
            {
                topN = 3;
            }

            var results = await searchService.SearchAsync(
                indexProfile.IndexName,
                embedding.Vector.ToArray(),
                interaction.ItemId,
                topN,
                cancellationToken);

            if (results == null || !results.Any())
            {
                return null;
            }

            // Combine the relevant chunks into context
            var contextBuilder = new StringBuilder();

            foreach (var result in results)
            {
                if (result.Chunk != null && !string.IsNullOrWhiteSpace(result.Chunk.Content))
                {
                    contextBuilder.AppendLine("---");
                    contextBuilder.AppendLine(result.Chunk.Content);
                }
            }

            return contextBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document context. Document context will not be used.");
            return null;
        }
    }
}
