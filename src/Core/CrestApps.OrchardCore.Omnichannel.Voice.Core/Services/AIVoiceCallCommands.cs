using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The in-process <see cref="IAIVoiceCallCommands"/>.
/// </summary>
public sealed class AIVoiceCallCommands : IAIVoiceCallCommands
{
    private readonly ConcurrentDictionary<string, ImmutableHandlers> _handlers = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public IDisposable Register(string activityId, Func<AIVoiceCallCommand, Task<bool>> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityId);
        ArgumentNullException.ThrowIfNull(handler);

        _handlers.AddOrUpdate(activityId, _ => new ImmutableHandlers([handler]), (_, existing) => existing.Add(handler));

        return new Registration(this, activityId, handler);
    }

    /// <inheritdoc/>
    public async Task<bool> SendAsync(string activityId, AIVoiceCallCommand command)
    {
        if (string.IsNullOrEmpty(activityId) || command is null || !_handlers.TryGetValue(activityId, out var handlers))
        {
            return false;
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
