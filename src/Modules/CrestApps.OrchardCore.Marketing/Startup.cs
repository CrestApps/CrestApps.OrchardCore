using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Marketing.BackgroundTasks;
using CrestApps.OrchardCore.Marketing.Drivers;
using CrestApps.OrchardCore.Marketing.Handlers;
using CrestApps.OrchardCore.Marketing.Indexes;
using CrestApps.OrchardCore.Marketing.Migrations;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Navigation;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Marketing;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Purchases, segments and campaigns live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(MarketingConstants.CollectionName));
        services.AddDataMigration<MarketingMigrations>()
            .AddIndexProvider<MarketingIndexProvider>()
            .AddIndexProvider<SegmentIndexProvider>()
            .AddIndexProvider<CampaignIndexProvider>();
        services.AddScoped<ICustomerPurchasesStore, CustomerPurchasesStore>();
        services.AddScoped<ISegmentStore, SegmentStore>();
        services.AddScoped<ICampaignStore, CampaignStore>();
        services.AddScoped<CustomerPurchasesService>();
        services.AddSignedLinks();
        services.AddScoped<CampaignService>();
        services.AddScoped<IOrderEventHandler, CustomerPurchasesOrderHandler>();
        services.AddSingleton<IBackgroundTask, MarketingBackgroundTask>();

        // A campaign's Liquid reads its customer.
        services.Configure<TemplateOptions>(options => options.MemberAccessStrategy.Register<CampaignCustomer>());

        services.AddPermissionProvider<MarketingPermissionProvider>();
        services.AddNavigationProvider<MarketingAdminMenu>();
        services.AddDisplayDriver<Segment, SegmentDisplayDriver>();
        services.AddDisplayDriver<Campaign, CampaignDisplayDriver>();
    }
}
