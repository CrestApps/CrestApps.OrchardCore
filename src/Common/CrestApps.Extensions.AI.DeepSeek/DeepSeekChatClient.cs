using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Extensions.AI.DeepSeek.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.Extensions.AI.DeepSeek;

public sealed class DeepSeekChatClient : IChatClient
{
    private static readonly Uri _baseUri = new("https://api.deepseek.com");
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string _defaultModelName = "deepseek-chat";
    private const string _completionEndpoint = "chat/completions";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public ChatClientMetadata Metadata { get; }

    public DeepSeekChatClient(IHttpClientFactory httpClientFactory, string modelName = null, ILogger logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _httpClientFactory = httpClientFactory;
        _logger = logger;
        Metadata = new ChatClientMetadata("DeepSeek", _baseUri, string.IsNullOrEmpty(modelName) ? _defaultModelName : modelName);
    }

    public async Task<ChatCompletion> CompleteAsync(IList<ChatMessage> chatMessages, ChatOptions options = null, CancellationToken cancellationToken = default)
    {
        var request = GetRequest(chatMessages, options, out var httpClient);

        var response = await httpClient.PostAsJsonAsync(_completionEndpoint, request, _jsonSerializerOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (_logger is not null)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogError("Request failed with status code {StatusCode}: {Body}", response.StatusCode, body);
            }

            return new ChatCompletion(new ChatMessage(ChatRole.Assistant, content: null));
        }

        var data = await response.Content.ReadFromJsonAsync<DeepSeekResponse>(_jsonSerializerOptions, cancellationToken);

        var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(data.Created);

        if (data?.Choices is not null && data.Choices.Count > 0)
        {
            if (data.Choices[0].FinishReason == "tool_calls")
            {
                if (options.ToolMode == ChatToolMode.Auto && options?.Tools is not null)
                {
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
                        response = await httpClient.PostAsJsonAsync(_completionEndpoint, request, _jsonSerializerOptions, cancellationToken);

                        if (response.IsSuccessStatusCode)
                        {
                            data = await response.Content.ReadFromJsonAsync<DeepSeekResponse>(_jsonSerializerOptions, cancellationToken);
                        }
                        else if (_logger is not null)
                        {
                            var body = await response.Content.ReadAsStringAsync(cancellationToken);

                            _logger.LogError("Request failed with status code {StatusCode}: {Body}", response.StatusCode, body);
                        }
                    }
                }
            }

            if (data.Choices.Count > 0 && data.Choices[0].FinishReason == "stop")
            {
                return new ChatCompletion(data.Choices.Select(x => GetMessage(x.Message)).ToArray())
                {
                    FinishReason = new ChatFinishReason(data.Choices[0].FinishReason),
                    CompletionId = data.Id,
                    ModelId = data.Model,
                    CreatedAt = dateTimeOffset.UtcDateTime,
                    Usage = new UsageDetails
                    {
                        InputTokenCount = data.Usage?.PromptTokens,
                        OutputTokenCount = data.Usage?.CompletionTokens,
                        TotalTokenCount = data.Usage?.TotalTokens,
                    },
                };
            }

            return new ChatCompletion(new ChatMessage(ChatRole.Assistant, content: null))
            {
                FinishReason = new ChatFinishReason(data.Choices[0].FinishReason),
                CompletionId = data.Id,
                ModelId = data.Model,
                CreatedAt = dateTimeOffset.UtcDateTime,
                Usage = new UsageDetails
                {
                    InputTokenCount = data.Usage?.PromptTokens,
                    OutputTokenCount = data.Usage?.CompletionTokens,
                    TotalTokenCount = data.Usage?.TotalTokens,
                },
            };
        }

        return new ChatCompletion(new ChatMessage(ChatRole.Assistant, content: null))
        {
            FinishReason = ChatFinishReason.Stop,
            CompletionId = data.Id,
            ModelId = data.Model,
            CreatedAt = dateTimeOffset.UtcDateTime,
            Usage = new UsageDetails
            {
                InputTokenCount = data.Usage?.PromptTokens,
                OutputTokenCount = data.Usage?.CompletionTokens,
                TotalTokenCount = data.Usage?.TotalTokens,
            },
        };
    }

    public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = GetRequest(chatMessages, options, out var httpClient);
        request.Stream = true;

        using var response = await httpClient.PostAsJsonAsync(_completionEndpoint, request, _jsonSerializerOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger?.LogError("Request failed with status code {StatusCode}: {Body}", response.StatusCode, body);

            throw new HttpRequestException($"Request failed with status code {response.StatusCode}: {body}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data:"))
            {
                continue;
            }

            var json = line.Substring("data:".Length).Trim();

            if (json == "[DONE]")
            {
                yield break;
            }

            var update = JsonSerializer.Deserialize<DeepSeekStreamingResponse>(json, _jsonSerializerOptions);

            if (update?.Choices != null && update.Choices.Count > 0)
            {
                foreach (var choice in update.Choices)
                {
                    yield return new StreamingChatCompletionUpdate
                    {
                        CompletionId = update.Id,
                        ModelId = update.Model,
                        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(update.Created).UtcDateTime,
                        Role = string.IsNullOrEmpty(choice.Delta?.Role) ? ChatRole.Assistant : new ChatRole(choice.Delta?.Role),
                        Text = choice.Delta?.Content,
                        FinishReason = string.IsNullOrEmpty(choice.FinishReason) ? null : new ChatFinishReason(choice.FinishReason),
                    };
                }
            }
        }
    }

    public void Dispose()
    {
        // Nothing to dispose. Implementation required for the IChatClient interface.
    }

    public object GetService(Type serviceType, object serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return
            serviceKey is not null
            ? null
            : serviceType.IsInstanceOfType(this)
                ? this
                : null;
    }

    private DeepSeekRequest GetRequest(IList<ChatMessage> chatMessages, ChatOptions options, out HttpClient httpClient)
    {
        httpClient = _httpClientFactory.CreateClient("DeepSeek");

        httpClient.BaseAddress = _baseUri;

        var request = new DeepSeekRequest()
        {
            Model = Metadata.ModelId,
        };

        if (options is not null)
        {
            if (options?.AdditionalProperties is not null && options.AdditionalProperties.TryGetValue("ApiKey", out var apiKey))
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
                request.Messages.Add(new DeepSeekMessage()
                {
                    Role = ChatRole.Tool.Value,
                    ToolCallId = toolCall.Id,
                    Content = str,
                });
            }
            else
            {
                var resultJson = JsonSerializer.Serialize(result);

                request.Messages.Add(new DeepSeekMessage()
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
