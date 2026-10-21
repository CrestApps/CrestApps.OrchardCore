using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Fundraising.Drivers;
using CrestApps.OrchardCore.Fundraising.Handlers;
using CrestApps.OrchardCore.Fundraising.Indexes;
using CrestApps.OrchardCore.Fundraising.Migrations;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Fundraising.Navigation;
using CrestApps.OrchardCore.Fundraising.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Fundraising;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Donations live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(FundraisingConstants.CollectionName));
        services.AddDataMigration<FundraisingMigrations>()
            .AddIndexProvider<DonationIndexProvider>();
        services.AddScoped<IDonationStore, DonationStore>();
        services.AddCommerceEmailSender();
        services.AddScoped<DonationService>();

        // The amount a donor chose rides on its cart line, and the paid order records it.
        services.AddScoped<ICartLineHandler, DonationLineHandler>();
        services.AddScoped<IOrderEventHandler, DonationOrderHandler>();
        services.Configure<CartOptions>(options => options.ChooseOnProductPageParts.Add(nameof(FundraisingCampaignPart)));

        services.AddContentPart<FundraisingCampaignPart>()
            .UseDisplayDriver<FundraisingCampaignPartDisplayDriver>();
        services.AddSiteDisplayDriver<FundraisingSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(FundraisingConstants.SettingsGroupId, FundraisingPermissions.ManageFundraising);

        services.AddPermissionProvider<FundraisingPermissionProvider>();
        services.AddNavigationProvider<FundraisingAdminMenu>();
        services.AddNavigationProvider<FundraisingAccountMenu>();
    }
}
