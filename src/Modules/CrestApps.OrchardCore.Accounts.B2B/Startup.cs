using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Accounts.B2B.Api;
using CrestApps.OrchardCore.Accounts.B2B.Drivers;
using CrestApps.OrchardCore.Accounts.B2B.Handlers;
using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using CrestApps.OrchardCore.Accounts.B2B.Migrations;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Navigation;
using CrestApps.OrchardCore.Accounts.B2B.Deployments;
using CrestApps.OrchardCore.Accounts.B2B.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using OrchardCore.Deployment;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Contents.Services;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Accounts.B2B;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The business settings are a part on the account; price lists live in their own YesSql collection, which has
        // to be declared to be stored.
        services.AddContentPart<B2BAccountPart>()
            .UseDisplayDriver<B2BAccountPartDisplayDriver>();
        services.AddScoped<IContentDisplayDriver, B2BAccountCardsDisplayDriver>();
        services.AddTransient<IContentsAdminListFilterProvider, B2BAccountsContentsAdminListFilterProvider>();
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(B2BAccountsConstants.PriceListsCollectionName));
        services.AddDataMigration<B2BAccountMigrations>()
            .AddIndexProvider<B2BAccountIndexProvider>()
            .AddIndexProvider<PriceListIndexProvider>();
        services.AddScoped<IB2BAccountStore, B2BAccountStore>();
        services.AddScoped<IPriceListStore, PriceListStore>();
        services.AddScoped<IBuyerPriceProvider, B2BPriceProvider>();
        services.AddScoped<B2BAccountService>();
        services.AddScoped<IOrderEventHandler, B2BOrderHandler>();

        // An account's terms are a tender, like a gift card: they pay what the deposit doesn't, on an invoice due later.
        services.AddScoped<AccountTermsService>();
        services.AddScoped<ICheckoutTenderProvider, AccountTermsTenderProvider>();
        services.AddDisplayDriver<CheckoutFlow, AccountTermsCheckoutFlowDisplayDriver>();

        // The Business account step: the purchase order number, before payment.
        services.AddScoped<B2BCheckoutStepService>();
        services.AddScoped<B2BApprovalService>();
        services.AddScoped<PunchoutService>();
        services.AddScoped<ICartActionProvider, PunchoutCartActionProvider>();
        services.AddScoped<ICheckoutPaymentGate, B2BApprovalPaymentGate>();
        services.AddScoped<ICheckoutHandler, B2BCheckoutHandler>();
        services.AddDisplayDriver<CheckoutFlow, B2BCheckoutStepDisplayDriver>();

        services.AddPermissionProvider<B2BAccountsPermissionProvider>();
        services.AddNavigationProvider<B2BAccountsAdminMenu>();
        services.AddNavigationProvider<BusinessAccountMenu>();
        services.AddDisplayDriver<PriceList, PriceListDisplayDriver>();
    }
}

[RequireFeatures(CheckoutConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapB2BAccountApi();
}

/// <summary>
/// Registers the price list recipe step.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<PriceListsStep>();
}

/// <summary>
/// Registers the price list deployment step.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<PriceListsDeploymentSource, PriceListsDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, PriceListsDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Describes the price list recipe and deployment steps to CrestApps Recipes, so they can be checked and written with
/// help.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, PriceListsRecipeStepSchema>();
        services.AddDeploymentStepSchema<PriceListsDeploymentStepSchema>();
    }
}
