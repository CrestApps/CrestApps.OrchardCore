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

    // Provider message ids already claimed for processing, with the time each was claimed so the map can be swept.
    // A provider redelivery arrives within seconds to minutes of the original, so an in-memory record that is kept
    // for a while and then evicted is enough to recognise one; nothing here needs to survive a restart.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _claimedInboundMessages = new(StringComparer.Ordinal);

    private static readonly TimeSpan _inboundClaimRetention = TimeSpan.FromMinutes(30);

    private long _lastSweepTicks;

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

    /// <inheritdoc/>
    public bool TryClaimInboundMessage(string providerMessageId)
    {
        // An unidentifiable message cannot be deduplicated; let it through rather than risk dropping a real reply.
        if (string.IsNullOrEmpty(providerMessageId))
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;

        SweepExpiredClaims(now);

        // TryAdd is atomic, so exactly one of two concurrent deliveries of the same id wins the claim.
        return _claimedInboundMessages.TryAdd(providerMessageId, now);
    }

    private void SweepExpiredClaims(DateTimeOffset now)
    {
        // Opportunistic, at most once a minute, so the claim map cannot grow without bound on a long-running node
        // while never adding a lock to the common claim path.
        var last = Interlocked.Read(ref _lastSweepTicks);

        if (now.UtcTicks - last < TimeSpan.FromMinutes(1).Ticks)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastSweepTicks, now.UtcTicks, last) != last)
        {
            return;
        }

        foreach (var entry in _claimedInboundMessages)
        {
            if (now - entry.Value > _inboundClaimRetention)
            {
                _claimedInboundMessages.TryRemove(entry.Key, out _);
            }
        }
    }

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
