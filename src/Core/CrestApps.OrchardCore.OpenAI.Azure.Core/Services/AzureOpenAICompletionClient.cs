using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using Azure.Identity;
using CrestApps.Azure.Core;
using CrestApps.Azure.Core.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAICompletionClient : AICompletionServiceBase, IAICompletionClient
{
    private readonly INamedCatalog<AIDeployment> _deploymentStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAILinkGenerator _linkGenerator;
    private readonly IEnumerable<IAzureOpenAIDataSourceHandler> _azureOpenAIDataSourceHandlers;
    private readonly ILogger _logger;

    private IAIToolsService _toolsService;

    private AzureOpenAIClientOptions _clientOptions;

    public AzureOpenAICompletionClient(
        INamedCatalog<AIDeployment> deploymentStore,
        IOptions<AIProviderOptions> providerOptions,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IAILinkGenerator linkGenerator,
        IEnumerable<IAzureOpenAIDataSourceHandler> azureOpenAIDataSourceHandlers,
        ILogger<AzureOpenAICompletionClient> logger)
        : base(providerOptions.Value)
    {
        _deploymentStore = deploymentStore;
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
        _linkGenerator = linkGenerator;
        _azureOpenAIDataSourceHandlers = azureOpenAIDataSourceHandlers;
        _logger = logger;
    }

    public string Name
        => AzureOpenAIConstants.ProviderName;

    public async Task<Microsoft.Extensions.AI.ChatResponse> CompleteAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, AICompletionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        if (!ProviderOptions.Providers.TryGetValue(AzureOpenAIConstants.ProviderName, out var provider))
        {
            throw new ArgumentException($"Provider '{AzureOpenAIConstants.ProviderName}' not found.");
        }

        var connectionName = GetDefaultConnectionName(provider, context.ConnectionName);

        if (string.IsNullOrEmpty(connectionName))
        {
            _logger.LogWarning("Unable to chat. Unable to find a connection '{ConnectionName}' or the default connection", context.ConnectionName);

            return null;
        }

        if (!provider.Connections.TryGetValue(connectionName, out var connectionProperties))
        {
            _logger.LogWarning("Unable to chat. Unable to find a connection '{ConnectionName}'", context.ConnectionName);

            return null;
        }

        var deploymentName = GetDefaultDeploymentName(provider, connectionName);

        if (string.IsNullOrEmpty(deploymentName))
        {
            _logger.LogWarning("Unable to chat. Unable to find a deployment id '{DeploymentId}' or the default deployment", context.DeploymentId);

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

        var prompts = GetPrompts(context, azureMessages);

        var azureClient = GetChatClient(connectionProperties);

        var chatClient = azureClient.GetChatClient(deploymentName);

        var functions = !context.DisableTools
            ? await GetFunctionsAsync(context.ToolNames, context.InstanceIds)
            : [];

        var chatOptions = await GetOptionsWithDataSourceAsync(context, functions);
        await ConfigureOptionsAsync(chatOptions, context, prompts);
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
                    var templateIndex = references.Count + 1;
                    var template = $"[doc{templateIndex}]";

                    references[template] = new AICompletionReference
                    {
                        Text = string.IsNullOrEmpty(citation.Title) ? template : citation.Title,
                        Link = _linkGenerator.GetContentItemPath(citation.FilePath, linkContext),
                        Title = citation.Title,
                        Index = templateIndex,
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
            _logger.LogWarning("Unable to chat. Unable to find a connection '{ConnectionName}' or the default connection", context.ConnectionName);

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

        var azureClient = GetChatClient(connection);

        var chatClient = azureClient.GetChatClient(deploymentName);

        var functions = !context.DisableTools
            ? await GetFunctionsAsync(context.ToolNames, context.InstanceIds)
            : [];

        var chatOptions = await GetOptionsWithDataSourceAsync(context, functions);

        Dictionary<string, object> linkContext = null;

        ChatCompletionOptions subSequenceContext = null;

        var prompts = GetPrompts(context, azureMessages);

        await ConfigureOptionsAsync(chatOptions, context, prompts);

        // Accumulate tool call updates across streaming chunks.
        // Key is the tool call index, value contains the accumulated tool call data.
        var accumulatedToolCalls = new Dictionary<int, (string ToolCallId, string FunctionName, List<byte> ArgumentBytes)>();

        await foreach (var update in chatClient.CompleteChatStreamingAsync(prompts, chatOptions, cancellationToken))
        {
            // Accumulate tool call updates as they arrive.
            foreach (var toolCallUpdate in update.ToolCallUpdates)
            {
                if (!accumulatedToolCalls.TryGetValue(toolCallUpdate.Index, out var accumulated))
                {
                    accumulated = (toolCallUpdate.ToolCallId, toolCallUpdate.FunctionName, new List<byte>());
                    accumulatedToolCalls[toolCallUpdate.Index] = accumulated;
                }

                // Update ToolCallId and FunctionName if they are provided in this chunk.
                if (!string.IsNullOrEmpty(toolCallUpdate.ToolCallId))
                {
                    accumulated.ToolCallId = toolCallUpdate.ToolCallId;
                }

                if (!string.IsNullOrEmpty(toolCallUpdate.FunctionName))
                {
                    accumulated.FunctionName = toolCallUpdate.FunctionName;
                }

                // Append function arguments bytes.
                if (toolCallUpdate.FunctionArgumentsUpdate is not null)
                {
                    accumulated.ArgumentBytes.AddRange(toolCallUpdate.FunctionArgumentsUpdate.ToArray());
                }

                accumulatedToolCalls[toolCallUpdate.Index] = accumulated;
            }

            if (update.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Convert accumulated tool call data to ChatToolCall objects.
                var toolCalls = accumulatedToolCalls.Values
                    .Where(tc => !string.IsNullOrEmpty(tc.ToolCallId) && !string.IsNullOrEmpty(tc.FunctionName))
                    .Select(tc => ChatToolCall.CreateFunctionToolCall(
                        tc.ToolCallId,
                        tc.FunctionName,
                        BinaryData.FromBytes(tc.ArgumentBytes.ToArray())))
                    .ToList();

                await ProcessToolCallsAsync(prompts, toolCalls, functions);

                // Clear accumulated tool calls for the next potential round.
                accumulatedToolCalls.Clear();

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

    protected override async Task<AIDeployment> GetDeploymentAsync(AICompletionContext content)
    {
        if (!string.IsNullOrEmpty(content.DeploymentId))
        {
            return await _deploymentStore.FindByIdAsync(content.DeploymentId);
        }

        return null;
    }

    private async Task ProcessToolCallsAsync(List<ChatMessage> prompts, IEnumerable<ChatToolCall> tollCalls, IEnumerable<Microsoft.Extensions.AI.AIFunction> functions)
    {
        if (tollCalls is null || !tollCalls.Any())
        {
            return;
        }

        prompts.Add(ChatMessage.CreateAssistantMessage(tollCalls));

        foreach (var toolCall in tollCalls)
        {
            var function = functions.FirstOrDefault(x => x.Name == toolCall.FunctionName);

            if (function is null)
            {
                continue;
            }

            var arguments = toolCall.FunctionArguments.ToObjectFromJson<Microsoft.Extensions.AI.AIFunctionArguments>();

            arguments.Services = _serviceProvider;

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

    private AzureOpenAIClient GetChatClient(AIProviderConnectionEntry connection)
    {
        _clientOptions ??= new AzureOpenAIClientOptions()
        {
            ClientLoggingOptions = new ClientLoggingOptions()
            {
                EnableLogging = connection.GetBooleanOrFalseValue("EnableLogging"),
                EnableMessageContentLogging = connection.GetBooleanOrFalseValue("EnableMessageContentLogging"),
                EnableMessageLogging = connection.GetBooleanOrFalseValue("EnableMessageLogging"),
                LoggerFactory = _loggerFactory,
            },
        };

        var endpoint = connection.GetEndpoint();

        var azureClient = connection.GetAzureAuthenticationType() switch
        {
            AzureAuthenticationType.ApiKey => new AzureOpenAIClient(endpoint, new ApiKeyCredential(connection.GetApiKey()), _clientOptions),
            AzureAuthenticationType.ManagedIdentity => new AzureOpenAIClient(endpoint, new ManagedIdentityCredential(), _clientOptions),
            AzureAuthenticationType.Default => new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), _clientOptions),
            _ => throw new NotSupportedException("The specified authentication type is not supported.")
        };

        return azureClient;
    }

    private async Task<ChatCompletionOptions> GetOptionsWithDataSourceAsync(AICompletionContext context, IEnumerable<Microsoft.Extensions.AI.AIFunction> functions)
    {
        var chatOptions = GetOptions(context, functions);

        if (!string.IsNullOrEmpty(context.DataSourceId) && !string.IsNullOrEmpty(context.DataSourceType))
        {
            var dataSourceContext = new AzureOpenAIDataSourceContext(context.DataSourceId, context.DataSourceType)
            {
                Strictness = context.AdditionalProperties.TryGetValue("Strictness", out var strictnessObj) && strictnessObj is int strictness ? strictness : null,
                TopNDocuments = context.AdditionalProperties.TryGetValue("TopNDocuments", out var topNDocumentsObj) && topNDocumentsObj is int topNDocuments ? topNDocuments : null,
                Filter = context.AdditionalProperties.TryGetValue("Filter", out var filterObj) && filterObj is string filter ? filter : null,
                IsInScope = context.AdditionalProperties.TryGetValue("IsInScope", out var isInScopeObj) && isInScopeObj is bool isInScope ? isInScope : (bool?)null,
            };

            foreach (var handler in _azureOpenAIDataSourceHandlers)
            {
                await handler.ConfigureSourceAsync(chatOptions, dataSourceContext);
            }
        }

        return chatOptions;
    }

    private async ValueTask ConfigureOptionsAsync(ChatCompletionOptions chatOptions, AICompletionContext context, List<ChatMessage> prompts)
    {
        var optionsContext = new AzureOpenAIChatOptionsContext(chatOptions, context, prompts);

        foreach (var handler in _azureOpenAIDataSourceHandlers)
        {
            await handler.ConfigureOptionsAsync(optionsContext);
        }
    }

    private static ChatCompletionOptions GetOptions(AICompletionContext context, IEnumerable<Microsoft.Extensions.AI.AIFunction> functions)
    {
        var chatOptions = new ChatCompletionOptions()
        {
            Temperature = context.Temperature,
            TopP = context.TopP,
            FrequencyPenalty = context.FrequencyPenalty,
            PresencePenalty = context.PresencePenalty,
            MaxOutputTokenCount = context.MaxTokens,
        };

        if (!context.DisableTools)
        {
            foreach (var function in functions)
            {
                chatOptions.Tools.Add(function.ToChatTool());
            }

            if (chatOptions.Tools.Count > 0)
            {
                chatOptions.ToolChoice = ChatToolChoice.CreateAutoChoice();
            }
        }

        return chatOptions;
    }

    private async Task<IEnumerable<Microsoft.Extensions.AI.AIFunction>> GetFunctionsAsync(string[] toolNames, string[] instanceIds)
    {
        var totalToolNames = toolNames?.Length ?? 0;
        var totalInstanceIds = instanceIds?.Length ?? 0;

        if (totalToolNames == 0 && totalInstanceIds == 0)
        {
            return [];
        }

        _toolsService ??= _serviceProvider.GetService<IAIToolsService>();

        if (_toolsService is null)
        {
            return [];
        }

        var functions = new List<Microsoft.Extensions.AI.AIFunction>();

        if (totalToolNames > 0)
        {
            foreach (var name in toolNames)
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

        if (totalInstanceIds > 0)
        {
            foreach (var instanceId in instanceIds)
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

        return functions;
    }

    private static List<ChatMessage> GetPrompts(AICompletionContext context, List<ChatMessage> azureMessages)
    {
        var prompts = new List<ChatMessage>();

        var systemMessage = GetSystemMessage(context);

        if (!string.IsNullOrEmpty(systemMessage))
        {
            prompts.Add(new SystemChatMessage(systemMessage));
        }

        if (context.PastMessagesCount > 1)
        {
            var skip = GetTotalMessagesToSkip(azureMessages.Count, context.PastMessagesCount.Value);

            prompts.AddRange(azureMessages.Skip(skip).Take(context.PastMessagesCount.Value));
        }
        else
        {
            prompts.AddRange(azureMessages);
        }

        return prompts;
    }

    private async Task<(AIProviderConnectionEntry, string)> GetConnectionAsync(AICompletionContext context, string providerName)
    {
        string deploymentName = null;

        if (ProviderOptions.Providers.TryGetValue(providerName, out var provider))
        {
            var connectionName = GetDefaultConnectionName(provider, context.ConnectionName);

            deploymentName = GetDefaultDeploymentName(provider, connectionName);

            var deployment = await GetDeploymentAsync(context);

            if (deployment is not null)
            {
                connectionName = deployment.ConnectionName;
                deploymentName = deployment.Name;
            }

            if (!string.IsNullOrEmpty(connectionName) && provider.Connections.TryGetValue(connectionName, out var connectionProperties))
            {
                return new(connectionProperties, deploymentName);
            }
        }

        return new(null, deploymentName);
    }
}
