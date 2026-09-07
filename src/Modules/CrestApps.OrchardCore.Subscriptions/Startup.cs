using CrestApps.OrchardCore.Subscriptions.Navigation;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout;
using OrchardCore.BackgroundTasks;
using CrestApps.OrchardCore.Subscriptions.Tasks;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.Drivers.Steps;
using CrestApps.OrchardCore.Subscriptions.Endpoints;
using CrestApps.OrchardCore.Subscriptions.Handlers;
using CrestApps.OrchardCore.Subscriptions.Indexes;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Reports;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.Workflows.Drivers;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Wizard;
using CrestApps.OrchardCore.Wizard.Core.Services;
using CrestApps.OrchardCore.Wizard.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Registers the core subscription services, content parts, indexes, permissions, filters, and admin UI components.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <summary>
    /// Configures the service registrations required by the base subscriptions feature.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<SubscriptionPartMigrations>()
            .AddContentPart<SubscriptionPart>()
            .UseDisplayDriver<SubscriptionPartDisplayDriver>();

        // Automatically inject the SubscriptionPart into any content type that uses the Subscription
        // stereotype, so administrators only need to set the stereotype.
        services.AddScoped<IContentDefinitionHandler, SubscriptionPartContentTypeDefinitionHandler>();

        services.AddDataMigration<SubscriptionSummaryWidgetMigrations>()
            .AddContentPart<SubscriptionSummaryPart>()
            .UseDisplayDriver<SubscriptionSummaryPartDisplayDriver>();

        services.AddDataMigration<SubscriptionsContentItemIndexMigrations>()
            .AddScopedIndexProvider<SubscriptionsContentItemIndexProvider>();

        services.AddDataMigration<SubscriptionSessionIndexMigrations>()
            .AddIndexProvider<SubscriptionSessionIndexProvider>();

        services.AddScoped<IDisplayDriver<SubscriptionFlow>, DefaultSubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, ContentStepSubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, PaymentStepSubscriptionFlowDisplayDriver>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, UserRegistrationSubscriptionFlowDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, SubscriptionPartSettingsDisplayDriver>();
        services.AddScoped<IContentDefinitionHandler, SubscriptionContentTypeDefinitionHandler>();

        services.AddScoped<ISubscriptionHandler, UserRegistrationSubscriptionHandler>();
        services.AddScoped<ISubscriptionHandler, PaymentSubscriptionHandler>();
        services.AddScoped<ISubscriptionHandler, ContentSubscriptionHandler>();

        // Taxation is optional. The no-op tax service keeps subscriptions working when the Taxation
        // feature is disabled; the taxation-aware implementation is registered by the TaxationStartup
        // below only when the Taxation feature is enabled.
        services.TryAddScoped<ISubscriptionTaxProfileProvider, DefaultSubscriptionTaxProfileProvider>();
        services.TryAddScoped<ISubscriptionTaxService, NullSubscriptionTaxService>();

        services.TryAddScoped<GuestSessionTokenManager>();
        services.AddScoped<ISubscriptionSessionStore, SubscriptionSessionStore>();
        services.AddScoped<WizardSessionStore>();
        services.AddScoped<SubscriptionWizardFlowFactory>();
        services.AddScoped<IWizardSessionStore, SubscriptionWizardSessionStore>();
        services.AddScoped<IWizardHandler, SubscriptionWizardHandler>();
        services.AddScoped<IDisplayDriver<WizardFlow>, SubscriptionWizardFlowDisplayDriver>();

        services.AddScoped<SubscriptionPaymentSession>();

        // Compute the default payment method once every payment provider has registered its methods.
        // This lives in the base feature so a default is always resolved regardless of which single
        // provider (Stripe, Pay Later, ...) happens to be enabled.
        services.AddTransient<IPostConfigureOptions<PaymentMethodOptions>, DefaultPaymentMethodConfigurations>();

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

        // The durable subscription agreement. It is what survives the checkout that created it, so every
        // later question about who is subscribed is answered from here rather than from a session.
        services.AddDataMigration<SubscriptionRecordMigrations>()
            .AddIndexProvider<SubscriptionRecordIndexProvider>();

        services.AddScoped<ISubscriptionStore, SubscriptionStore>();
        services.AddScoped<ISubscriptionManager, SubscriptionManager>();
        services.AddScoped<ISubscriptionLifecycleService, DefaultSubscriptionLifecycleService>();
        services.AddScoped<ISubscriptionAccessService, DefaultSubscriptionAccessService>();
        services.AddScoped<ISubscriptionLifecycleHandler, EntitlementSubscriptionLifecycleHandler>();
        services.AddSingleton<IBackgroundTask, SubscriptionLifecycleBackgroundTask>();

        // The gateway is authoritative for a recurring agreement, so its notifications are what keep the
        // local record honest about renewals, failures, and cancellations made outside this application.
        services.AddScoped<IPaymentEvent, SubscriptionRecordPaymentEventHandler>();

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
}

/// <summary>
/// Registers subscription settings that depend on the Orchard Core roles feature.
/// </summary>
[RequireFeatures("OrchardCore.Roles")]
public sealed class RolesStartup : StartupBase
{
    /// <summary>
    /// Configures role-dependent subscription services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<SubscriptionRoleSettingsDisplayDriver>();

        // Roles already gate content, features, and permissions across Orchard Core, so granting one for as
        // long as a subscription is current is what makes member-only access enforceable without any of
        // those gates knowing subscriptions exist.
        services.AddScoped<ISubscriptionEntitlementApplier, RoleSubscriptionEntitlementApplier>();

        services.AddDataMigration<SubscriptionEntitlementPartMigrations>()
            .AddContentPart<SubscriptionEntitlementPart>()
            .UseDisplayDriver<SubscriptionEntitlementPartDisplayDriver>();
    }
}

/// <summary>
/// Wires the Stripe payment integration into the subscription checkout. It activates automatically
/// whenever both the Subscriptions and Stripe features are enabled, so there is no separate integration
/// feature to switch on.
/// </summary>
[RequireFeatures(SubscriptionConstants.Features.Area, StripeConstants.Feature.ModuleId)]
public sealed class StripeStartup : StartupBase
{
    /// <summary>
    /// Configures the Stripe services used by subscription checkout.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDisplayDriver<SubscriptionFlowPaymentMethod>, StripePaymentSubscriptionFlowDisplayDriver>();
        services.AddScoped<StripePriceSyncService>();
        services.AddScoped<IPaymentEvent, SubscriptionPaymentHandler>();
        services.AddScoped<IContentHandler, SubscriptionsContentHandler>();
        services.AddScoped<ISubscriptionHandler, StripeSubscriptionHandler>();
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

    /// <summary>
    /// Adds the Stripe subscription checkout endpoints to the route builder.
    /// </summary>
    /// <param name="app">The application builder for the current tenant pipeline.</param>
    /// <param name="routes">The endpoint route builder to configure.</param>
    /// <param name="serviceProvider">The tenant service provider.</param>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddCreateStripeSubscriptionEndpoint()
            .AddCreatePaymentIntentEndpoint()
            .AddStripeCreateSetupIntentEndpoint()
            .AddCreateCheckoutSessionEndpoint();
    }
}

/// <summary>
/// Wires the offline Pay Later option into the subscription checkout. It activates when the standalone
/// Pay Later module is enabled alongside Subscriptions, so Pay Later is owned by one module and reused
/// across checkout scenarios.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.PayLater")]
public sealed class PayLaterStartup : StartupBase
{
    /// <summary>
    /// Configures the Pay Later payment method for subscription checkout.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
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

    /// <summary>
    /// Adds the Pay Later subscription checkout endpoint to the route builder.
    /// </summary>
    /// <param name="app">The application builder for the current tenant pipeline.</param>
    /// <param name="routes">The endpoint route builder to configure.</param>
    /// <param name="serviceProvider">The tenant service provider.</param>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddCreatePayLaterEndpoint();
    }
}

/// <summary>
/// Registers subscription services that create and monitor tenant onboarding flows.
/// </summary>
[Feature(SubscriptionConstants.Features.TenantOnboarding)]
public sealed class TenantOnboardingStartup : StartupBase
{
    /// <summary>
    /// Configures services for tenant onboarding subscription steps and workflow events.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TenantOnboardingPart>()
            .UseDisplayDriver<TenantOnboardingPartDisplayDriver>();

        services.AddDataMigration<TenantOnboardingMigrations>();
        services.AddScoped<ISubscriptionHandler, TenantOnboardingSubscriptionHandler>();
        services.AddScoped<IDisplayDriver<SubscriptionFlow>, TenantOnboardingStepSubscriptionFlowDisplayDriver>();
        services.AddSiteDisplayDriver<SubscriptionOnboardingSettingsDisplayDriver>();

        services.AddActivity<SubscribedTenantSetupSucceededEvent, SubscribedTenantSetupSucceededEventDisplayDriver>();
        services.AddActivity<SubscribedTenantFailedSetupEvent, SubscribedTenantFailedSetupEventDisplayDriver>();
    }
}

/// <summary>
/// Registers feature profile selection for tenant onboarding subscriptions.
/// </summary>
[Feature(SubscriptionConstants.Features.TenantOnboarding)]
[RequireFeatures("OrchardCore.Tenants.FeatureProfiles")]
public sealed class FeatureProfileTenantOnboardingStartup : StartupBase
{
    /// <summary>
    /// Configures feature profile display drivers and indexing for tenant onboarding.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TenantOnboardingPart>()
            .UseDisplayDriver<FeatureProfilesTenantOnboardingPartDisplayDriver>();

        services.AddScoped<IDisplayDriver<SubscriptionFlow>, FeatureProfileTenantOnboardingStepSubscriptionFlowDisplayDriver>();
        services.AddIndexProvider<SubscriptionTenantIndexProvider>()
            .AddDataMigration<SubscriptionTenantIndexMigrations>();
    }
}

/// <summary>
/// Registers the reCAPTCHA step for subscription flows.
/// </summary>
[Feature(SubscriptionConstants.Features.ReCaptcha)]
public sealed class ReCaptchaStartup : StartupBase
{
    /// <summary>
    /// Configures the reCAPTCHA subscription flow display driver.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<SubscriptionFlow, ReCaptchaSubscriptionFlowDisplayDriver>();
    }
}

/// <summary>
/// Replaces the default subscription tax service when the taxation feature is available.
/// </summary>
[RequireFeatures(TaxationConstants.Feature.Taxation)]
public sealed class TaxationStartup : StartupBase
{
    /// <summary>
    /// Configures the taxation-aware subscription tax service.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Replace the no-op tax service with the taxation-aware implementation. This runs only when the
        // Taxation feature is enabled, keeping the runtime dependency on taxation optional.
        services.RemoveAll<ISubscriptionTaxService>();
        services.AddScoped<ISubscriptionTaxService, SubscriptionTaxService>();
    }
}

/// <summary>
/// Registers the subscription and commerce reports contributed to the admin Reports area.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    /// <summary>
    /// Configures the report providers used by the subscriptions module.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IReport, SubscriptionRevenueReport>()
            .AddScoped<IReport, SubscriptionsDashboardReport>()
            .AddScoped<IReport, ExpiringSubscriptionsReport>()
            .AddScoped<IReport, NewSubscriptionsTrendReport>()
            .AddScoped<IReport, TaxCollectedReport>()
            .AddScoped<IReport, ProductPerformanceReport>();
    }
}

/// <summary>
/// Registers the bridge that turns a completed generic checkout into a durable subscription agreement.
/// </summary>
/// <remarks>
/// It is gated on the Checkout feature because that is what raises the completion this handler reacts to.
/// Registering it unconditionally would add a handler that can never run.
/// </remarks>
[RequireFeatures(CheckoutConstants.Features.Area)]
public sealed class CheckoutStartup : StartupBase
{
    /// <summary>
    /// Configures the checkout-driven subscription services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICheckoutHandler, SubscriptionActivationCheckoutHandler>();
    }
}

/// <summary>
/// Registers the workflow events raised as a subscription moves through its life.
/// </summary>
/// <remarks>
/// Welcoming a new subscriber, warning one whose card was declined, and asking a leaver why are reactions
/// that differ from site to site. Raising a workflow event lets the site owner build the one they want
/// instead of picking from settings that could never cover every case.
/// </remarks>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class SubscriptionWorkflowsStartup : StartupBase
{
    /// <summary>
    /// Configures the subscription lifecycle workflow events.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<SubscriptionStartedEvent, SubscriptionStartedEventDisplayDriver>();
        services.AddActivity<SubscriptionRenewedEvent, SubscriptionRenewedEventDisplayDriver>();
        services.AddActivity<SubscriptionPastDueEvent, SubscriptionPastDueEventDisplayDriver>();
        services.AddActivity<SubscriptionCanceledEvent, SubscriptionCanceledEventDisplayDriver>();
        services.AddActivity<SubscriptionExpiredEvent, SubscriptionExpiredEventDisplayDriver>();

        services.AddScoped<ISubscriptionLifecycleHandler, WorkflowSubscriptionLifecycleHandler>();
    }
}

/// <summary>
/// Registers selling Orchard Core sites through the public checkout.
/// </summary>
/// <remarks>
/// It is a separate feature from the legacy tenant onboarding because it works the other way round: the
/// checkout only records that a site was bought, and a durable job builds it. That split is what keeps a
/// slow recipe, a brief outage, or a deployment restart from leaving a paying customer with nothing.
/// </remarks>
[RequireFeatures(SubscriptionConstants.Features.Tenants)]
public sealed class TenantProvisioningStartup : StartupBase
{
    /// <summary>
    /// Configures the site-selling services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<TenantProvisioningJobMigrations>()
            .AddIndexProvider<TenantProvisioningJobIndexProvider>();

        services.AddScoped<ITenantProvisioningJobStore, TenantProvisioningJobStore>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<ICheckoutHandler, TenantProvisioningCheckoutHandler>();
        services.AddScoped<IDisplayDriver<CheckoutFlow>, TenantProvisioningStepDisplayDriver>();

        // A site that keeps serving after the customer stops paying costs the owner money indefinitely, so
        // the site's state follows the subscription's.
        services.AddScoped<ISubscriptionEntitlementApplier, TenantSubscriptionEntitlementApplier>();

        services.AddSingleton<IBackgroundTask, TenantProvisioningBackgroundTask>();
        services.AddNavigationProvider<TenantProvisioningAdminMenu>();
    }
}
