using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Providers.CustomLlm;

/// <summary>
/// A chat client that sends each turn to your own server over a WebSocket and reads the reply as it streams back, for
/// a model or an agent you host that does not speak an OpenAI-compatible API.
/// </summary>
/// <remarks>
/// Each request opens a socket, sends one <c>request</c> message, and reads <c>delta</c> messages until <c>done</c>
/// (or <c>error</c>). The messages are JSON text frames:
/// <code>
/// → {"type":"request","model":"my-agent","conversationId":"...","messages":[{"role":"system","content":"..."},{"role":"user","content":"..."}]}
/// ← {"type":"delta","content":"Sure, "}
/// ← {"type":"delta","content":"your order ships today."}
/// ← {"type":"done","usage":{"inputTokens":120,"outputTokens":9}}
/// </code>
/// </remarks>
public sealed class CustomLlmChatClient : IChatClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Func<CancellationToken, Task<WebSocket>> _connect;
    private readonly string _model;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomLlmChatClient"/> class.
    /// </summary>
    /// <param name="connect">Opens a socket to the server.</param>
    /// <param name="model">The model name sent with each request.</param>
    public CustomLlmChatClient(Func<CancellationToken, Task<WebSocket>> connect, string model)
    {
        ArgumentNullException.ThrowIfNull(connect);

        _connect = connect;
        _model = model;
    }

    /// <summary>
    /// Gets or sets how long to wait for the server, between messages, before giving up.
    /// </summary>
    public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions options = null, CancellationToken cancellationToken = default)
    {
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updates.Add(update);
        }

        return updates.ToChatResponse();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        using var socket = await _connect(cancellationToken);
        var request = new CustomLlmRequest
        {
            Model = options?.ModelId ?? _model,
            ConversationId = options?.ConversationId,
            Messages = messages
                .Where(message => !string.IsNullOrEmpty(message.Text))
                .Select(message => new CustomLlmMessage { Role = message.Role.Value, Content = message.Text })
                .ToList(),
            Temperature = options?.Temperature,
            MaxOutputTokens = options?.MaxOutputTokens,
        };

        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(request, _jsonOptions), WebSocketMessageType.Text, true, cancellationToken);

        var responseId = Guid.NewGuid().ToString("n");

        while (true)
        {
            var reply = await ReceiveAsync(socket, cancellationToken);

            if (reply is null)
            {
                throw new InvalidOperationException("The custom LLM server closed the connection before it finished its reply.");
            }

            switch (reply.Type)
            {
                case "delta" when !string.IsNullOrEmpty(reply.Content):
                    yield return new ChatResponseUpdate(ChatRole.Assistant, reply.Content) { ResponseId = responseId, ModelId = request.Model };
                    break;

                case "done":
                    if (reply.Usage is { } usage)
                    {
                        yield return new ChatResponseUpdate
                        {
                            Role = ChatRole.Assistant,
                            ResponseId = responseId,
                            FinishReason = ChatFinishReason.Stop,
                            Contents = [new UsageContent(new UsageDetails { InputTokenCount = usage.InputTokens, OutputTokenCount = usage.OutputTokens, TotalTokenCount = usage.InputTokens + usage.OutputTokens })],
                        };
                    }

                    await CloseQuietlyAsync(socket);

                    yield break;

                case "error":
                    await CloseQuietlyAsync(socket);

                    throw new InvalidOperationException($"The custom LLM server answered with an error: {reply.Message}");
            }
        }
    }

    /// <inheritdoc/>
    public object GetService(Type serviceType, object serviceKey = null)
        => serviceType == typeof(ChatClientMetadata)
            ? new ChatClientMetadata(nameof(CustomLlmChatClient), defaultModelId: _model)
            : serviceType?.IsInstanceOfType(this) == true ? this : null;

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private async Task<CustomLlmReply> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReplyTimeout);

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        while (true)
        {
            WebSocketReceiveResult result;

            try
            {
                result = await socket.ReceiveAsync(chunk, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The custom LLM server sent nothing for {ReplyTimeout.TotalSeconds:0} seconds.");
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            buffer.Write(chunk, 0, result.Count);

            if (result.EndOfMessage)
            {
                return result.MessageType == WebSocketMessageType.Text
                    ? JsonSerializer.Deserialize<CustomLlmReply>(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length), _jsonOptions)
                    : new CustomLlmReply();
            }
        }
    }

    private static async Task CloseQuietlyAsync(WebSocket socket)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
                // The reply is complete; a socket that will not close cleanly changes nothing.
            }
        }
    }

    private sealed class CustomLlmRequest
    {
        public string Type { get; set; } = "request";

        public string Model { get; set; }

        public string ConversationId { get; set; }

        public List<CustomLlmMessage> Messages { get; set; } = [];

        public float? Temperature { get; set; }

        public int? MaxOutputTokens { get; set; }
    }

    private sealed class CustomLlmMessage
    {
        public string Role { get; set; }

        public string Content { get; set; }
    }

    private sealed class CustomLlmReply
    {
        public string Type { get; set; }

        public string Content { get; set; }

        public string Message { get; set; }

        public CustomLlmUsage Usage { get; set; }
    }

    private sealed class CustomLlmUsage
    {
        public long InputTokens { get; set; }

        public long OutputTokens { get; set; }
    }
}
