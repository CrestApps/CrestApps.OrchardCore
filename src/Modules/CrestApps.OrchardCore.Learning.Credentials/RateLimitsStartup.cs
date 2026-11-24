using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Learning.Credentials;

/// <summary>
/// Holds each visitor to a number of requests a minute on the public credential endpoints and the verification page,
/// when Orchard Core Rate Limits is on. A credential's code has 128 random bits, so guessing one is hopeless; the limit
/// keeps the endpoints from being used to probe or to load the site.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsStartup : StartupBase
{
    /// <summary>
    /// How many requests a visitor may make a minute.
    /// </summary>
    public const int RequestsPerMinute = 120;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options.AddGroupRateLimit(
            CredentialsConstants.RateLimitGroup,
            RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(
                policyName: CredentialsConstants.RateLimitGroup,
                permitLimit: RequestsPerMinute,
                window: TimeSpan.FromMinutes(1))));
    }
}
