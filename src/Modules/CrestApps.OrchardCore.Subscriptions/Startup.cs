using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Endpoints;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.Drivers.Steps;
using CrestApps.OrchardCore.Subscriptions.Endpoints;
using CrestApps.OrchardCore.Subscriptions.Handlers;
using CrestApps.OrchardCore.Subscriptions.Indexes;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions;

public sealed class Startup : StartupBase
{
    private static readonly string _subscriptionControllerName = typeof(SubscriptionsController).ControllerName();

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<SubscriptionPartMigrations>()
            .AddContentPart<SubscriptionPart>()
            .UseDisplayDriver<SubscriptionPartDisplayDriver>();

        services.AddDataMigration<SubscriptionsContentItemIndexMigrations>()
            .AddScopedIndexProvider<SubscriptionsContentItemIndexProvider>();

        services.AddDataMigration<SubscriptionSessionIndexMigrations>()
            .AddIndexProvider<SubscriptionSessionIndexProvider>();

        services.AddScoped<IDisplayDriver<SubscriptionFlow>, DefaultSubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, ContentStepSubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, PaymentStepSubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, UserRegistrationSubscriptionFlowDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, SubscriptionPartSettingsDisplayDriver>();

        // TODO, we should not depend on the DI registration order.
        // Important: register UserRegistrationSubscriptionHandler before PaymentSubscriptionHandler  to ensure 
        // that the conceal logic is applied first. 
        services.AddScoped<ISubscriptionHandler, UserRegistrationSubscriptionHandler>()
            .AddScoped<ISubscriptionHandler, PaymentSubscriptionHandler>();

        services.AddScoped<ISubscriptionHandler, ContentSubscriptionHandler>();
        services.AddScoped<ISubscriptionSessionStore, SubscriptionSessionStore>();

        services.AddScoped<SubscriptionPaymentSession>();
        services.AddScoped<IDisplayDriver<SubscriptionRegisterUserForm>, SubscriptionRegisterUserFormDisplayDriver>();
        services.Configure<SubscriptionPaymentSessionOptions>(options =>
        {
            options.MaxLiveSession = TimeSpan.FromDays(1);
            options.Purposes.Add(SubscriptionPaymentSessionExtensions.InitialPaymentPurpose);
            options.Purposes.Add(SubscriptionPaymentSessionExtensions.SubscriptionPaymentInfoPurpose);
            options.Purposes.Add(SubscriptionPaymentSessionExtensions.UserRegistrationPurpose);
        });

        services.AddScoped<IDisplayDriver<ISite>, SubscriptionSettingsDisplayDriver>();
        services.AddScoped<IPermissionProvider, SubscriptionPermissionsProvider>();
        services.AddScoped<INavigationProvider, SubscriptionsAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "ListSubscriptions",
            areaName: SubscriptionConstants.Features.ModuleId,
            pattern: "Subscriptions/{contentType?}",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Index) }
        );

        routes.MapAreaControllerRoute(
            name: "SubscriptionSignup",
            areaName: SubscriptionConstants.Features.ModuleId,
            pattern: "Subscription/{contentItemId}/Signup",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Signup) }
        );

        routes.MapAreaControllerRoute(
            name: "SubscriptionSignupConfirmation",
            areaName: SubscriptionConstants.Features.ModuleId,
            pattern: "Subscription/{sessionId}/Signup/Confirmation",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Confirmation) }
        );

        routes.MapAreaControllerRoute(
            name: "SubscriptionSignupStep",
            areaName: SubscriptionConstants.Features.ModuleId,
            pattern: "Subscription/{sessionId}/Signup/Step/{step?}",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Display) }
        );
    }
}

[RequireFeatures("OrchardCore.Roles")]
public sealed class RolesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<ISite>, SubscriptionRoleSettingsDisplayDriver>();
    }
}

[Feature(SubscriptionConstants.Features.Stripe)]
public sealed class StripeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, StripePaymentSubscriptionFlowDisplayDriver>();
        services.AddScoped<IPaymentEvent, SubscriptionPaymentHandler>();
        services.AddScoped<IContentHandler, SubscriptionsContentHandler>();
        services.AddScoped<ISubscriptionHandler, StripeSubscriptionHandler>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddCreateStripeSubscriptionEndpoint()
            .AddCreatePaymentIntentEndpoint()
            .AddStripeCreateSetupIntentEndpoint();
    }
}

[Feature(SubscriptionConstants.Features.TenantOnboarding)]
public sealed class TenantOnboardingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TenantOnboardingPart>()
            .UseDisplayDriver<TenantOnboardingPartDisplayDriver>();

        services.AddDataMigration<TenantOnboardingMigrations>();
        services.AddScoped<ISubscriptionHandler, UserRegistrationSubscriptionHandler>();
        services.AddScoped<ISubscriptionHandler, TenantOnboardingSubscriptionHandler>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, TenantOnboardingStepSubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<ISite>, SubscriptionOnboardingSettingsDisplayDriver>();
    }
}
