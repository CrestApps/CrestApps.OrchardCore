using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Drivers;
using CrestApps.OrchardCore.Learning.Xapi.Endpoints;
using CrestApps.OrchardCore.Learning.Xapi.Migrations;
using CrestApps.OrchardCore.Learning.Xapi.Navigation;
using CrestApps.OrchardCore.Learning.Xapi.Services;
using CrestApps.OrchardCore.Learning.Xapi.ViewModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.RateLimits;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Xapi;

/// <summary>
/// Registers the Learning Record Store: the store and its indexes, the LRS endpoint, the permissions and the admin list
/// of statements.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(XapiConstants.CollectionName));
        services.AddDataMigration<XapiMigrations>()
            .AddIndexProvider<XapiStatementIndexProvider>()
            .AddIndexProvider<XapiDocumentIndexProvider>()
            .AddIndexProvider<XapiActivityIndexProvider>();

        services.AddLearningXapiCore();
        services.AddPermissionProvider<XapiPermissionProvider>();
        services.AddNavigationProvider<XapiAdminMenu>();
        services.TryAddScoped<LearnerUserLookup>();
        services.AddDisplayDriver<XapiStatementRow, XapiStatementRowDisplayDriver>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapLearningXapi();
}

/// <summary>
/// Limits how many requests one address may send to the LRS, when Orchard Core's Rate Limits feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsStartup : StartupBase
{
    /// <summary>
    /// How many LRS requests an address may send a minute: enough for a classroom behind one address.
    /// </summary>
    public const int RequestsPerMinute = 1200;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options.AddGroupRateLimit(
            XapiConstants.RateLimitGroup,
            RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(
                policyName: XapiConstants.RateLimitGroup,
                permitLimit: RequestsPerMinute,
                window: TimeSpan.FromMinutes(1))));
    }
}
