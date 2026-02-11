using System.Text;
using System.Threading.Channels;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public class ChatInteractionHub : Hub<IChatInteractionHubClient>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISourceCatalogManager<ChatInteraction> _interactionManager;
    private readonly IChatInteractionPromptStore _promptStore;
    private readonly IAIDataSourceStore _dataSourceStore;
    private readonly IAICompletionService _completionService;
    private readonly IAICompletionContextBuilder _completionContextBuilder;
    private readonly IPromptRouter _promptRouter;
    private readonly IClock _clock;
    private readonly ILogger<ChatInteractionHub> _logger;
    private readonly ISession _session;

    protected readonly IStringLocalizer S;

    public ChatInteractionHub(
        IAuthorizationService authorizationService,
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IChatInteractionPromptStore promptStore,
        IAIDataSourceStore dataSourceStore,
        IAICompletionService completionService,
        IAICompletionContextBuilder completionContextBuilder,
        IPromptRouter promptRouter,
        IClock clock,
        ILogger<ChatInteractionHub> logger,
        ISession session,
        IStringLocalizer<ChatInteractionHub> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _interactionManager = interactionManager;
        _promptStore = promptStore;
        _dataSourceStore = dataSourceStore;
        _completionService = completionService;
        _completionContextBuilder = completionContextBuilder;
        _promptRouter = promptRouter;
        _clock = clock;
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
            var dataSource = await _dataSourceStore.FindByIdAsync(dataSourceId);

            if (dataSource is not null)
            {
                interaction.Put(new ChatInteractionDataSourceMetadata()
                {
                    DataSourceType = dataSource.Type,
                    DataSourceId = dataSource.ItemId,
                });

                interaction.Put(new AzureRagChatMetadata()
                {
                    Strictness = strictness,
                    TopNDocuments = topNDocuments,
                    IsInScope = isInScope ?? true,
                    Filter = filter,
                });
            }
        }
        else
        {
            interaction.Put(new ChatInteractionDataSourceMetadata());
            interaction.Put(new AzureRagChatMetadata());
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

        // Clear prompts using the prompt store
        await _promptStore.DeleteAllPromptsAsync(itemId);
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

            var transcript = existingPrompts
                .Where(x => !x.IsGeneratedPrompt)
                .Select(p => new ChatMessage(p.Role, p.Text));

            var assistantPrompt = new ChatInteractionPrompt
            {
                ItemId = IdGenerator.GenerateId(),
                ChatInteractionId = itemId,
                Role = ChatRole.Assistant,
                CreatedUtc = DateTime.UtcNow,
            };

            var builder = new StringBuilder();

            // Process documents using intent-aware, strategy-based approach
            var documentProcessingResult = await ReasonAsync(interaction, existingPrompts, prompt, cancellationToken);

            // Handle chart generation results
            if (documentProcessingResult != null && documentProcessingResult.HasGeneratedChart)
            {
                await HandleChartGenerationResultAsync(writer, interaction, assistantPrompt, documentProcessingResult, cancellationToken);
                return;
            }

            // Handle chart generation errors
            if (documentProcessingResult != null && documentProcessingResult.IsChartGenerationIntent && !documentProcessingResult.IsSuccess)
            {
                var errorMessage = new CompletionPartialMessage
                {
                    SessionId = interaction.ItemId,
                    MessageId = assistantPrompt.ItemId,
                    Content = documentProcessingResult.ErrorMessage,
                };

                await writer.WriteAsync(errorMessage, cancellationToken);

                assistantPrompt.Text = documentProcessingResult.ErrorMessage;
                await _promptStore.CreateAsync(assistantPrompt);
                await _session.SaveChangesAsync(cancellationToken);

                return;
            }

            // Handle image generation results
            if (documentProcessingResult != null && documentProcessingResult.HasGeneratedImages)
            {
                await HandleImageGenerationResultAsync(writer, interaction, assistantPrompt, documentProcessingResult, cancellationToken);
                return;
            }

            // Handle image generation errors (use IsImageGenerationIntent flag instead of string check)
            if (documentProcessingResult != null && documentProcessingResult.IsImageGenerationIntent && !documentProcessingResult.IsSuccess)
            {
                var errorMessage = new CompletionPartialMessage
                {
                    SessionId = interaction.ItemId,
                    MessageId = assistantPrompt.ItemId,
                    Content = documentProcessingResult.ErrorMessage,
                };

                await writer.WriteAsync(errorMessage, cancellationToken);

                assistantPrompt.Text = documentProcessingResult.ErrorMessage;
                await _promptStore.CreateAsync(assistantPrompt);
                await _session.SaveChangesAsync(cancellationToken);

                return;
            }

            var completionContext = await _completionContextBuilder.BuildAsync(interaction, c =>
            {
                c.UserMarkdownInResponse = true;

                var systemMessage = c.SystemMessage ?? string.Empty;
                if (documentProcessingResult != null && documentProcessingResult.IsSuccess)
                {
                    if (documentProcessingResult.HasContext)
                    {
                        // Append document context to the system message
                        systemMessage = systemMessage + "\n\n" + documentProcessingResult.GetCombinedContext();
                    }

                    // Merge tool names requested by strategies into the completion context
                    // so they are resolved by the standard tool registration pipeline.
                    if (documentProcessingResult.ToolNames.Count > 0)
                    {
                        c.ToolNames = [.. c.ToolNames ?? [], .. documentProcessingResult.ToolNames];
                    }
                }

                c.SystemMessage = systemMessage;
            });

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
                Content = AIHubErrorMessageHelper.GetFriendlyErrorMessage(ex, S).Value,
            };

            await writer.WriteAsync(errorMessage, cancellationToken);
        }
        finally
        {
            writer.Complete();
        }
    }

    /// <summary>
    /// Processes prompt using intent-aware, strategy-based approach.
    /// First detects the user's intent, then routes to the appropriate processing strategy.
    /// </summary>
    private async Task<IntentProcessingResult> ReasonAsync(ChatInteraction interaction, IReadOnlyCollection<ChatInteractionPrompt> prompts, string prompt, CancellationToken cancellationToken)
    {
        var context = new PromptRoutingContext(interaction)
        {
            Prompt = prompt,
            Source = interaction.Source,
            ConnectionName = interaction.ConnectionName,
            Documents = interaction.Documents ?? [],
            ConversationHistory = BuildConversationHistory(prompts),
            MaxHistoryMessagesForImageGeneration = interaction.PastMessagesCount ?? 5,
        };

        return await _promptRouter.RouteAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds a conversation history from past prompts for context.
    /// </summary>
    private static List<ChatMessage> BuildConversationHistory(IReadOnlyCollection<ChatInteractionPrompt> prompts)
    {
        var history = new List<ChatMessage>();

        if (prompts == null || prompts.Count == 0)
        {
            return history;
        }

        foreach (var prompt in prompts.Where(p => !p.IsGeneratedPrompt))
        {
            history.Add(new ChatMessage(prompt.Role, prompt.Text));
        }

        return history;
    }


    /// <summary>
    /// Handles the result of image generation and sends the generated images to the client.
    /// </summary>
    private async Task HandleImageGenerationResultAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        ChatInteraction interaction,
        ChatInteractionPrompt assistantPrompt,
        IntentProcessingResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = result?.GeneratedImages;
            if (response?.Contents is null || response.Contents.Count == 0)
            {
                var emptyMessage = new CompletionPartialMessage
                {
                    SessionId = interaction.ItemId,
                    MessageId = assistantPrompt.ItemId,
                    Content = result?.ErrorMessage ?? S["No images were generated."].Value,
                };

                await writer.WriteAsync(emptyMessage, cancellationToken);
                return;
            }

            var messageBuilder = new StringBuilder();

            foreach (var contentItem in response.Contents)
            {
                var imageUri = ExtractImageUri(contentItem);

                if (string.IsNullOrWhiteSpace(imageUri))
                {
                    continue;
                }

                // Use markdown-style syntax with special marker for client-side rendering
                messageBuilder.AppendLine($"![Generated Image]({imageUri})");
                messageBuilder.AppendLine();
            }

            var content = messageBuilder.Length > 0
                ? messageBuilder.ToString()
                : (result?.ErrorMessage ?? S["No images were generated."].Value);

            var partialMessage = new CompletionPartialMessage
            {
                SessionId = interaction.ItemId,
                MessageId = assistantPrompt.ItemId,
                Content = content,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);

            assistantPrompt.Text = content;
            await _promptStore.CreateAsync(assistantPrompt);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling image generation result.");

            var errorMessage = new CompletionPartialMessage
            {
                SessionId = interaction.ItemId,
                MessageId = assistantPrompt.ItemId,
                Content = S["An error occurred while processing the generated image."].Value,
            };

            await writer.WriteAsync(errorMessage, cancellationToken);
        }
    }

    /// <summary>
    /// Handles the result of chart generation and sends the Chart.js configuration to the client.
    /// </summary>
    private async Task HandleChartGenerationResultAsync(
        ChannelWriter<CompletionPartialMessage> writer,
        ChatInteraction interaction,
        ChatInteractionPrompt assistantPrompt,
        IntentProcessingResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            var chartConfig = result?.GeneratedChartConfig;
            if (string.IsNullOrWhiteSpace(chartConfig))
            {
                var emptyMessage = new CompletionPartialMessage
                {
                    SessionId = interaction.ItemId,
                    MessageId = assistantPrompt.ItemId,
                    Content = result?.ErrorMessage ?? S["No chart was generated."].Value,
                };

                await writer.WriteAsync(emptyMessage, cancellationToken);
                return;
            }

            // Use a special marker format that the client will recognize and render as a chart
            // Format: [chart:<json-config>]
            var content = $"[chart:{chartConfig}]";

            var partialMessage = new CompletionPartialMessage
            {
                SessionId = interaction.ItemId,
                MessageId = assistantPrompt.ItemId,
                Content = content,
            };

            await writer.WriteAsync(partialMessage, cancellationToken);

            assistantPrompt.Text = content;
            await _promptStore.CreateAsync(assistantPrompt);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling chart generation result.");

            var errorMessage = new CompletionPartialMessage
            {
                SessionId = interaction.ItemId,
                MessageId = assistantPrompt.ItemId,
                Content = S["An error occurred while processing the generated chart."].Value,
            };

            await writer.WriteAsync(errorMessage, cancellationToken);
        }
    }

    /// <summary>
    /// Extracts the image URI from an AIContent object.
    /// Handles UriContent, DataContent, and other content types.
    /// </summary>
    private static string ExtractImageUri(AIContent contentItem)
    {
        if (contentItem is null)
        {
            return null;
        }

        // Check if it's a UriContent (most common for image generation)
        if (contentItem is UriContent uriContent)
        {
            return uriContent.Uri?.ToString();
        }

        // Check if it's a DataContent with a URI
        if (contentItem is DataContent dataContent && dataContent.Uri is not null)
        {
            return dataContent.Uri.ToString();
        }

        // Fallback: try to get raw value if it's somehow stored differently
        // The ToString() method for AIContent typically returns type name, so avoid using it
        return null;
    }
}
