using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Keeps pre-dialed agent legs in the distributed cache. A leg is a fact that lasts as long as its offer rings, a
/// few seconds, and the cache is shared across nodes wherever the deployment configured it to be, so the node that
/// receives the leg's answered webhook sees the leg the node handling the offer rang.
/// </summary>
public sealed class DistributedCacheAgentPreDialLegStore : IAgentPreDialLegStore
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly IClock _clock;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheAgentPreDialLegStore"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="clock">The clock used to turn an absolute expiry into a lifetime.</param>
    /// <param name="shellSettings">The current tenant, which keys the entries so tenants never see each other's legs.</param>
    public DistributedCacheAgentPreDialLegStore(
        IDistributedCache cache,
        IClock clock,
        ShellSettings shellSettings)
    {
        _cache = cache;
        _clock = clock;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public async Task<AgentPreDialLeg> FindAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(reservationId))
        {
            return null;
        }

        var value = await _cache.GetAsync(GetKey("reservation", reservationId), cancellationToken);

        if (value is null || value.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AgentPreDialLeg>(value, _serializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public Task<string> FindReservationIdByInteractionAsync(string interactionId, CancellationToken cancellationToken = default)
        => FindPointerAsync("interaction", interactionId, cancellationToken);

    /// <inheritdoc/>
    public Task<string> FindReservationIdByAgentAsync(string agentId, CancellationToken cancellationToken = default)
        => FindPointerAsync("agent", agentId, cancellationToken);

    /// <inheritdoc/>
    public async Task SaveAsync(AgentPreDialLeg leg, DateTime expiresUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leg);
        ArgumentException.ThrowIfNullOrEmpty(leg.ReservationId);

        var lifetime = expiresUtc - _clock.UtcNow;

        if (lifetime <= TimeSpan.Zero)
        {
            lifetime = TimeSpan.FromSeconds(1);
        }

        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime };

        await _cache.SetAsync(
            GetKey("reservation", leg.ReservationId),
            JsonSerializer.SerializeToUtf8Bytes(leg, _serializerOptions),
            options,
            cancellationToken);

        var pointer = Encoding.UTF8.GetBytes(leg.ReservationId);

        if (!string.IsNullOrEmpty(leg.InteractionId))
        {
            await _cache.SetAsync(GetKey("interaction", leg.InteractionId), pointer, options, cancellationToken);
        }

        if (!string.IsNullOrEmpty(leg.AgentId))
        {
            await _cache.SetAsync(GetKey("agent", leg.AgentId), pointer, options, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(AgentPreDialLeg leg, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leg);

        if (string.IsNullOrEmpty(leg.ReservationId))
        {
            return;
        }

        await _cache.RemoveAsync(GetKey("reservation", leg.ReservationId), cancellationToken);

        // A pointer is only removed while it still points at this offer: the agent may already have been offered
        // the next call, whose pointer must survive this one being forgotten.
        await RemovePointerAsync("interaction", leg.InteractionId, leg.ReservationId, cancellationToken);
        await RemovePointerAsync("agent", leg.AgentId, leg.ReservationId, cancellationToken);
    }

    private async Task<string> FindPointerAsync(string kind, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var value = await _cache.GetAsync(GetKey(kind, id), cancellationToken);

        return value is null || value.Length == 0
            ? null
            : Encoding.UTF8.GetString(value);
    }

    private async Task RemovePointerAsync(string kind, string id, string reservationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        var current = await FindPointerAsync(kind, id, cancellationToken);

        if (string.Equals(current, reservationId, StringComparison.Ordinal))
        {
            await _cache.RemoveAsync(GetKey(kind, id), cancellationToken);
        }
    }

    private string GetKey(string kind, string id)
        => $"ContactCenter:AgentPreDial:{_tenantName}:{kind}:{id}";
}
