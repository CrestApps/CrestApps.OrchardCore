using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Commerce.Core;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.PointOfSale.Api;
using CrestApps.OrchardCore.PointOfSale.Deployments;
using CrestApps.OrchardCore.PointOfSale.Drivers;
using CrestApps.OrchardCore.PointOfSale.Indexes;
using CrestApps.OrchardCore.PointOfSale.Migrations;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Navigation;
using CrestApps.OrchardCore.PointOfSale.Recipes;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PointOfSale;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registers and shifts live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(PosConstants.CollectionName));
        services.AddDataMigration<PosMigrations>()
            .AddIndexProvider<RegisterIndexProvider>()
            .AddIndexProvider<RegisterShiftIndexProvider>();
        services.AddScoped<IRegisterStore, RegisterStore>();
        services.AddScoped<IRegisterShiftStore, RegisterShiftStore>();
        services.AddPersonalDataContributor<RegisterShiftPersonalDataContributor>();
        services.AddScoped<PosShiftService>();
        services.AddScoped<PosSaleService>();
        services.AddCommerceEmailSender()
            .AddCommerceEmailKind(PosReceipts.EmailKind, LocalizationSource.Create<Startup>("Register receipt"), LocalizationSource.Create<Startup>("{{ Order }}: the register sale."));

        // Cash at the till is a tender, like a gift card; a card on the counter terminal pays the rest.
        services.AddScoped<ICheckoutTenderProvider, PosCashTenderProvider>();
        services.AddScoped<PosCounterCardPaymentProvider>();
        services.AddScoped<ICheckoutPaymentProvider>(serviceProvider => serviceProvider.GetRequiredService<PosCounterCardPaymentProvider>());
        services.AddScoped<ICheckoutPaymentRefundProvider>(serviceProvider => serviceProvider.GetRequiredService<PosCounterCardPaymentProvider>());

        services.AddPermissionProvider<PosPermissionProvider>();
        services.AddNavigationProvider<PosAdminMenu>();
        services.AddDisplayDriver<Register, RegisterDisplayDriver>();
        services.AddDisplayDriver<RegisterShift, RegisterShiftDisplayDriver>();

        services.AddRecipeExecutionStep<RegistersStep>();
        services.AddScoped<IRecipeStep, RegistersRecipeStepSchema>();
        services.AddDeployment<RegistersDeploymentSource, RegistersDeploymentStep, SummaryDeploymentStepDisplayDriver<RegistersDeploymentStep>>();
        services.AddDeploymentStepSchema<RegistersDeploymentStepSchema>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapPosApi();
}
