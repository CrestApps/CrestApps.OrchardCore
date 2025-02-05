using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.DeepSeek.Core.Models;
using CrestApps.OrchardCore.OpenAI.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.DeepSeek.Core.Services;

public sealed class DeepSeekChatClient : IChatClient
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string _defaultModelName = "deepseek-reasoner";

    private static readonly Uri _baseUri = new("https://api.deepseek.com");

    private readonly IHttpClientFactory _httpClientFactory;

    public ChatClientMetadata Metadata { get; }

    public DeepSeekChatClient(IHttpClientFactory httpClientFactory, string modelName = null)
    {
        Metadata = new ChatClientMetadata(DeepSeekConstants.DeepSeekProviderName, _baseUri, string.IsNullOrEmpty(modelName) ? _defaultModelName : modelName);
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ChatCompletion> CompleteAsync(IList<ChatMessage> chatMessages, ChatOptions options = null, CancellationToken cancellationToken = default)
    {
        var request = GetRequest(chatMessages, options, out var httpClient);

        var response = await httpClient.PostAsJsonAsync("v1/completions", request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<DeepSeekResponse>(_jsonSerializerOptions, cancellationToken);

        if (data?.Choices is not null && data.Choices.Count > 0)
        {
            var forceStop = false;

            if (data.Choices[0].FinishReason == "tool_calls")
            {
                if (options.ToolMode == ChatToolMode.Auto && options?.Tools is not null)
                {
                    forceStop = true;

                    var functions = new List<AIFunction>();

                    foreach (var tool in options.Tools)
                    {
                        if (tool is not AIFunction function)
                        {
                            continue;
                        }

                        functions.Add(function);
                    }

                    if (await ProcessToolCallsAsync(request, data.Choices[0], functions))
                    {
                        response = await httpClient.PostAsJsonAsync("v1/completions", request, cancellationToken);
                        response.EnsureSuccessStatusCode();
                        data = await response.Content.ReadFromJsonAsync<DeepSeekResponse>(_jsonSerializerOptions, cancellationToken);
                    }
                }
                else
                {
                    forceStop = true;
                }
            }

            if (data.Choices.Count > 0 && (data.Choices[0].FinishReason != "tool_calls" || forceStop))
            {
                return new ChatCompletion(data.Choices.Select(x => GetMessage(x.Message)).ToArray());
            }
        }

        return new ChatCompletion(new ChatMessage(ChatRole.Assistant, "AI model drew blank!"));
    }

    private DeepSeekRequest GetRequest(IList<ChatMessage> chatMessages, ChatOptions options, out HttpClient httpClient)
    {
        httpClient = _httpClientFactory.CreateClient(DeepSeekConstants.DeepSeekProviderName);

        httpClient.BaseAddress = _baseUri;
        var request = new DeepSeekRequest()
        {
            Model = Metadata.ModelId,
        };

        if (options is not null)
        {
            if (options?.AdditionalProperties is not null && options.AdditionalProperties.TryGetValue("apiKey", out var apiKey))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.ToString());
            }

            if (options.Tools is not null)
            {
                request.Tools = [];

                foreach (var tool in options.Tools)
                {
                    if (tool is not AIFunction func)
                    {
                        continue;
                    }

                    request.Tools.Add(func.ToChatTool());
                }
            }

            request.Temperature = options.Temperature;
            request.TopP = options.TopP;
            request.FrequencyPenalty = options.FrequencyPenalty;
            request.PresencePenalty = options.PresencePenalty;
            request.MaxTokens = options.MaxOutputTokens;
        }

        if (string.IsNullOrEmpty(request.Model))
        {
            request.Model = _defaultModelName;
        }

        request.Messages = chatMessages.Select(message => new DeepSeekMessage
        {
            Role = message.Role.Value,
            Content = message.Text,
        }).ToList();
        return request;
    }

    public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(IList<ChatMessage> chatMessages, ChatOptions options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {

    }

    public object GetService(Type serviceType, object serviceKey = null)
    {
        throw new NotImplementedException();
    }

    private static ChatMessage GetMessage(DeepSeekMessage message)
    {
        var role = message.Role switch
        {
            "assistant" => ChatRole.Assistant,
            "user" => ChatRole.User,
            "system" => ChatRole.System,
            "tool" => ChatRole.Tool,
            _ => ChatRole.System,
        };

        return new ChatMessage(role, message.Content);
    }

    private static async Task<bool> ProcessToolCallsAsync(DeepSeekRequest request, DeepSeekChoice choice, IList<AIFunction> tools)
    {
        if (tools == null || choice.Message.ToolCalls is not null && choice.Message.ToolCalls.Length == 0)
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

            var function = tools.FirstOrDefault(x => x.Metadata.Name == toolCall.Function.Name);

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
