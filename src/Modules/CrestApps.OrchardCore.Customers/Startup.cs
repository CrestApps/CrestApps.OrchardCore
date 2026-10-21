using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.BackgroundTasks;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Indexes;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Drivers;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Customers.Migrations;
using CrestApps.OrchardCore.Customers.Navigation;
using CrestApps.OrchardCore.Customers.Recipes;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Customers.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Users.Events;
using OrchardCore.Users.Handlers;

namespace CrestApps.OrchardCore.Customers;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Customers live in their own YesSql collection, which has to be declared for its documents to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CustomersConstants.CollectionName));
        services.AddDataMigration<CustomerMigrations>()
            .AddIndexProvider<CustomerIndexProvider>();

        services.AddScoped<ICustomerStore, CustomerStore>();
        services.AddScoped<ICustomerService, DefaultCustomerService>();

        // A guest's history joins their account once the account's email is verified.
        services.AddScoped<ICustomerMergeService, DefaultCustomerMergeService>();
        services.AddScoped<ILoginFormEvent, CustomerMergeLoginFormEvent>();
        services.AddScoped<IRegistrationFormEvents, CustomerMergeRegistrationFormEvents>();
        services.AddScoped<IUserEventHandler, CustomerMergeUserEventHandler>();

        services.AddScoped<CustomerAddressBook>();
        services.AddScoped<CustomerAccountPages>();

        // Sample stores add their customers through a recipe. Customers are personal data, so there is no export step.
        services.AddRecipeExecutionStep<CustomersStep>();

        // Access and erasure requests, and the retention windows for guests' personal data.
        services.AddScoped<ICustomerPrivacyService, DefaultCustomerPrivacyService>();

        // Erasing or exporting a customer covers what the other commerce modules keep about them: checkouts,
        // transactions, subscriptions, installment plans, coupon redemptions and payment-provider customers.
        services.AddScoped<ICustomerPrivacyHandler, CommercePersonalDataCustomerPrivacyHandler>();
        services.AddSingleton<IBackgroundTask, CustomerRetentionBackgroundTask>();
        services.AddSiteDisplayDriver<CustomerPrivacySettingsDisplayDriver>();
        services.AddNavigationProvider<CustomerSettingsAdminMenu>();

        // Lets a role that holds only the manage customers permission open Settings > Commerce > Customers.
        // The group is registered here, not with the checkout setting, because the privacy settings share it.
        services.AddSiteSettingsPermission(CustomersConstants.SettingsGroupId, CustomerPermissions.ManageCustomers);

        // Statements, receipts and reminders address a customer by their customer record once there is one.
        services.AddScoped<DefaultCustomerContactResolver>();
        services.Replace(ServiceDescriptor.Scoped<ICustomerContactResolver, CustomerRecordContactResolver>());

        services.AddPermissionProvider<CustomerPermissionsProvider>();
        services.AddNavigationProvider<CustomersAdminMenu>();
        services.AddNavigationProvider<CustomersAccountMenu>();
        services.AddDisplayDriver<Customer, CustomerDisplayDriver>();
    }
}

/// <summary>
/// The Contact step of an order's checkout, and its setting, when Orders is enabled.
/// </summary>
[RequireFeatures(OrderConstants.Features.Orders)]
public sealed class OrdersStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICheckoutHandler, CustomerContactCheckoutHandler>();

        // Who is buying, a business or a person, and whether they are exempt, reaches checkout tax.
        services.AddScoped<ICheckoutTaxProfileHandler, CustomerCheckoutTaxProfileHandler>();
        services.AddScoped<ICustomerMergeHandler, OrderCustomerMergeHandler>();
        services.AddScoped<OrderAnonymizer>();
        services.AddScoped<ICustomerPrivacyHandler, OrderCustomerPrivacyHandler>();
        services.AddScoped<CustomerContactStepService>();
        services.AddDisplayDriver<CheckoutFlow, CustomerContactCheckoutFlowDisplayDriver>();
        services.AddSiteDisplayDriver<CustomerCheckoutSettingsDisplayDriver>();
    }
}

/// <summary>
/// The store's own checkout fields: the Checkout details content type, its editor on the Contact step, and what was
/// entered on the order's pages.
/// </summary>
[Feature(CustomersConstants.Features.CheckoutDetails)]
public sealed class CheckoutDetailsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<CheckoutDetailsMigrations>();
        services.AddScoped<CheckoutDetailsForm>();
        services.AddScoped<ICheckoutHandler, CheckoutDetailsCheckoutHandler>();
        services.AddDisplayDriver<CheckoutFlow, CheckoutDetailsCheckoutFlowDisplayDriver>();
        services.AddDisplayDriver<Order, OrderCheckoutDetailsDisplayDriver>();
    }
}

/// <summary>
/// Store credit: given to customers by hand or as a refund, and paying toward their checkouts after tax.
/// </summary>
[Feature(CustomersConstants.Features.StoreCredit)]
public sealed class StoreCreditStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<StoreCreditMigrations>()
            .AddIndexProvider<Indexes.StoreCreditAccountIndexProvider>();
        services.AddScoped<IStoreCreditStore, StoreCreditStore>();
        services.AddScoped<StoreCreditService>();
        services.AddScoped<StoreCreditTenderProvider>();
        services.AddScoped<ICheckoutTenderProvider>(serviceProvider => serviceProvider.GetRequiredService<StoreCreditTenderProvider>());
        services.AddScoped<Orders.Core.Services.IOrderStoreCreditRefunder>(serviceProvider => serviceProvider.GetRequiredService<StoreCreditTenderProvider>());
        services.AddDisplayDriver<CheckoutFlow, StoreCreditCheckoutFlowDisplayDriver>();
        services.AddDisplayDriver<Customer, CustomerStoreCreditDisplayDriver>();
        services.AddNavigationProvider<StoreCreditAccountMenu>();
    }
}

/// <summary>
/// Loyalty points: earned on orders, taken back on cancels and refunds, and spent toward checkouts after tax.
/// </summary>
[Feature(CustomersConstants.Features.Loyalty)]
public sealed class LoyaltyStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<LoyaltyMigrations>()
            .AddIndexProvider<Indexes.LoyaltyAccountIndexProvider>();
        services.AddScoped<ILoyaltyStore, LoyaltyStore>();
        services.AddScoped<LoyaltyService>();
        services.AddScoped<LoyaltyTenderProvider>();
        services.AddScoped<ICheckoutTenderProvider>(serviceProvider => serviceProvider.GetRequiredService<LoyaltyTenderProvider>());
        services.AddScoped<LoyaltyOrderEventHandler>();
        services.AddScoped<Orders.Core.Services.IOrderEventHandler>(serviceProvider => serviceProvider.GetRequiredService<LoyaltyOrderEventHandler>());
        services.AddDisplayDriver<CheckoutFlow, LoyaltyCheckoutFlowDisplayDriver>();
        services.AddDisplayDriver<Customer, CustomerLoyaltyDisplayDriver>();
        services.AddSiteDisplayDriver<LoyaltySettingsDisplayDriver>();
        services.AddSiteSettingsPermission(CustomersConstants.LoyaltySettingsGroupId, CustomerPermissions.ManageCustomers);
        services.AddNavigationProvider<LoyaltyAccountMenu>();
        services.AddNavigationProvider<LoyaltySettingsAdminMenu>();
    }
}

/// <summary>
/// Referrals: a link per customer, a discount for the friend, and a reward for the customer on the friend's first order.
/// </summary>
[Feature(CustomersConstants.Features.Referrals)]
public sealed class ReferralsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<ReferralMigrations>()
            .AddIndexProvider<Indexes.ReferralProfileIndexProvider>();
        services.AddScoped<IReferralStore, ReferralStore>();
        services.AddScoped<ReferralService>();
        services.AddScoped<ICheckoutHandler, ReferralCheckoutHandler>();
        services.AddScoped<ReferralOrderEventHandler>();
        services.AddScoped<Orders.Core.Services.IOrderEventHandler>(serviceProvider => serviceProvider.GetRequiredService<ReferralOrderEventHandler>());
        services.AddSiteDisplayDriver<ReferralSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(CustomersConstants.ReferralSettingsGroupId, CustomerPermissions.ManageCustomers);
        services.AddNavigationProvider<ReferralAccountMenu>();
        services.AddNavigationProvider<ReferralSettingsAdminMenu>();
    }
}

[Feature(CustomersConstants.Features.Api)]
[RequireFeatures(OrderConstants.Features.Orders, CheckoutConstants.Features.Api)]
public sealed class CheckoutApiStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapCustomerCheckoutApi();
}

[Feature(CustomersConstants.Features.Api)]
[RequireFeatures(CustomersConstants.Features.StoreCredit, CheckoutConstants.Features.Api)]
public sealed class StoreCreditApiStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapStoreCreditCheckoutApi();
}

[Feature(CustomersConstants.Features.Api)]
[RequireFeatures(CustomersConstants.Features.Loyalty, CheckoutConstants.Features.Api)]
public sealed class LoyaltyApiStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapLoyaltyCheckoutApi();
}

[Feature(CustomersConstants.Features.Api)]
public sealed class AccountApiStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapCustomerAccountApi();
        routes.MapCustomerAdminApi();
    }
}

/// <summary>
/// Tells webhooks about new customers when the Commerce Webhooks feature is on.
/// </summary>
[RequireFeatures(CommerceConstants.Features.Webhooks)]
public sealed class WebhooksStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICustomerHandler, CustomerWebhookHandler>();
        services.AddCommerceWebhookEvent(CommerceWebhookEvents.CustomerCreated, "Customer created", "Customers");
    }
}
