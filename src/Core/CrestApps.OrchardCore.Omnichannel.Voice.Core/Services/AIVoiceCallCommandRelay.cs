using System.Collections.Concurrent;
using System.Text.Json;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Relays commands for live AI calls between the nodes of a tenant over an <see cref="IAIVoiceCallMessageBus"/>, as
/// a request that the node holding the call answers.
/// </summary>
/// <remarks>
/// Every node listens on one request channel and on a reply channel of its own. A request names the activity, the
/// command and the node to answer. A node with no call for the activity stays silent, so the sender waits for the
/// one node that has it, or gives up after <see cref="ReplyTimeout"/>. A bus that fails is logged and reported as
/// "not carried out", so the caller falls back to what the node can do alone.
/// </remarks>
public sealed class AIVoiceCallCommandRelay : IAIVoiceCallCommandRelay, IDisposable
{
    /// <summary>
    /// The channel every node listens on for requests.
    /// </summary>
    public const string RequestChannel = "CrestApps:AIVoiceCallCommands:Requests";

    /// <summary>
    /// The prefix of each node's reply channel.
    /// </summary>
    public const string ReplyChannelPrefix = "CrestApps:AIVoiceCallCommands:Replies:";

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAIVoiceCallMessageBus _bus;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private Func<string, AIVoiceCallCommand, Task<bool?>> _deliverLocally;
    private bool _started;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceCallCommandRelay"/> class.
    /// </summary>
    /// <param name="bus">The bus shared by the tenant's nodes.</param>
    /// <param name="logger">The logger.</param>
    public AIVoiceCallCommandRelay(IAIVoiceCallMessageBus bus, ILogger<AIVoiceCallCommandRelay> logger)
    {
        _bus = bus;
        _logger = logger;
        NodeId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Gets this node's identity on the bus.
    /// </summary>
    public string NodeId { get; }

    /// <summary>
    /// Gets or sets how long a sender waits for the node holding the call.
    /// </summary>
    public TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <inheritdoc/>
    public async Task StartAsync(Func<string, AIVoiceCallCommand, Task<bool?>> deliverLocally)
    {
        ArgumentNullException.ThrowIfNull(deliverLocally);

        if (_started)
        {
            return;
        }

        await _startLock.WaitAsync();

        try
        {
            if (_started)
            {
                return;
            }

            _deliverLocally = deliverLocally;
            await _bus.SubscribeAsync(RequestChannel, OnRequestAsync);
            await _bus.SubscribeAsync(ReplyChannelPrefix + NodeId, OnReplyAsync);
            _started = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The AI call command relay could not listen; commands reach only the calls held by this node.");
        }
        finally
        {
            _startLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SendAsync(string activityId, AIVoiceCallCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityId) || command is null || !_started)
        {
            return false;
        }

        var requestId = Guid.NewGuid().ToString("N");
        var reply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = reply;

        try
        {
            var request = new RelayRequest(requestId, NodeId, activityId, command.Kind, command.Text);
            await _bus.PublishAsync(RequestChannel, JsonSerializer.Serialize(request, _serializerOptions));

            return await reply.Task.WaitAsync(ReplyTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "No node answered for AI call activity '{ActivityId}' within {Seconds} seconds; the command '{Kind}' was not carried out.",
                    activityId.SanitizeLogValue(),
                    ReplyTimeout.TotalSeconds,
                    command.Kind);
            }

            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The command for AI call activity '{ActivityId}' could not be relayed to the other nodes.", activityId.SanitizeLogValue());

            return false;
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
        => _startLock.Dispose();

    private async Task OnRequestAsync(string message)
    {
        RelayRequest request;

        try
        {
            request = JsonSerializer.Deserialize<RelayRequest>(message, _serializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "An AI call command on the relay could not be read and was ignored.");

            return;
        }

        // This node already tried its own calls before relaying.
        if (request is null || string.IsNullOrEmpty(request.RequestId) || string.Equals(request.ReplyTo, NodeId, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var handled = await _deliverLocally(request.ActivityId, new AIVoiceCallCommand(request.Kind, request.Text));

            // Only the node holding the call answers; the others would only race it with a "no".
            if (handled is null)
            {
                return;
            }

            await _bus.PublishAsync(ReplyChannelPrefix + request.ReplyTo, JsonSerializer.Serialize(new RelayReply(request.RequestId, handled.Value), _serializerOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A relayed command for AI call activity '{ActivityId}' failed.", request.ActivityId.SanitizeLogValue());
        }
    }

    private Task OnReplyAsync(string message)
    {
        try
        {
            var reply = JsonSerializer.Deserialize<RelayReply>(message, _serializerOptions);

            if (reply is not null && !string.IsNullOrEmpty(reply.RequestId) && _pending.TryGetValue(reply.RequestId, out var pending))
            {
                pending.TrySetResult(reply.Handled);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "A reply on the AI call command relay could not be read and was ignored.");
        }

        return Task.CompletedTask;
    }

    private sealed record RelayRequest(string RequestId, string ReplyTo, string ActivityId, AIVoiceCallCommandKind Kind, string Text);

    private sealed record RelayReply(string RequestId, bool Handled);
}
