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
    /// <param name="context">Who asks: the platform, and for an unregistered one the profile address it signed in with
    /// (<see cref="AgentCheckout.ProfileUrlProperty"/>).</param>
    /// <param name="settings">The agentic commerce settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<bool> IsFullAsync(this IAgentCheckoutStore store, AgentCheckoutContext context, AgenticCommerceSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(context.PlatformId);
        ArgumentNullException.ThrowIfNull(settings);

        var limit = Math.Max(1, settings.MaxOpenCheckoutsPerPlatform);

        if (await store.CountOpenAsync(context.PlatformId, cancellationToken) >= limit)
        {
            return true;
        }

        if (!context.PlatformId.StartsWith(AgentPlatform.UnregisteredIdPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // An unregistered platform is known only by its profile's address; without one there is no share to give it.
        return AgentPlatformAddress.GetOriginKey(context.GetProperty(AgentCheckout.ProfileUrlProperty)) is not { } origin ||
            await store.CountOpenFromSameOriginAsync(origin, cancellationToken) >= Math.Max(1, limit / UnregisteredOriginShare);
    }
}
