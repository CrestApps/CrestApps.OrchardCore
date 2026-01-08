using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Stripe.Core;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using YesSql.Filters.Query;

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
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, EnhancedConfirmationSubscriptionFlowDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, SubscriptionPartSettingsDisplayDriver>();

        services.AddScoped<ISubscriptionHandler, UserRegistrationSubscriptionHandler>();
        services.AddScoped<ISubscriptionHandler, PaymentSubscriptionHandler>();
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

        services.AddScoped<IAuthorizationHandler, SubscriptionsPermissionsHandler>();

        services.AddSiteDisplayDriver<SubscriptionSettingsDisplayDriver>();
        services.AddScoped<IPermissionProvider, SubscriptionPermissionsProvider>();
        services.AddNavigationProvider<SubscriptionsAdminMenu>();

        services.AddDataMigration<SubscriptionIndexMigrations>()
            .AddIndexProvider<SubscriptionIndexProvider>();

        services.AddIndexProvider<SubscriptionTransactionIndexProvider>()
            .AddDataMigration<SubscriptionTransactionIndexMigrations>();

        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, SubscriptionResourceManagementOptionsConfiguration>();

        services.AddScoped<IDisplayDriver<SubscriberDashboard>, SubscriberDashboardDisplayDriver>();

        services.AddScoped<IDisplayDriver<ListSubscriptionOptions>, ListSubscriptionOptionsDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionSession>, SubscriptionSessionDisplayDriver>();

        services.AddScoped<ISubscriptionsAdminListQueryService, DefaultSubscriptionsAdminListQueryService>();

        services.AddTransient<ISubscriptionAdminListFilterProvider, DefaultSubscriptionAdminListFilterProvider>();
        services.AddSingleton<ISubscriptionAdminListFilterParser>(sp =>
        {
            var filterProviders = sp.GetServices<ISubscriptionAdminListFilterProvider>();
            var builder = new QueryEngineBuilder<SubscriptionSession>();
            foreach (var provider in filterProviders)
            {
                provider.Build(builder);
            }

            var parser = builder.Build();

            return new DefaultSubscriptionsAdminListFilterParser(parser);
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAreaControllerRoute(
            name: "ListServicePlans",
            areaName: SubscriptionConstants.Features.Area,
            pattern: "ServicePlans/{contentType?}",
            defaults: new { controller = typeof(ServicePlansController).ControllerName(), action = nameof(ServicePlansController.Index) }
        );

        routes.MapAreaControllerRoute(
            name: "SubscriptionSignup",
            areaName: SubscriptionConstants.Features.Area,
            pattern: "Subscription/Signup/{contentItemId}",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Signup) }
        );

        routes.MapAreaControllerRoute(
            name: "SubscriptionConfirmation",
            areaName: SubscriptionConstants.Features.Area,
            pattern: "Subscription/Confirmation/{sessionId}",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Confirmation) }
        );

        routes.MapAreaControllerRoute(
            name: "SubscriptionStep",
            areaName: SubscriptionConstants.Features.Area,
            pattern: "Subscription/Step/{sessionId}",
            defaults: new { controller = _subscriptionControllerName, action = nameof(SubscriptionsController.Display) }
        );
    }
}

[RequireFeatures("OrchardCore.Roles")]
public sealed class RolesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<SubscriptionRoleSettingsDisplayDriver>();
    }
}

[Feature(SubscriptionConstants.Features.Stripe)]
public sealed class StripeStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<SubscriptionFlowPaymentMethod>, StripePaymentSubscriptionFlowDisplayDriver>();
        services.AddScoped<StripePriceSyncService>();
        services.AddScoped<IFeatureEventHandler, StripePriceSyncHandler>();
        services.AddScoped<IPaymentEvent, SubscriptionPaymentHandler>();
        services.AddScoped<IContentHandler, SubscriptionsContentHandler>();
        services.AddScoped<ISubscriptionHandler, StripeSubscriptionHandler>();
        services.AddTransient<IPostConfigureOptions<PaymentMethodOptions>, DefaultPaymentMethodConfigurations>();
        services.AddSiteDisplayDriver<CurrencySubscriptionSettingsDisplayDriver>();
        services.Configure<PaymentMethodOptions>(options =>
        {
            options.PaymentMethods[StripeConstants.ProcessorKey] = new PaymentMethod
            {
                Title = "Stripe",
                HasProcessor = true,
            };
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddCreateStripeSubscriptionEndpoint()
            .AddCreatePaymentIntentEndpoint()
            .AddStripeCreateSetupIntentEndpoint();
    }
}

[Feature(SubscriptionConstants.Features.PayLater)]
public sealed class PayLaterStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<SubscriptionFlowPaymentMethod>, PayLaterPaymentSubscriptionFlowDisplayDriver>();
        services.Configure<PaymentMethodOptions>(options =>
        {
            options.PaymentMethods[SubscriptionConstants.PayLaterProcessorKey] = new PaymentMethod
            {
                Title = "Pay Later",
                HasProcessor = false,
            };
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddCreatePayLaterEndpoint();
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
        services.AddScoped<ISubscriptionHandler, TenantOnboardingSubscriptionHandler>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, TenantOnboardingStepSubscriptionFlowDisplayDriver>();
        services.AddSiteDisplayDriver<SubscriptionOnboardingSettingsDisplayDriver>();
    }
}

[Feature(SubscriptionConstants.Features.TenantOnboarding)]
[RequireFeatures("OrchardCore.Tenants.FeatureProfiles")]
public sealed class FeatureProfileTenantOnboardingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TenantOnboardingPart>()
            .UseDisplayDriver<FeatureProfilesTenantOnboardingPartDisplayDriver>();

        services.AddScoped<IDisplayDriver<SubscriptionFlow>, FeatureProfileTenantOnboardingStepSubscriptionFlowDisplayDriver>();
        services.AddIndexProvider<SubscriptionTenantIndexProvider>();
    }
}

[Feature(SubscriptionConstants.Features.ReCaptcha)]
public sealed class ReCaptchaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, ReCaptchaSubscriptionFlowDisplayDriver>();
    }
}
