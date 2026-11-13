using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// The built-in limit of the sign-in code pages, listed read-only under Tools &gt; Rate Limits.
/// </summary>
[Feature(UsersConstants.Feature.EmailCodeSignIn)]
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class EmailCodeSignInRateLimitsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.Configure<RateLimitsOptions>(options => options
            .AddClientGroupRateLimit(EmailSignInCodes.RateLimitGroup, 20, TimeSpan.FromMinutes(1)));
}
