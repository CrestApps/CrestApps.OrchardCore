using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Companies.Api;
using CrestApps.OrchardCore.Companies.Drivers;
using CrestApps.OrchardCore.Companies.Handlers;
using CrestApps.OrchardCore.Companies.Indexes;
using CrestApps.OrchardCore.Companies.Migrations;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Companies.Navigation;
using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Companies;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Companies live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CompaniesConstants.CollectionName));
        services.AddDataMigration<CompanyMigrations>()
            .AddIndexProvider<CompanyIndexProvider>()
            .AddIndexProvider<PriceListIndexProvider>();
        services.AddScoped<ICompanyStore, CompanyStore>();
        services.AddScoped<IPriceListStore, PriceListStore>();
        services.AddScoped<IBuyerPriceProvider, CompanyPriceProvider>();
        services.AddScoped<CompanyService>();
        services.AddScoped<IOrderEventHandler, CompanyOrderHandler>();

        // A company's terms are a tender, like a gift card: they pay what the deposit doesn't, on an invoice due later.
        services.AddScoped<CompanyTermsService>();
        services.AddScoped<ICheckoutTenderProvider, CompanyTermsTenderProvider>();
        services.AddDisplayDriver<CheckoutFlow, CompanyTermsCheckoutFlowDisplayDriver>();

        // The Company step: the purchase order number, before payment.
        services.AddScoped<CompanyCheckoutStepService>();
        services.AddScoped<CompanyApprovalService>();
        services.AddScoped<PunchoutService>();
        services.AddScoped<ICartActionProvider, PunchoutCartActionProvider>();
        services.AddScoped<ICheckoutPaymentGate, CompanyApprovalPaymentGate>();
        services.AddScoped<ICheckoutHandler, CompanyCheckoutHandler>();
        services.AddDisplayDriver<CheckoutFlow, CompanyCheckoutStepDisplayDriver>();

        services.AddPermissionProvider<CompaniesPermissionProvider>();
        services.AddNavigationProvider<CompaniesAdminMenu>();
        services.AddNavigationProvider<CompanyAccountMenu>();
        services.AddDisplayDriver<Company, CompanyDisplayDriver>();
        services.AddDisplayDriver<PriceList, PriceListDisplayDriver>();
    }
}

[RequireFeatures(CheckoutConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapCompanyApi();
}
