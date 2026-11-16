using CrestApps.OrchardCore.AgenticCommerce.Core.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Whether an agent platform may open another checkout or cart.
/// </summary>
public static class AgentCheckoutAllowance
{
    /// <summary>
    /// The part of the allowance the unregistered platforms of one origin may hold: one in this many.
    /// </summary>
    public const int UnregisteredOriginShare = 10;

    /// <summary>
    /// Returns whether a platform has as many open checkouts and carts as it may. A registered platform may have
    /// <see cref="AgenticCommerceSettings.MaxOpenCheckoutsPerPlatform"/>. Unregistered platforms may have that many
    /// together, and those whose profiles share an origin a tenth of it, so one caller can't take the whole allowance.
    /// </summary>
    /// <param name="store">The agent checkout store.</param>
    /// <param name="platformId">The platform's id.</param>
    /// <param name="settings">The agentic commerce settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<bool> IsFullAsync(this IAgentCheckoutStore store, string platformId, AgenticCommerceSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(platformId);
        ArgumentNullException.ThrowIfNull(settings);

        var limit = Math.Max(1, settings.MaxOpenCheckoutsPerPlatform);

        if (await store.CountOpenAsync(platformId, cancellationToken) >= limit)
        {
            return true;
        }

        return platformId.StartsWith(AgentPlatform.UnregisteredIdPrefix, StringComparison.Ordinal) &&
            await store.CountOpenFromSameOriginAsync(platformId, cancellationToken) >= Math.Max(1, limit / UnregisteredOriginShare);
    }
}
