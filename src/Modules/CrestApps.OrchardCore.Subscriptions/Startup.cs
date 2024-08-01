using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.Indexes;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Mvc.Core.Utilities;

namespace CrestApps.OrchardCore.Subscriptions;

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

        services.AddScoped<ISubscriptionHandler, ContentSubscriptionHandler>();

        services.AddDataMigration<SubscriptionSessionIndexMigrations>()
            .AddIndexProvider<SubscriptionSessionIndexProvider>();

        services.AddScoped<IDisplayDriver<SubscriptionFlow>, SubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, ContentSubscriptionFlowDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, SubscriptionPartSettingsDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "ListSubscriptions",
            areaName: SubscriptionsConstants.Features.ModuleId,
            pattern: "Subscriptions/{contentType?}",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Index) }
        );

        routes.MapAreaControllerRoute(
            name: "SubscriptionsSignup",
            areaName: SubscriptionsConstants.Features.ModuleId,
            pattern: "Subscription/{contentItemId}/Signup",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Signup) }
        );
    }
}
