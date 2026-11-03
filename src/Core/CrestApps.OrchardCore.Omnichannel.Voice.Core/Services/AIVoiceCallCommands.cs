using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The <see cref="IAIVoiceCallCommands"/> registry: commands reach the calls held by this node, and, through an
/// <see cref="IAIVoiceCallCommandRelay"/> when one is registered, the calls held by the tenant's other nodes.
/// </summary>
public sealed class AIVoiceCallCommands : IAIVoiceCallCommands
{
    private readonly ConcurrentDictionary<string, ImmutableHandlers> _handlers = new(StringComparer.Ordinal);
    private readonly IAIVoiceCallCommandRelay _relay;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceCallCommands"/> class that reaches this node's calls only.
    /// </summary>
    public AIVoiceCallCommands()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceCallCommands"/> class.
    /// </summary>
    /// <param name="relays">The relay to the tenant's other nodes, when one is registered.</param>
    public AIVoiceCallCommands(IEnumerable<IAIVoiceCallCommandRelay> relays)
    {
        _relay = relays?.FirstOrDefault();
    }

    /// <inheritdoc/>
    public IDisposable Register(string activityId, Func<AIVoiceCallCommand, Task<bool>> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityId);
        ArgumentNullException.ThrowIfNull(handler);

        _handlers.AddOrUpdate(activityId, _ => new ImmutableHandlers([handler]), (_, existing) => existing.Add(handler));

        // A node holding a call must hear the commands other nodes relay for it.
        _ = _relay?.StartAsync(DeliverLocallyAsync);

        return new Registration(this, activityId, handler);
    }

    /// <inheritdoc/>
    public async Task<bool> SendAsync(string activityId, AIVoiceCallCommand command)
        => await DeliverLocallyAsync(activityId, command) == true;

    /// <inheritdoc/>
    public async Task<bool> RelayAsync(string activityId, AIVoiceCallCommand command)
    {
        if (string.IsNullOrEmpty(activityId) || command is null)
        {
            return false;
        }

        var handled = await DeliverLocallyAsync(activityId, command);

        if (handled is not null || _relay is null)
        {
            return handled == true;
        }

        // The call is not held here: offer the command to the node that holds it.
        await _relay.StartAsync(DeliverLocallyAsync);

        return await _relay.SendAsync(activityId, command);
    }

    private async Task<bool?> DeliverLocallyAsync(string activityId, AIVoiceCallCommand command)
    {
        if (string.IsNullOrEmpty(activityId) || command is null || !_handlers.TryGetValue(activityId, out var handlers))
        {
            return null;
        }

        var handled = false;

        foreach (var handler in handlers.Items)
        {
            handled |= await handler(command);
        }

        return handled;
    }

    private void Remove(string activityId, Func<AIVoiceCallCommand, Task<bool>> handler)
    {
        while (_handlers.TryGetValue(activityId, out var existing))
        {
            var remaining = existing.Remove(handler);

            if (remaining.Items.Count == 0)
            {
                if (((ICollection<KeyValuePair<string, ImmutableHandlers>>)_handlers).Remove(new KeyValuePair<string, ImmutableHandlers>(activityId, existing)))
                {
                    return;
                }
            }
            else if (_handlers.TryUpdate(activityId, remaining, existing))
            {
                return;
            }
        }
    }

    private sealed record ImmutableHandlers(IReadOnlyList<Func<AIVoiceCallCommand, Task<bool>>> Items)
    {
        public ImmutableHandlers Add(Func<AIVoiceCallCommand, Task<bool>> handler)
            => new([.. Items, handler]);

        public ImmutableHandlers Remove(Func<AIVoiceCallCommand, Task<bool>> handler)
            => new(Items.Where(item => item != handler).ToList());
    }

    private sealed class Registration : IDisposable
    {
        private readonly AIVoiceCallCommands _owner;
        private readonly string _activityId;
        private readonly Func<AIVoiceCallCommand, Task<bool>> _handler;
        private int _disposed;

        public Registration(AIVoiceCallCommands owner, string activityId, Func<AIVoiceCallCommand, Task<bool>> handler)
        {
            _owner = owner;
            _activityId = activityId;
            _handler = handler;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.Remove(_activityId, _handler);
            }
        }
    }
}
