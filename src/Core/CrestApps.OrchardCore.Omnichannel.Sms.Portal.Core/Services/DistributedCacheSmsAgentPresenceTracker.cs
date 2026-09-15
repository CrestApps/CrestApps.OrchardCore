using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Keeps portal presence in the distributed cache. A heartbeat is a fact that expires, not a record worth a
/// document write every few seconds per agent, and the cache is shared across nodes wherever the deployment
/// has configured it to be, so an agent whose portal is open on one node is present to the router on every
/// other.
/// </summary>
public sealed class DistributedCacheSmsAgentPresenceTracker : ISmsAgentPresenceTracker
{
    private static readonly byte[] _presentMarker = [1];

    private readonly IDistributedCache _cache;
    private readonly TimeSpan _timeout;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheSmsAgentPresenceTracker"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="availabilityOptions">
    /// The agent availability options. The same heartbeat timeout voice presence uses, so an operator tuning
    /// how long a silent agent stays reachable tunes it once for every channel.
    /// </param>
    /// <param name="shellSettings">The current tenant, which keys the entries so tenants never see each other's agents.</param>
    public DistributedCacheSmsAgentPresenceTracker(
        IDistributedCache cache,
        IOptions<AgentAvailabilityOptions> availabilityOptions,
        ShellSettings shellSettings)
    {
        _cache = cache;
        _timeout = availabilityOptions.Value.HeartbeatTimeout;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public Task TouchAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        return _cache.SetAsync(
            GetKey(agentId),
            _presentMarker,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _timeout },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsPresentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        return await _cache.GetAsync(GetKey(agentId), cancellationToken) is not null;
    }

    private string GetKey(string agentId)
        => $"SmsPortal:Presence:{_tenantName}:{agentId}";
}
