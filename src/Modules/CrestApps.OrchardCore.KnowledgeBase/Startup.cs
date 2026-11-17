using CrestApps.OrchardCore.KnowledgeBase.Drivers;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Migrations;
using CrestApps.OrchardCore.KnowledgeBase.Navigation;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.RateLimits;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.KnowledgeBase;

/// <summary>
/// Registers the Knowledge Base.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<KnowledgeArticlePart>()
            .UseDisplayDriver<KnowledgeArticlePartDisplayDriver>();
        services.AddIndexProvider<KnowledgeArticleIndexProvider>();
        services.AddDataMigration<KnowledgeBaseMigrations>();
        services.AddScoped<KnowledgeArticleAccess>();
        services.AddScoped<KnowledgeArticleSearch>();
        services.AddScoped<KnowledgeBaseCategories>();
        services.AddScoped<IAuthorizationHandler, KnowledgeArticleAuthorizationHandler>();
        services.AddPermissionProvider<KnowledgeBasePermissionProvider>();
        services.AddNavigationProvider<KnowledgeBaseAdminMenu>();
    }
}

/// <summary>
/// Holds each visitor address to a number of "was this helpful?" answers, when Orchard Core's Rate Limits feature is on,
/// so a script can't push an article up the help center.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsStartup : StartupBase
{
    /// <summary>
    /// Answers a visitor address may give in ten minutes.
    /// </summary>
    public const int FeedbackPerTenMinutes = 20;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.Configure<RateLimitsOptions>(options => options.AddGroupRateLimit(
            KnowledgeBaseConstants.FeedbackRateLimitGroup,
            RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(KnowledgeBaseConstants.FeedbackRateLimitGroup, FeedbackPerTenMinutes, TimeSpan.FromMinutes(10))));
}
