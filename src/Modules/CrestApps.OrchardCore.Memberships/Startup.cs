using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Memberships.BackgroundTasks;
using CrestApps.OrchardCore.Memberships.Drivers;
using CrestApps.OrchardCore.Memberships.Handlers;
using CrestApps.OrchardCore.Memberships.Indexes;
using CrestApps.OrchardCore.Memberships.Migrations;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Navigation;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Memberships;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Memberships live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(MembershipsConstants.CollectionName));
        services.AddDataMigration<MembershipMigrations>()
            .AddIndexProvider<MembershipIndexProvider>();
        services.AddScoped<IMembershipStore, MembershipStore>();
        services.AddScoped<MembershipService>();
        services.AddScoped<IOrderEventHandler, MembershipOrderEventHandler>();
        services.AddScoped<ICartLineHandler, MembershipCartLineHandler>();
        services.AddSingleton<IBackgroundTask, MembershipsBackgroundTask>();

        services.AddContentPart<MembershipPart>()
            .UseDisplayDriver<MembershipPartDisplayDriver>();
        services.AddDisplayDriver<Membership, MembershipDisplayDriver>();

        services.AddPermissionProvider<MembershipsPermissionProvider>();
        services.AddNavigationProvider<MembershipsAdminMenu>();
        services.AddNavigationProvider<MembershipsAccountMenu>();
    }
}
