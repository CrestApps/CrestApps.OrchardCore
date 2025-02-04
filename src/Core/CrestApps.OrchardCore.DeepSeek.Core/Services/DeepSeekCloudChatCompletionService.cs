using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.DeepSeek.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekCloudChatCompletionService : IAIChatCompletionService
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IAIToolsService _toolsService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DefaultDeepSeekOptions _defaultOptions;
    private readonly AIProviderOptions _providerOptions;
    private readonly ILogger _logger;

    public DeepSeekCloudChatCompletionService(
        IOptions<AIProviderOptions> providerOptions,
        IAIToolsService toolsService,
        IOptions<DefaultDeepSeekOptions> defaultOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<DeepSeekCloudChatCompletionService> logger)
    {
        _toolsService = toolsService;
        _httpClientFactory = httpClientFactory;
        _defaultOptions = defaultOptions.Value;
        _providerOptions = providerOptions.Value;
        _logger = logger;
    }

    public string Name
        => DeepSeekCloudChatProfileSource.Key;

    public async Task<AIChatCompletionResponse> ChatAsync(IEnumerable<ChatMessage> messages, AIChatCompletionContext context)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(context);

        AIProviderConnection connection = null;

        if (_providerOptions.Providers.TryGetValue(DeepSeekConstants.DeepSeekProviderName, out var entry))
        {
            var connectionName = "deepseek-cloud";

            if (!string.IsNullOrEmpty(connectionName) && entry.Connections.TryGetValue(connectionName, out var connectionProperties))
            {
                connection = connectionProperties;
            }
        }

        if (connection is null)
        {
            _logger.LogWarning("Unable to chat. Unable to find the deployment associated with the profile with id '{ProfileId}' or a default DefaultDeploymentName.", context.Profile.Id);

            return AIChatCompletionResponse.Empty;
        }

        var metadata = context.Profile.As<DeepSeekChatProfileMetadata>();

        var request = new DeepSeekRequest()
        {
            Temperature = metadata.Temperature ?? _defaultOptions.Temperature,
            TopP = metadata.TopP ?? _defaultOptions.TopP,
            FrequencyPenalty = metadata.FrequencyPenalty ?? _defaultOptions.FrequencyPenalty,
            PresencePenalty = metadata.PresencePenalty ?? _defaultOptions.PresencePenalty,
            MaxTokens = metadata.MaxTokens ?? _defaultOptions.MaxOutputTokens,
        };

        var modelName = connection.GetDefaultDeploymentName();

        if (string.IsNullOrEmpty(modelName))
        {
            modelName = "deepseek-reasoner";
        }

        request.Model = modelName;

        if (!context.DisableTools && context.Profile.FunctionNames is not null)
        {
            request.Tools = [];

            foreach (var functionName in context.Profile.FunctionNames)
            {
                var function = _toolsService.GetFunction(functionName);

                if (function is null)
                {
                    continue;
                }

                request.Tools.Add(function.ToChatTool());
            }
        }

        request.Messages =
        [
            new()
            {
                Role = ChatRole.System.Value,
                Content = GetSystemMessage(context, metadata),
            },
        ];

        var pastMessageCount = metadata.PastMessagesCount ?? _defaultOptions.PastMessagesCount;

        var chatMessages = messages.Where(x => (x.Role == ChatRole.User || x.Role == ChatRole.Assistant) && !string.IsNullOrWhiteSpace(x.Text)).ToArray();

        var skip = GetTotalMessagesToSkip(chatMessages.Length, pastMessageCount);

        request.Messages.AddRange(chatMessages.Skip(skip).Take(pastMessageCount).Select(x => new DeepSeekMessage
        {
            Role = x.Role.Value,
            Content = x.Text,
        }));

        try
        {
            var httpClient = _httpClientFactory.CreateClient(DeepSeekConstants.DeepSeekProviderName);

            httpClient.BaseAddress = new Uri(connection.GetApiUrl());
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connection.GetApiKey());

            var response = await httpClient.PostAsJsonAsync("v1/completions", request);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<DeepSeekResponse>(_jsonSerializerOptions);

            if (data?.Choices is not null && data.Choices.Count > 0)
            {
                if (data.Choices[0].FinishReason == "tool_calls")
                {
                    if (await ProcessToolCallsAsync(request, data.Choices[0]))
                    {
                        response = await httpClient.PostAsJsonAsync("v1/completions", request);
                        response.EnsureSuccessStatusCode();

                        data = await response.Content.ReadFromJsonAsync<DeepSeekResponse>(_jsonSerializerOptions);
                    }
                }

                if (data.Choices.Count > 0 && data.Choices[0].FinishReason == "stop")
                {
                    return new AIChatCompletionResponse
                    {
                        Choices = data.Choices.Select(x => new AIChatCompletionChoice()
                        {
                            Content = x.Message.Content,
                        }),
                    };
                }

                return AIChatCompletionResponse.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while chatting with the DeepSeek service.");
        }

        return AIChatCompletionResponse.Empty;
    }

    private static int GetTotalMessagesToSkip(int totalMessages, int pastMessageCount)
    {
        if (pastMessageCount > 0 && totalMessages > pastMessageCount)
        {
            return totalMessages - pastMessageCount;
        }

        return 0;
    }

    private static string GetSystemMessage(AIChatCompletionContext context, DeepSeekChatProfileMetadata metadata)
    {
        var systemMessage = metadata.SystemMessage ?? string.Empty;

        if (context.UserMarkdownInResponse)
        {
            systemMessage += Environment.NewLine + AIConstants.SystemMessages.UseMarkdownSyntax;
        }

        return systemMessage;
    }

    private async Task<bool> ProcessToolCallsAsync(DeepSeekRequest request, DeepSeekChoice choice)
    {
        if (choice.Message.ToolCalls is not null && choice.Message.ToolCalls.Length == 0)
        {
            return false;
        }

        request.Messages.Add(new DeepSeekMessage()
        {
            Role = ChatRole.Assistant.Value,
            ToolCalls = choice.Message.ToolCalls,
        });

        foreach (var toolCall in choice.Message.ToolCalls)
        {
            if (string.IsNullOrEmpty(toolCall.Function?.Name))
            {
                continue;
            }

            var function = _toolsService.GetFunction(toolCall.Function.Name);

            if (function is null)
            {
                continue;
            }

            var arguments = string.IsNullOrEmpty(toolCall.Function.Arguments)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.Function.Arguments);

            var result = await function.InvokeAsync(arguments);

            if (result is string str)
            {
                request.Messages.Add(new DeepSeekToolMessage()
                {
                    Role = ChatRole.Tool.Value,
                    ToolCallId = toolCall.Id,
                    Content = str,
                });
            }
            else
            {
                var resultJson = JsonSerializer.Serialize(result);

                request.Messages.Add(new DeepSeekToolMessage()
                {
                    Role = ChatRole.Tool.Value,
                    ToolCallId = toolCall.Id,
                    Content = resultJson,
                });
            }
        }

        return true;
    }
}
