using System.Collections.Concurrent;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// A process-wide, tenant-partitioned implementation of <see cref="IAsteriskPendingCallerTerminationRegistry"/>. It
/// backs both the termination-claim set and the pending-retry queue with process-wide static, tenant-qualified
/// concurrent sets, so a claim and its retry entry always share one lifecycle and neither can outlive the other.
/// The state is intentionally process-wide — like the per-channel create locks it coordinates with — because a
/// stranded-caller hang up is a remote ARI operation that can still be in flight across a shell reload: a per-shell
/// fence would be dropped while the old generation's hang up (or a new generation's routing of the same channel) is
/// still in progress. Keeping the claim process-wide keeps <see cref="IAsteriskChannelTenantBindingStore.CreateAsync"/>
/// fenced across overlapping shell generations until the termination actually completes, and keeping the pending
/// queue process-wide guarantees the reconciler in whichever generation is live can complete the hang up and release
/// the claim, so nothing is leaked across a reload. Entries are tenant-qualified so the shared state is multi-tenant
/// safe, and are removed once the caller is confirmed gone; a process restart drops the sets alongside the ARI
/// WebSocket, and Asterisk's Stasis-application-disconnect disposition then releases any residual channel.
/// </summary>
internal sealed class AsteriskPendingCallerTerminationRegistry : IAsteriskPendingCallerTerminationRegistry
{
    private static readonly ConcurrentDictionary<string, byte> _claims = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);

    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskPendingCallerTerminationRegistry"/> class.
    /// </summary>
    /// <param name="shellSettings">The tenant shell settings used to partition the process-wide claim and pending sets by tenant.</param>
    public AsteriskPendingCallerTerminationRegistry(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    /// <inheritdoc/>
    public bool HasTerminationClaim(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            return false;
        }

        return _claims.ContainsKey(GetKey(channelId));
    }

    /// <inheritdoc/>
    public void PlantTerminationClaim(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            return;
        }

        _claims[GetKey(channelId)] = 0;
    }

    /// <inheritdoc/>
    public void RemoveTerminationClaim(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            return;
        }

        _claims.TryRemove(GetKey(channelId), out _);
    }

    /// <inheritdoc/>
    public void Enqueue(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            return;
        }

        _pending.TryAdd(GetKey(channelId), 0);
    }

    /// <inheritdoc/>
    public void Resolve(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            return;
        }

        _pending.TryRemove(GetKey(channelId), out _);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetPending()
    {
        if (_pending.IsEmpty)
        {
            return [];
        }

        var prefix = GetKey(string.Empty);
        var pending = new List<string>();

        foreach (var key in _pending.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                pending.Add(key.Substring(prefix.Length));
            }
        }

        return pending;
    }

    private string GetKey(string channelId)
        => _shellSettings.Name + "|" + channelId;
}
