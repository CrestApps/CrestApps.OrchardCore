using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using Azure.Identity;
using CrestApps.Azure.Core;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using OpenAI.Chat;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureAISearchCompletionClient : AICompletionServiceBase, IAICompletionClient
{
    private static readonly AIProfileMetadata _defaultMetadata = new();

    private readonly INamedModelStore<AIDeployment> _deploymentStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAILinkGenerator _linkGenerator;
    private readonly IEnumerable<IAzureOpenAIDataSourceHandler> _azureOpenAIDataSourceHandlers;
    private readonly DefaultAIOptions _defaultOptions;
    private readonly ILogger _logger;

    private IAIToolsService _toolsService;
    private McpService _mcpService;
    private IModelStore<McpConnection> _mcpConnectionsStore;

    public AzureAISearchCompletionClient(
        INamedModelStore<AIDeployment> deploymentStore,
        IOptions<AIProviderOptions> providerOptions,
        IServiceProvider serviceProvider,
        IAILinkGenerator linkGenerator,
        IEnumerable<IAzureOpenAIDataSourceHandler> azureOpenAIDataSourceHandlers,
        IOptions<DefaultAIOptions> defaultOptions,
        ILogger<AzureOpenAICompletionClient> logger)
        : base(providerOptions.Value)
    {
        _deploymentStore = deploymentStore;
        _serviceProvider = serviceProvider;
        _linkGenerator = linkGenerator;
        _azureOpenAIDataSourceHandlers = azureOpenAIDataSourceHandlers;
        _defaultOptions = defaultOptions.Value;
        _logger = logger;
    }

    public string Name
        => AzureOpenAIConstants.AzureOpenAIOwnData;

    public async Task<Microsoft.Extensions.AI.ChatResponse> CompleteAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default)
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

        var functions = !context.DisableTools && context.Profile is not null
            ? await GetFunctionsAsync(context.Profile)
            : [];

        var chatOptions = await GetOptionsWithDataSourceAsync(context, functions);
        try
        {
            var data = await chatClient.CompleteChatAsync(prompts, chatOptions, cancellationToken);

            if (data is null)
            {
                return null;
            }

            if (data.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                await ProcessToolCallsAsync(prompts, data.Value.ToolCalls, functions);

                // Create a new chat option that excludes references to data sources to address the limitations in Azure OpenAI.
                data = await chatClient.CompleteChatAsync(prompts, GetOptions(context, functions), cancellationToken);
            }

            var role = new Microsoft.Extensions.AI.ChatRole(data.Value.Role.ToString().ToLowerInvariant());
            var choices = new List<Microsoft.Extensions.AI.ChatMessage>();

            foreach (var choice in data.Value.Content)
            {
                choices.Add(new Microsoft.Extensions.AI.ChatMessage(role, choice.Text));
            }

            var result = new Microsoft.Extensions.AI.ChatResponse(choices)
            {
                ResponseId = data.Value.Id,
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

    public async IAsyncEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> CompleteStreamingAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, AICompletionContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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

        var functions = !context.DisableTools && context.Profile is not null
            ? await GetFunctionsAsync(context.Profile)
            : [];

        var chatOptions = await GetOptionsWithDataSourceAsync(context, functions);

        Dictionary<string, object> linkContext = null;

        ChatCompletionOptions subSequenceContext = null;

        await foreach (var update in chatClient.CompleteChatStreamingAsync(prompts, chatOptions, cancellationToken))
        {
            if (update.FinishReason == ChatFinishReason.ToolCalls)
            {
                await ProcessToolCallsAsync(prompts, update.ToolCallUpdates);

                // Create a new chat option that excludes references to data sources to address the limitations in Azure OpenAI.
                subSequenceContext ??= GetOptions(context, functions);

                await foreach (var newUpdate in chatClient.CompleteChatStreamingAsync(prompts, subSequenceContext, cancellationToken))
                {
                    var result = new Microsoft.Extensions.AI.ChatResponseUpdate
                    {
                        ResponseId = newUpdate.CompletionId,
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
                var result = new Microsoft.Extensions.AI.ChatResponseUpdate
                {
                    ResponseId = update.CompletionId,
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

    private static async Task ProcessToolCallsAsync(List<ChatMessage> prompts, IEnumerable<ChatToolCall> tollCalls, IEnumerable<Microsoft.Extensions.AI.AIFunction> functions)
    {
        prompts.Add(ChatMessage.CreateAssistantMessage(tollCalls));

        foreach (var toolCall in tollCalls)
        {
            var function = functions.FirstOrDefault(x => x.Name == toolCall.FunctionName);

            if (function is null)
            {
                continue;
            }

            var arguments = toolCall.FunctionArguments.ToObjectFromJson<Microsoft.Extensions.AI.AIFunctionArguments>();

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
        _toolsService ??= _serviceProvider.GetService<IAIToolsService>();

        if (_toolsService is null)
        {
            return;
        }

        var tollCalls = tollCallsUpdate.Select(x => ChatToolCall.CreateFunctionToolCall(x.ToolCallId, x.FunctionName, x.FunctionArgumentsUpdate));

        prompts.Add(ChatMessage.CreateAssistantMessage(tollCalls));

        foreach (var toolCall in tollCallsUpdate)
        {
            var function = await _toolsService.GetAsync(toolCall.FunctionName) as Microsoft.Extensions.AI.AIFunction;

            if (function is null)
            {
                continue;
            }

            var arguments = toolCall.FunctionArgumentsUpdate.ToObjectFromJson<Microsoft.Extensions.AI.AIFunctionArguments>();

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

    private static AzureOpenAIClient GetChatClient(AIProviderConnectionEntry connection)
    {
        var endpoint = connection.GetEndpoint();
        var azureClient = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new AzureOpenAIClient(endpoint, new ApiKeyCredential(connection.GetApiKey())),
            AzureAuthenticationType.ManagedIdentity => new AzureOpenAIClient(endpoint, new ManagedIdentityCredential()),
            AzureAuthenticationType.Default => new AzureOpenAIClient(endpoint, new DefaultAzureCredential()),
            _ => throw new NotSupportedException("The provided authentication type is not supported.")
        };

        return azureClient;
    }

    private async Task<ChatCompletionOptions> GetOptionsWithDataSourceAsync(AICompletionContext context, IEnumerable<Microsoft.Extensions.AI.AIFunction> functions)
    {
        if (context.Profile is null)
        {
            throw new InvalidOperationException();
        }

        var chatOptions = GetOptions(context, functions);

        if (!context.Profile.TryGet<AIProfileDataSourceMetadata>(out var dataSourceMetadata) || string.IsNullOrEmpty(dataSourceMetadata.DataSourceType))
        {
            return chatOptions;
        }

        var dataSourceContext = new AzureOpenAIDataSourceContext(context.Profile);

        foreach (var handler in _azureOpenAIDataSourceHandlers)
        {
            if (!handler.CanHandle(dataSourceMetadata.DataSourceType))
            {
                continue;
            }

            await handler.ConfigureSourceAsync(chatOptions, dataSourceContext);
        }

        return chatOptions;
    }

    private ChatCompletionOptions GetOptions(AICompletionContext context, IEnumerable<Microsoft.Extensions.AI.AIFunction> functions)
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

        foreach (var function in functions)
        {
            chatOptions.Tools.Add(function.ToChatTool());
        }

        if (chatOptions.Tools.Count > 0)
        {
            chatOptions.ToolChoice = ChatToolChoice.CreateAutoChoice();
        }

        return chatOptions;
    }

    private async Task<IEnumerable<Microsoft.Extensions.AI.AIFunction>> GetFunctionsAsync(AIProfile profile)
    {
        var functions = new List<Microsoft.Extensions.AI.AIFunction>();

        if (profile.TryGet<AIProfileFunctionInvocationMetadata>(out var funcMetadata))
        {
            _toolsService ??= _serviceProvider.GetService<IAIToolsService>();

            if (_toolsService is not null && funcMetadata.Names is not null && funcMetadata.Names.Length > 0)
            {
                foreach (var name in funcMetadata.Names)
                {
                    var tool = await _toolsService.GetByNameAsync(name);

                    if (tool is null || tool is not Microsoft.Extensions.AI.AIFunction function)
                    {
                        continue;
                    }

                    functions.Add(function);

                    continue;
                }
            }
        }

        if (profile.TryGet<AIProfileFunctionInstancesMetadata>(out var instancesMetadata))
        {
            if (_toolsService is not null && instancesMetadata.InstanceIds is not null && instancesMetadata.InstanceIds.Length > 0)
            {
                foreach (var instanceId in instancesMetadata.InstanceIds)
                {
                    var tool = await _toolsService.GetByInstanceIdAsync(instanceId);

                    if (tool is null || tool is not Microsoft.Extensions.AI.AIFunction function)
                    {
                        continue;
                    }

                    functions.Add(function);

                    continue;
                }
            }
        }

        if (profile.TryGet<AIProfileMcpMetadata>(out var mcpMetadata) &&
            mcpMetadata.ConnectionIds is not null &&
            mcpMetadata.ConnectionIds.Length > 0)
        {
            // Lazily load MCP services in case the MCP feature is disabled.
            _mcpConnectionsStore ??= _serviceProvider.GetService<IModelStore<McpConnection>>();

            if (_mcpConnectionsStore is not null)
            {
                _mcpService ??= _serviceProvider.GetService<McpService>();

                if (_mcpService is not null)
                {
                    var connections = (await _mcpConnectionsStore.GetAllAsync())
                        .ToDictionary(x => x.Id);

                    foreach (var connectionId in mcpMetadata.ConnectionIds)
                    {
                        if (!connections.TryGetValue(connectionId, out var connection))
                        {
                            continue;
                        }

                        var client = await _mcpService.GetOrCreateClientAsync(connection);

                        if (client is null)
                        {
                            continue;
                        }

                        foreach (var tool in await client.ListToolsAsync())
                        {
                            if (tool is not Microsoft.Extensions.AI.AIFunction function)
                            {
                                continue;
                            }

                            functions.Add(function);

                            continue;
                        }
                    }
                }
            }
        }

        return functions;
    }

    protected override async Task<AIDeployment> GetDeploymentAsync(AICompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.Profile.DeploymentId))
        {
            return await _deploymentStore.FindByIdAsync(content.Profile.DeploymentId);
        }

        return null;
    }
}
