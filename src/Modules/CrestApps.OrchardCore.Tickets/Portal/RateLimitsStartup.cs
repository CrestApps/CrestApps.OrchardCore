using CrestApps.OrchardCore.Tickets.WebForm;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// Holds requesters and visitors to a number of new requests, replies and ratings when Orchard Core's Rate Limits
/// feature is on: per signed-in user, otherwise per visitor address (behind a proxy, set up Reverse Proxy so it is the
/// visitor's, not the proxy's).
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsStartup : StartupBase
{
    /// <summary>
    /// New requests a requester may open in ten minutes.
    /// </summary>
    public const int CreatesPerTenMinutes = 10;

    /// <summary>
    /// Replies and ratings a requester may send in ten minutes.
    /// </summary>
    public const int RepliesPerTenMinutes = 30;

    /// <summary>
    /// Replies and ratings a visitor address may send in ten minutes.
    /// </summary>
    public const int GuestPostsPerTenMinutes = 10;

    /// <summary>
    /// Ticket Form submissions a visitor may send in ten minutes.
    /// </summary>
    public const int WebFormPostsPerTenMinutes = 5;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.Configure<RateLimitsOptions>(options =>
        {
            var window = TimeSpan.FromMinutes(10);

            options.AddGroupRateLimit(TicketsConstants.RateLimitGroups.PortalCreate, RateLimitPartitionHelpers.CreateFixedWindowPolicy(TicketsConstants.RateLimitGroups.PortalCreate, CreatesPerTenMinutes, window, RateLimitPartitionHelpers.GetClientPartitionKey));
            options.AddGroupRateLimit(TicketsConstants.RateLimitGroups.PortalReply, RateLimitPartitionHelpers.CreateFixedWindowPolicy(TicketsConstants.RateLimitGroups.PortalReply, RepliesPerTenMinutes, window, RateLimitPartitionHelpers.GetClientPartitionKey));
            options.AddGroupRateLimit(TicketsConstants.RateLimitGroups.PortalGuest, RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(TicketsConstants.RateLimitGroups.PortalGuest, GuestPostsPerTenMinutes, window));
            options.AddGroupRateLimit(TicketsConstants.RateLimitGroups.WebForm, RateLimitPartitionHelpers.CreateFixedWindowPolicy(TicketsConstants.RateLimitGroups.WebForm, WebFormPostsPerTenMinutes, window, RateLimitPartitionHelpers.GetClientPartitionKey));

            // A refused form post goes back to the page, which asks the visitor to wait, instead of an error page.
            options.AddRouteRejectionHandler(TicketFormController.SubmitRouteName, TicketFormController.OnRateLimitedAsync);
        });
}
