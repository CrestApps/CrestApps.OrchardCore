using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.CrestApps.Subscriptions.Core.Indexes;
using OrchardCore.CrestApps.Subscriptions.Core.Models;
using OrchardCore.CrestApps.Subscriptions.Drivers;
using OrchardCore.CrestApps.Subscriptions.Migrations;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace OrchardCore.CrestApps.Subscriptions;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<SubscriptionsPartMigrations>()
            .AddContentPart<SubscriptionsPart>()
            .UseDisplayDriver<SubscriptionsPartDisplayDriver>();

        // TO DO, create migration
        services.AddIndexProvider<SubscriptionsContentItemIndexProvider>();
    }
}
