using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The in-memory <see cref="IAutomatedConversationGate"/>. A single node owns each conversation's inbound
/// processing, so an in-process registry keyed by session is sufficient; registered as a singleton inside the
/// tenant container, it is shared by the scoped handlers separate inbound webhooks create, and isolated from
/// every other tenant.
/// </summary>
public sealed class InMemoryAutomatedConversationGate : IAutomatedConversationGate
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeGenerations = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public IAutomatedConversationGeneration Begin(string sessionId, CancellationToken hostToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var source = CancellationTokenSource.CreateLinkedTokenSource(hostToken);

        _activeGenerations.AddOrUpdate(
            sessionId,
            source,
            (_, existing) =>
            {
                // A reply is already being composed for this conversation. The newer inbound message makes it
                // stale, so it is cancelled and replaced; the superseded turn disposes its own source.
                existing.Cancel();

                return source;
            });

        return new Generation(this, sessionId, source);
    }

    /// <inheritdoc/>
    public bool IsGenerating(string sessionId)
        => !string.IsNullOrEmpty(sessionId) && _activeGenerations.ContainsKey(sessionId);

    private void Release(string sessionId, CancellationTokenSource source)
    {
        // Only release the slot when this generation still owns it; a newer turn may already have taken it.
        _activeGenerations.TryRemove(new KeyValuePair<string, CancellationTokenSource>(sessionId, source));
    }

    private sealed class Generation : IAutomatedConversationGeneration
    {
        private readonly InMemoryAutomatedConversationGate _gate;
        private readonly string _sessionId;
        private readonly CancellationTokenSource _source;

        public Generation(InMemoryAutomatedConversationGate gate, string sessionId, CancellationTokenSource source)
        {
            _gate = gate;
            _sessionId = sessionId;
            _source = source;
        }

        public CancellationToken Token => _source.Token;

        public void Dispose()
        {
            _gate.Release(_sessionId, _source);
            _source.Dispose();
        }
    }
}
