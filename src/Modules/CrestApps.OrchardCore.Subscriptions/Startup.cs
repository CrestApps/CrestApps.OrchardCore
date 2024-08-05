using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.Handlers;
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
using OrchardCore.Json;
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

        services.AddDataMigration<SubscriptionSessionIndexMigrations>()
            .AddIndexProvider<SubscriptionSessionIndexProvider>();

        services.AddScoped<IDisplayDriver<SubscriptionFlow>, SubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, ContentSubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, PaymentSubscriptionFlowDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, SubscriptionPartSettingsDisplayDriver>();

        services.AddScoped<ISubscriptionHandler, ContentSubscriptionHandler>();
        services.AddScoped<ISubscriptionHandler, PaymentSubscriptionHandler>();

        services.AddSingleton<SubscriptionPaymentSession>();
        services.Configure<DocumentJsonSerializerOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(BillingDurationKeyJsonConverter.Instance);
        });
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

        routes.MapAreaControllerRoute(
            name: "SubscriptionsSignupConfirmation",
            areaName: SubscriptionsConstants.Features.ModuleId,
            pattern: "Subscription/{sessionId}/Signup/Confirmation",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Confirmation) }
        );

        routes.MapAreaControllerRoute(
            name: "SubscriptionsSignupStep",
            areaName: SubscriptionsConstants.Features.ModuleId,
            pattern: "Subscription/{sessionId}/Signup/Step/{step?}",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.ViewSession) }
        );
    }
}

[Feature(SubscriptionsConstants.Features.Stripe)]
public sealed class StripeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, StripePaymentSubscriptionFlowDisplayDriver>();
        services.AddScoped<IPaymentEvent, SubscriptionPaymentHandler>();
    }
}
