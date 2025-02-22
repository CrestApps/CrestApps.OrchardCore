using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Services;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OrchardCore.Contents.Indexing;
using OrchardCore.Entities;
using OrchardCore.Search.AzureAI.Models;
using OrchardCore.Search.AzureAI.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureAISearchCompletionClient : AICompletionServiceBase, IAICompletionClient
{
    private static readonly AIProfileMetadata _defaultMetadata = new();

    private readonly IAIDeploymentStore _deploymentStore;
    private readonly AzureAISearchIndexSettingsService _azureAISearchIndexSettingsService;
    private readonly IAIToolsService _toolsService;
    private readonly IAILinkGenerator _linkGenerator;
    private readonly DefaultAIOptions _defaultOptions;
    private readonly AzureAISearchDefaultOptions _azureAISearchDefaultOptions;
    private readonly ILogger _logger;

    public AzureAISearchCompletionClient(
        IAIDeploymentStore deploymentStore,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<AzureAISearchDefaultOptions> azureAISearchDefaultOptions,
        AzureAISearchIndexSettingsService azureAISearchIndexSettingsService,
        IAIToolsService toolService,
        IAILinkGenerator linkGenerator,
        IOptions<DefaultAIOptions> defaultOptions,
        ILogger<AzureOpenAICompletionClient> logger)
        : base(providerOptions.Value)
    {
        _deploymentStore = deploymentStore;
        _azureAISearchIndexSettingsService = azureAISearchIndexSettingsService;
        _toolsService = toolService;
        _linkGenerator = linkGenerator;
        _defaultOptions = defaultOptions.Value;
        _azureAISearchDefaultOptions = azureAISearchDefaultOptions.Value;
        _logger = logger;
    }

    public string Name
        => AzureAISearchProfileSource.ImplementationName;

    public async Task<Microsoft.Extensions.AI.ChatCompletion> CompleteAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        (var connection, var deploymentName) = await GetConnectionAsync(context, AzureOpenAIConstants.ProviderName);

        if (connection is null)
        {
            _logger.LogWarning("Unable to chat. Unable to find the deployment associated with the profile with id '{ProfileId}' or a default DefaultDeploymentName.", context.Profile?.Id);

            return null;
        }

        var azureMessages = new List<ChatMessage>();

        var currentPrompt = string.Empty;

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Text))
            {
                continue;
            }

            if (message.Role == Microsoft.Extensions.AI.ChatRole.User)
            {
                azureMessages.Add(new UserChatMessage(message.Text));
                currentPrompt = message.Text;
            }
            else if (message.Role == Microsoft.Extensions.AI.ChatRole.Assistant)
            {
                azureMessages.Add(new AssistantChatMessage(message.Text));
            }
        }

        if (context.Profile is null || !context.Profile.TryGet<AIProfileMetadata>(out var metadata))
        {
            metadata = _defaultMetadata;
        }

        var pastMessageCount = metadata.PastMessagesCount ?? _defaultOptions.PastMessagesCount;
        var skip = GetTotalMessagesToSkip(azureMessages.Count, pastMessageCount);

        var prompts = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemMessage(context, metadata))
        };

        prompts.AddRange(azureMessages.Skip(skip).Take(pastMessageCount));

        var azureClient = GetChatClient(connection);

        var chatClient = azureClient.GetChatClient(deploymentName);

        var chatOptions = await GetOptionsWithDataSourceAsync(context);

        try
        {
            var data = await chatClient.CompleteChatAsync(prompts, chatOptions, cancellationToken);

            if (data is null)
            {
                return null;
            }

            if (data.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                await ProcessToolCallsAsync(prompts, data.Value.ToolCalls);

                // Create a new chat option that excludes references to data sources to address the limitations in Azure OpenAI.
                data = await chatClient.CompleteChatAsync(prompts, await GetOptionsAsync(context), cancellationToken);
            }

            var role = new Microsoft.Extensions.AI.ChatRole(data.Value.Role.ToString().ToLowerInvariant());
            var choices = new List<Microsoft.Extensions.AI.ChatMessage>();

            foreach (var choice in data.Value.Content)
            {
                choices.Add(new Microsoft.Extensions.AI.ChatMessage(role, choice.Text));
            }

            var result = new Microsoft.Extensions.AI.ChatCompletion(choices)
            {
                CompletionId = data.Value.Id,
                CreatedAt = data.Value.CreatedAt,
                ModelId = data.Value.Model,
                FinishReason = new Microsoft.Extensions.AI.ChatFinishReason(data.Value.FinishReason.ToString()),
                Usage = new Microsoft.Extensions.AI.UsageDetails()
                {
                    InputTokenCount = data.Value.Usage.InputTokenCount,
                    OutputTokenCount = data.Value.Usage.OutputTokenCount,
                    TotalTokenCount = data.Value.Usage.TotalTokenCount,
                },
            };

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var messageContext = data.Value.GetMessageContext();
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            if (messageContext?.Citations is not null && messageContext.Citations.Count > 0)
            {
                var linkContext = new Dictionary<string, object>
                {
                    { "prompt", currentPrompt },
                };

                var contentItemIds = new HashSet<string>();
                var references = new Dictionary<string, AICompletionReference>();
                foreach (var citation in messageContext.Citations)
                {
                    if (string.IsNullOrEmpty(citation.FilePath))
                    {
                        continue;
                    }

                    contentItemIds.Add(citation.FilePath);
                    var template = $"[doc{references.Count + 1}]";

                    references[template] = new AICompletionReference
                    {
                        Text = string.IsNullOrEmpty(citation.Title) ? template : citation.Title,
                        Link = _linkGenerator.GetContentItemPath(citation.FilePath, linkContext),
                        Title = citation.Title,
                    };
                }

                result.AdditionalProperties = new Microsoft.Extensions.AI.AdditionalPropertiesDictionary
                {
                    {"ContentItemIds", contentItemIds },
                    {"References", references },
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to get chat completion result from Azure OpenAI.");
        }

        return null;
    }

    public async IAsyncEnumerable<Microsoft.Extensions.AI.StreamingChatCompletionUpdate> CompleteStreamingAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, AICompletionContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        (var connection, var deploymentName) = await GetConnectionAsync(context, AzureOpenAIConstants.ProviderName);

        if (connection is null)
        {
            _logger.LogWarning("Unable to chat. Unable to find the deployment associated with the profile with id '{ProfileId}' or a default DefaultDeploymentName.", context.Profile?.Id);

            yield break;
        }

        var azureMessages = new List<ChatMessage>();

        var currentPrompt = string.Empty;

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Text))
            {
                continue;
            }

            if (message.Role == Microsoft.Extensions.AI.ChatRole.User)
            {
                azureMessages.Add(new UserChatMessage(message.Text));
                currentPrompt = message.Text;
            }
            else if (message.Role == Microsoft.Extensions.AI.ChatRole.Assistant)
            {
                azureMessages.Add(new AssistantChatMessage(message.Text));
            }
        }

        if (context.Profile is null || !context.Profile.TryGet<AIProfileMetadata>(out var metadata))
        {
            metadata = _defaultMetadata;
        }

        var pastMessageCount = metadata.PastMessagesCount ?? _defaultOptions.PastMessagesCount;
        var skip = GetTotalMessagesToSkip(azureMessages.Count, pastMessageCount);

        var prompts = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemMessage(context, metadata))
        };

        prompts.AddRange(azureMessages.Skip(skip).Take(pastMessageCount));

        var azureClient = GetChatClient(connection);

        var chatClient = azureClient.GetChatClient(deploymentName);

        var chatOptions = await GetOptionsWithDataSourceAsync(context);

        Dictionary<string, object> linkContext = null;

        await foreach (var update in chatClient.CompleteChatStreamingAsync(prompts, chatOptions, cancellationToken))
        {
            if (update.FinishReason == ChatFinishReason.ToolCalls)
            {
                await ProcessToolCallsAsync(prompts, update.ToolCallUpdates);

                await foreach (var newUpdate in chatClient.CompleteChatStreamingAsync(prompts, await GetOptionsAsync(context), cancellationToken))
                {
                    var result = new Microsoft.Extensions.AI.StreamingChatCompletionUpdate
                    {
                        ChoiceIndex = 0,
                        CompletionId = newUpdate.CompletionId,
                        CreatedAt = newUpdate.CreatedAt,
                        ModelId = newUpdate.Model,
                        Contents = newUpdate.ContentUpdate.Select(x => new Microsoft.Extensions.AI.TextContent(x.Text))
                        .Cast<Microsoft.Extensions.AI.AIContent>()
                        .ToList(),
                    };

                    if (newUpdate.FinishReason is not null)
                    {
                        result.FinishReason = new Microsoft.Extensions.AI.ChatFinishReason(newUpdate.FinishReason?.ToString());
                    }

                    if (newUpdate.Role is not null)
                    {
                        result.Role = new Microsoft.Extensions.AI.ChatRole(newUpdate.Role.ToString().ToLowerInvariant());
                    }

                    yield return result;
                }
            }
            else
            {
                var result = new Microsoft.Extensions.AI.StreamingChatCompletionUpdate
                {
                    ChoiceIndex = 0,
                    CompletionId = update.CompletionId,
                    CreatedAt = update.CreatedAt,
                    ModelId = update.Model,
                    Contents = update.ContentUpdate.Select(x => new Microsoft.Extensions.AI.TextContent(x.Text))
                    .Cast<Microsoft.Extensions.AI.AIContent>()
                    .ToList(),
                };

                if (update.FinishReason is not null)
                {
                    result.FinishReason = new Microsoft.Extensions.AI.ChatFinishReason(update.FinishReason?.ToString());
                }

                if (update.Role is not null)
                {
                    result.Role = new Microsoft.Extensions.AI.ChatRole(update.Role.ToString().ToLowerInvariant());
                }

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                var updateContext = update.GetMessageContext();
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

                if (updateContext?.Citations is not null && updateContext.Citations.Count > 0)
                {
                    linkContext ??= new Dictionary<string, object>
                    {
                        { "prompt", currentPrompt },
                    };

                    var contentItemIds = new HashSet<string>();
                    var references = new Dictionary<string, AICompletionReference>();
                    foreach (var citation in updateContext.Citations)
                    {
                        if (string.IsNullOrEmpty(citation.FilePath))
                        {
                            continue;
                        }

                        contentItemIds.Add(citation.FilePath);
                        var templateIndex = references.Count + 1;

                        var template = $"[doc{templateIndex}]";

                        references[template] = new AICompletionReference
                        {
                            Text = string.IsNullOrEmpty(citation.Title) ? template : citation.Title,
                            Index = templateIndex,
                            Link = _linkGenerator.GetContentItemPath(citation.FilePath, linkContext),
                            Title = citation.Title,
                        };
                    }

                    result.AdditionalProperties = new Microsoft.Extensions.AI.AdditionalPropertiesDictionary
                    {
                        {"ContentItemIds", contentItemIds },
                        {"References", references },
                    };
                }

                yield return result;
            }
        }
    }

    private async Task ProcessToolCallsAsync(List<ChatMessage> prompts, IEnumerable<ChatToolCall> tollCalls)
    {
        prompts.Add(ChatMessage.CreateAssistantMessage(tollCalls));

        foreach (var toolCall in tollCalls)
        {
            var function = await _toolsService.GetAsync(toolCall.FunctionName) as Microsoft.Extensions.AI.AIFunction;

            if (function is null)
            {
                continue;
            }

            var arguments = toolCall.FunctionArguments.ToObjectFromJson<Dictionary<string, object>>();

            var result = await function.InvokeAsync(arguments);

            if (result is string str)
            {
                prompts.Add(new ToolChatMessage(toolCall.Id, str));
            }
            else
            {
                var resultJson = JsonSerializer.Serialize(result);

                prompts.Add(new ToolChatMessage(toolCall.Id, resultJson));
            }
        }
    }

    private async Task ProcessToolCallsAsync(List<ChatMessage> prompts, IEnumerable<StreamingChatToolCallUpdate> tollCallsUpdate)
    {
        var tollCalls = tollCallsUpdate.Select(x => ChatToolCall.CreateFunctionToolCall(x.ToolCallId, x.FunctionName, x.FunctionArgumentsUpdate));

        prompts.Add(ChatMessage.CreateAssistantMessage(tollCalls));

        foreach (var toolCall in tollCallsUpdate)
        {
            var function = await _toolsService.GetAsync(toolCall.FunctionName) as Microsoft.Extensions.AI.AIFunction;

            if (function is null)
            {
                continue;
            }

            var arguments = toolCall.FunctionArgumentsUpdate.ToObjectFromJson<Dictionary<string, object>>();

            var result = await function.InvokeAsync(arguments);

            if (result is string str)
            {
                prompts.Add(new ToolChatMessage(toolCall.ToolCallId, str));
            }
            else
            {
                var resultJson = JsonSerializer.Serialize(result);

                prompts.Add(new ToolChatMessage(toolCall.ToolCallId, resultJson));
            }
        }
    }

    private static AzureOpenAIClient GetChatClient(AIProviderConnection connection)
    {
        var endpoint = new Uri($"https://{connection.GetAccountName()}.openai.azure.com/");

        var azureClient = new AzureOpenAIClient(endpoint, connection.GetApiKeyCredential());

        return azureClient;
    }

    private async Task<ChatCompletionOptions> GetOptionsWithDataSourceAsync(AICompletionContext context)
    {
        if (context.Profile is null || !context.Profile.TryGet<AzureAIProfileAISearchMetadata>(out var metadata))
        {
            throw new InvalidOperationException();
        }

        var chatOptions = await GetOptionsAsync(context);

        var indexSettings = await _azureAISearchIndexSettingsService.GetAsync(metadata.IndexName);

        if (indexSettings == null
            || string.IsNullOrEmpty(indexSettings.IndexFullName)
            || !_azureAISearchDefaultOptions.ConfigurationExists())
        {
            return chatOptions;
        }

        var keyField = indexSettings.IndexMappings?.FirstOrDefault(x => x.IsKey);

#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        chatOptions.AddDataSource(new AzureSearchChatDataSource()
        {
            Endpoint = new Uri(_azureAISearchDefaultOptions.Endpoint),
            IndexName = indexSettings.IndexFullName,

            Authentication = DataSourceAuthentication.FromApiKey(_azureAISearchDefaultOptions.Credential.Key),
            Strictness = metadata.Strictness ?? AzureOpenAIConstants.DefaultStrictness,
            TopNDocuments = metadata.TopNDocuments ?? AzureOpenAIConstants.DefaultTopNDocuments,
            QueryType = "simple",
            InScope = true,
            SemanticConfiguration = "default",
            OutputContexts = DataSourceOutputContexts.Citations,
            FieldMappings = new DataSourceFieldMappings()
            {
                TitleFieldName = GetBestTitleField(keyField),
                FilePathFieldName = keyField?.AzureFieldKey,
                ContentFieldSeparator = Environment.NewLine,
            }
        });
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        return chatOptions;
    }

    private async Task<ChatCompletionOptions> GetOptionsAsync(AICompletionContext context)
    {
        if (context.Profile is null || !context.Profile.TryGet<AIProfileMetadata>(out var metadata))
        {
            metadata = _defaultMetadata;
        }

        var chatOptions = new ChatCompletionOptions()
        {
            Temperature = metadata.Temperature ?? _defaultOptions.Temperature,
            TopP = metadata.TopP ?? _defaultOptions.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? _defaultOptions.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? _defaultOptions.PresencePenalty,
            MaxOutputTokenCount = metadata.MaxTokens ?? _defaultOptions.MaxOutputTokens,
        };

        if (!context.DisableTools && context.Profile is not null &&
            (context?.Profile.TryGet<AIProfileFunctionInvocationMetadata>(out var funcMetadata) ?? false))
        {
            if (funcMetadata.Names is not null && funcMetadata.Names.Length > 0)
            {
                foreach (var name in funcMetadata.Names)
                {
                    var tool = await _toolsService.GetByNameAsync(name);

                    if (tool is null || tool is not Microsoft.Extensions.AI.AIFunction function)
                    {
                        continue;
                    }

                    chatOptions.Tools.Add(function.ToChatTool());
                }
            }

            if (funcMetadata.InstanceIds is not null && funcMetadata.InstanceIds.Length > 0)
            {
                foreach (var instanceId in funcMetadata.InstanceIds)
                {
                    var tool = await _toolsService.GetByInstanceIdAsync(instanceId);

                    if (tool is null || tool is not Microsoft.Extensions.AI.AIFunction function)
                    {
                        continue;
                    }

                    chatOptions.Tools.Add(function.ToChatTool());
                }
            }
        }

        return chatOptions;
    }

    protected override async Task<AIDeployment> GetDeploymentAsync(AICompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.Profile.DeploymentId))
        {
            return await _deploymentStore.FindByIdAsync(content.Profile.DeploymentId);
        }

        return null;
    }

    private static string GetBestTitleField(AzureAISearchIndexMap keyField)
    {
        if (keyField == null || keyField.AzureFieldKey == IndexingConstants.ContentItemIdKey)
        {
            return AzureAISearchIndexManager.DisplayTextAnalyzedKey;
        }

        return null;
    }
}
