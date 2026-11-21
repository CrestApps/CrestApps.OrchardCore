using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Holds each visitor to a number of requests a minute on the respondent pages (start, page posts, autosave, live
/// evaluation and answer checks), when Orchard Core Rate Limits is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RespondRateLimitsStartup : StartupBase
{
    /// <summary>
    /// The rate-limit group of the respondent pages.
    /// </summary>
    public const string RateLimitGroup = "Surveys.Respond";

    /// <summary>
    /// How many requests a visitor may make a minute.
    /// </summary>
    public const int RequestsPerMinute = 120;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options.AddGroupRateLimit(
            RateLimitGroup,
            RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(
                policyName: RateLimitGroup,
                permitLimit: RequestsPerMinute,
                window: TimeSpan.FromMinutes(1))));
    }
}
