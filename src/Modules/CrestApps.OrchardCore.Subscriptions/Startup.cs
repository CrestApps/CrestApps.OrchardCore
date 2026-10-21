using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.Indexes;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Navigation;
using CrestApps.OrchardCore.Subscriptions.Reports;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.Tasks;
using CrestApps.OrchardCore.Subscriptions.Workflows.Drivers;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Navigation;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Documents;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Registers the subscription domain: the plans that can be sold, the durable agreements that result, and
/// the lifecycle that keeps them honest.
/// </summary>
/// <remarks>
/// Subscriptions deliberately owns no checkout of its own. Buying a plan runs through the Checkout feature
/// like every other purchase, so there is exactly one path that moves money and exactly one ledger that
/// records it.
/// </remarks>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<SubscriptionPartMigrations>()
            .AddContentPart<SubscriptionPart>()
            .UseDisplayDriver<SubscriptionPartDisplayDriver>();

        // Automatically inject the SubscriptionPart and the ProductPart into any content type that uses the
        // Subscription stereotype, so administrators only need to set the stereotype.
        services.AddScoped<IContentDefinitionHandler, SubscriptionPartContentTypeDefinitionHandler>();

        // Plans are listed by the Product Listing feature like any other product; this gives them their billing
        // period and their position, for the "Recommended" sort. It is unused when the feature is off.
        services.AddScoped<SubscriptionProductSearchDocumentContributor>();
        services.AddScoped<IProductSearchDocumentContributor>(serviceProvider => serviceProvider.GetRequiredService<SubscriptionProductSearchDocumentContributor>());

        services.AddDataMigration<SubscriptionSummaryWidgetMigrations>()
            .AddContentPart<SubscriptionSummaryPart>()
            .UseDisplayDriver<SubscriptionSummaryPartDisplayDriver>();

        services.AddDataMigration<SubscriptionsContentItemIndexMigrations>()
            .AddScopedIndexProvider<SubscriptionsContentItemIndexProvider>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, SubscriptionPartSettingsDisplayDriver>();

        services.AddScoped<IAuthorizationHandler, SubscriptionsPermissionsHandler>();

        services.AddSiteDisplayDriver<SubscriptionSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(SubscriptionSettingsDisplayDriver.GroupId, SubscriptionPermissions.ManageSubscriptionSettings);
        services.AddPermissionProvider<SubscriptionPermissionsProvider>();
        services.AddNavigationProvider<SubscriptionsAdminMenu>();
        services.AddNavigationProvider<SubscriptionsAccountMenu>();
        services.AddScoped<SubscriptionDisplayText>();

        // The rows of the agreements report and of the customer's own plans.
        services
            .AddDisplayDriver<SubscriptionAgreement, SubscriptionAgreementDisplayDriver>()
            .AddDisplayDriver<CustomerSubscription, CustomerSubscriptionDisplayDriver>();

        // The agreement lives in its own YesSql collection, which has to be declared here. Declaring only
        // the index leaves the collection's document table uncreated, and every read or write against it
        // then fails at runtime with "no such table" even though the migration ran clean.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(SubscriptionConstants.SubscriptionCollectionName));

        // The durable subscription agreement. It is what survives the checkout that created it, so every
        // later question about who is subscribed is answered from here rather than from a session.
        services.AddDataMigration<SubscriptionRecordMigrations>()
            .AddIndexProvider<SubscriptionRecordIndexProvider>();

        services.AddScoped<ISubscriptionStore, SubscriptionStore>();
        services.AddScoped<ISubscriptionManager, SubscriptionManager>();
        services.AddScoped<ISubscriptionLifecycleService, DefaultSubscriptionLifecycleService>();
        services.AddScoped<ISubscriptionAccessService, DefaultSubscriptionAccessService>();
        services.AddScoped<ISubscriptionLifecycleHandler, EntitlementSubscriptionLifecycleHandler>();

        // A cancellation that never reaches the gateway keeps billing the customer, so this is registered
        // wherever agreements are, not only where a gateway happens to be configured.
        services.AddScoped<ISubscriptionLifecycleHandler, GatewayBillingSubscriptionLifecycleHandler>();
        services.AddSingleton<IBackgroundTask, SubscriptionLifecycleBackgroundTask>();

        // The gateway is authoritative for a recurring agreement, so its notifications are what keep the
        // local record honest about renewals, failures, and cancellations made outside this application.
        services.AddScoped<IPaymentEvent, SubscriptionRecordPaymentEventHandler>();
        services.AddScoped<ICommercePersonalDataContributor, SubscriptionPersonalDataContributor>();

        services.AddDisplayDriver<SubscriptionRegisterUserForm, SubscriptionRegisterUserFormDisplayDriver>();
    }
}

/// <summary>
/// Registers subscription settings that depend on the Orchard Core roles feature.
/// </summary>
[RequireFeatures("OrchardCore.Roles")]
public sealed class RolesStartup : StartupBase
{
    /// <inheritdoc/>
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
/// Registers everything that turns a checkout into a subscription: the plan being sold, the optional steps
/// a plan collects, and the durable agreement a completed checkout produces.
/// </summary>
/// <remarks>
/// It is gated on the Checkout feature because that is what raises the events these handlers react to.
/// Registering them unconditionally would add handlers that could never run, and would offer a signup link
/// that leads nowhere.
/// </remarks>
[RequireFeatures(CheckoutConstants.Features.Area)]
public sealed class CheckoutStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICheckoutHandler, SubscriptionPlanCheckoutHandler>();
        services.AddScoped<ICheckoutHandler, UserRegistrationCheckoutHandler>();
        services.AddScoped<ICheckoutHandler, ContentCheckoutHandler>();
        services.AddScoped<ICheckoutHandler, SubscriptionActivationCheckoutHandler>();

        services.AddDisplayDriver<CheckoutFlow, UserRegistrationCheckoutFlowDisplayDriver>();
        services.AddDisplayDriver<CheckoutFlow, ContentStepCheckoutFlowDisplayDriver>();
    }
}

/// <summary>
/// Registers the subscription and commerce reports contributed to the admin Reports area.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddReport<SubscriptionRevenueReport>()
            .AddReport<SubscriptionsDashboardReport>()
            .AddReport<ExpiringSubscriptionsReport>()
            .AddReport<NewSubscriptionsTrendReport>()
            .AddReport<TaxCollectedReport>()
            .AddReport<ProductPerformanceReport>();
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
    /// <inheritdoc/>
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
/// Registers selling Orchard Core sites through the checkout.
/// </summary>
/// <remarks>
/// The checkout only records that a site was bought; a durable job builds it. That split is what keeps a
/// slow recipe, a brief outage, or a deployment restart from leaving a paying customer with nothing.
/// </remarks>
[Feature(SubscriptionConstants.Features.Tenants)]
public sealed class TenantProvisioningStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TenantOnboardingPart>()
            .UseDisplayDriver<TenantOnboardingPartDisplayDriver>();

        services.AddDataMigration<TenantOnboardingMigrations>();
        services.AddSiteDisplayDriver<SubscriptionOnboardingSettingsDisplayDriver>();

        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(SubscriptionConstants.TenantProvisioningCollectionName));

        services.AddDataMigration<TenantProvisioningJobMigrations>()
            .AddIndexProvider<TenantProvisioningJobIndexProvider>();

        services.AddScoped<ITenantProvisioningJobStore, TenantProvisioningJobStore>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<ICheckoutHandler, TenantProvisioningCheckoutHandler>();
        services.AddDisplayDriver<CheckoutFlow, TenantProvisioningStepDisplayDriver>();

        // A site that keeps serving after the customer stops paying costs the owner money indefinitely, so
        // the site's state follows the subscription's.
        services.AddScoped<ISubscriptionEntitlementApplier, TenantSubscriptionEntitlementApplier>();

        services.AddSingleton<IBackgroundTask, TenantProvisioningBackgroundTask>();
        services.AddNavigationProvider<TenantProvisioningAdminMenu>();

        // The rows of the site provisioning list and of the customer's own sites.
        services
            .AddDisplayDriver<TenantProvisioningJob, TenantProvisioningJobDisplayDriver>()
            .AddDisplayDriver<CustomerSite, CustomerSiteDisplayDriver>();

        services.AddActivity<SubscribedTenantSetupSucceededEvent, SubscribedTenantSetupSucceededEventDisplayDriver>();
        services.AddActivity<SubscribedTenantFailedSetupEvent, SubscribedTenantFailedSetupEventDisplayDriver>();
    }
}

/// <summary>
/// Registers feature profile selection for the sites a subscription provisions.
/// </summary>
[Feature(SubscriptionConstants.Features.Tenants)]
[RequireFeatures("OrchardCore.Tenants.FeatureProfiles")]
public sealed class FeatureProfileTenantProvisioningStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<TenantOnboardingPart>()
            .UseDisplayDriver<FeatureProfilesTenantOnboardingPartDisplayDriver>();
    }
}

/// <summary>
/// Registers installment plans: payment plans an administrator sets up for a customer, collected through the
/// checkout and recorded on the transaction ledger.
/// </summary>
[Feature(SubscriptionConstants.Features.Installments)]
public sealed class InstallmentPlansStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlansStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InstallmentPlansStartup(IStringLocalizer<InstallmentPlansStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransactionSource(SubscriptionConstants.InstallmentPlans.TransactionSource, source =>
        {
            source.DisplayName = S["Installment plan"];
            source.Description = S["Down payments and scheduled payments of installment plans."];
        });

        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(SubscriptionConstants.InstallmentPlanCollectionName));

        services.AddDataMigration<InstallmentPlanMigrations>()
            .AddIndexProvider<InstallmentPlanIndexProvider>();

        services.AddScoped<IInstallmentPlanStore, InstallmentPlanStore>();
        services.AddScoped<IInstallmentPlanService, DefaultInstallmentPlanService>();
        services.AddScoped<ITransactionPaymentHandler, InstallmentPlanTransactionPaymentHandler>();
        services.AddScoped<ICommercePersonalDataContributor, InstallmentPlanPersonalDataContributor>();

        // The "Choose how to pay" step of a checkout that pays an outstanding transaction. The options come from the
        // providers; the one built in offers what the settings page configures.
        services.AddScoped<InstallmentOptionResolver>();
        services.AddScoped<IInstallmentOptionProvider, SiteSettingsInstallmentOptionProvider>();
        services.AddScoped<ICheckoutHandler, InstallmentOptionsCheckoutHandler>();
        services.AddDisplayDriver<CheckoutFlow, InstallmentOptionsCheckoutFlowDisplayDriver>();

        services.AddSiteDisplayDriver<InstallmentPlanSettingsDisplayDriver>();
        services.AddNavigationProvider<InstallmentPlansAdminMenu>();
        services.AddPermissionProvider<InstallmentPlanPermissionsProvider>();
        services.AddDisplayDriver<InstallmentPlan, InstallmentPlanDisplayDriver>();

        // Lets a role that holds only the installment plans permission open Settings > Installment Plans.
        services.AddSiteSettingsPermission(SubscriptionConstants.InstallmentPlans.SettingsGroupId, SubscriptionPermissions.ManageInstallmentPlans);

        services.AddSingleton<IBackgroundTask, InstallmentPlanBackgroundTask>();
    }
}
