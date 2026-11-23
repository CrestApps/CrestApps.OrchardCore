using CrestApps.OrchardCore.MailingLists.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.MailingLists.SignupWidget;

/// <summary>
/// Registers the mailing list sign-up widget.
/// </summary>
[Feature(MailingListsConstants.Features.SignupWidget)]
public sealed class SignupWidgetStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<MailingListSignupPart>()
            .UseDisplayDriver<MailingListSignupPartDisplayDriver>();
        services.AddDataMigration<MailingListSignupMigrations>();
    }
}

/// <summary>
/// Holds each visitor to a number of sign-ups through Orchard Core's Rate Limits: per signed-in user, otherwise per
/// visitor address (behind a proxy, set up Reverse Proxy so it is the visitor's, not the proxy's).
/// </summary>
[Feature(MailingListsConstants.Features.SignupWidget)]
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class SignupRateLimitsStartup : StartupBase
{
    /// <summary>
    /// The sign-ups a visitor may send in ten minutes.
    /// </summary>
    public const int SignupsPerTenMinutes = 10;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.Configure<RateLimitsOptions>(options => options.AddGroupRateLimit(
            MailingListsConstants.RateLimitGroups.Signup,
            RateLimitPartitionHelpers.CreateFixedWindowPolicy(MailingListsConstants.RateLimitGroups.Signup, SignupsPerTenMinutes, TimeSpan.FromMinutes(10), RateLimitPartitionHelpers.GetClientPartitionKey)));
}
