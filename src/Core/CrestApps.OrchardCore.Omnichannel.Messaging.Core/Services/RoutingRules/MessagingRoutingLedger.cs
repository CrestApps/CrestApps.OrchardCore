using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;

/// <summary>
/// Remembers when the routing rules last gave each agent a conversation, which is whose turn it is in a round robin.
/// </summary>
internal interface IMessagingRoutingLedger
{
    /// <summary>
    /// Gets when each user was last given a conversation by a routing rule.
    /// </summary>
    /// <param name="userIds">The agents' user ids.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The time, in UTC, by user id. A user never given one is left out.</returns>
    Task<IReadOnlyDictionary<string, DateTime>> GetLastAssignedAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a user was given a conversation.
    /// </summary>
    /// <param name="userId">The agent's user id.</param>
    /// <param name="assignedUtc">When, in UTC.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RecordAsync(string userId, DateTime assignedUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps the routing rules' round robin in the tenant's distributed cache, so every node sees the same turn when the
/// cache is shared. A cache that is lost or not shared only makes the rotation approximate for a while; it never stops a
/// conversation from being assigned.
/// </summary>
internal sealed class DistributedCacheMessagingRoutingLedger : IMessagingRoutingLedger
{
    private static readonly DistributedCacheEntryOptions _entryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromDays(30),
    };

    private readonly IDistributedCache _cache;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheMessagingRoutingLedger"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="shellSettings">The tenant, whose name keeps its keys apart from another tenant's.</param>
    public DistributedCacheMessagingRoutingLedger(IDistributedCache cache, ShellSettings shellSettings)
    {
        _cache = cache;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, DateTime>> GetLastAssignedAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        foreach (var userId in userIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal) ?? [])
        {
            var value = await _cache.GetStringAsync(GetKey(userId), cancellationToken);

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) &&
                ticks > 0 &&
                ticks <= DateTime.MaxValue.Ticks)
            {
                result[userId] = new DateTime(ticks, DateTimeKind.Utc);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public Task RecordAsync(string userId, DateTime assignedUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return _cache.SetStringAsync(GetKey(userId), assignedUtc.Ticks.ToString(CultureInfo.InvariantCulture), _entryOptions, cancellationToken);
    }

    private string GetKey(string userId)
        => $"MessagingRoutingLedger:{_tenantName}:{userId}";
}
