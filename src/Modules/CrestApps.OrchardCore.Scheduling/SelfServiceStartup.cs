using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Throttling;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Registers what the public booking pages need: the per-network attempt policy that throttles booking whether or not
/// Orchard Core's Rate Limits feature is on, and the signed tokens of e-mail confirmation links.
/// </summary>
[Feature(SchedulingConstants.Features.SelfService)]
public sealed class SelfServiceStartup : StartupBase
{
    /// <summary>
    /// How many bookings one network may make from the public booking pages in <see cref="BookingWindow"/>.
    /// </summary>
    internal const int BookingsPerNetwork = 10;

    internal static readonly TimeSpan BookingWindow = TimeSpan.FromHours(1);

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSignedLinks();
        services.AddAttemptPolicy(SchedulingConstants.AttemptPolicies.BookingNetwork, BookingsPerNetwork, BookingWindow);
    }
}

/// <summary>
/// With Orchard Core's Rate Limits feature on, holds each network to a number of booking posts in a window, across every
/// booking page. It shows read-only under Tools › Rate Limits, where a Group policy on <c>scheduling-booking</c> can add
/// stricter limits.
/// </summary>
[Feature(SchedulingConstants.Features.SelfService)]
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class SelfServiceRateLimitsStartup : StartupBase
{
    /// <summary>
    /// How many booking posts one network may make in ten minutes.
    /// </summary>
    internal const int BookingsPerWindow = 20;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options.AddClientGroupRateLimit(
            SchedulingConstants.RateLimitGroups.Booking,
            BookingsPerWindow,
            TimeSpan.FromMinutes(10),
            networkOnly: true));
    }
}
