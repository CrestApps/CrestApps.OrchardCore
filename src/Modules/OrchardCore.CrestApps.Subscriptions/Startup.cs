using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.CrestApps.Subscriptions.Controllers;
using OrchardCore.CrestApps.Subscriptions.Core.Indexes;
using OrchardCore.CrestApps.Subscriptions.Core.Models;
using OrchardCore.CrestApps.Subscriptions.Drivers;
using OrchardCore.CrestApps.Subscriptions.Migrations;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Mvc.Core.Utilities;

namespace OrchardCore.CrestApps.Subscriptions;

public sealed class Startup : StartupBase
{
    private static readonly string _subscriptionControllerName = typeof(SubscriptionsController).ControllerName();

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<SubscriptionsPartMigrations>()
            .AddContentPart<SubscriptionsPart>()
            .UseDisplayDriver<SubscriptionsPartDisplayDriver>();

        services.AddDataMigration<SubscriptionsContentItemIndexMigrations>()
            .AddScopedIndexProvider<SubscriptionsContentItemIndexProvider>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "ListSubscriptions",
            areaName: "OrchardCore.CrestApps.Subscriptions",
            pattern: "Subscriptions/{contentType?}",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Index) }
        );

        routes.MapAreaControllerRoute(
            name: "SubscriptionsSignup",
            areaName: "OrchardCore.CrestApps.Subscriptions",
            pattern: "Subscription/{id}/Signup",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Signup) }
        );
    }
}
