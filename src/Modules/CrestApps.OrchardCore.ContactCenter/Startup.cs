using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the services and configuration for the base Contact Center feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ContactCenterConstants.CollectionName));

        services
            .AddScoped<IInteractionStore, InteractionStore>()
            .AddScoped<IInteractionManager, InteractionManager>()
            .AddScoped<IInteractionEventStore, InteractionEventStore>()
            .AddScoped<IContactCenterEventPublisher, DefaultContactCenterEventPublisher>()
            .AddScoped<ICatalogEntryHandler<Interaction>, InteractionHandler>();

        services
            .AddIndexProvider<InteractionIndexProvider>()
            .AddDataMigration<InteractionIndexMigrations>();

        services
            .AddIndexProvider<InteractionEventIndexProvider>()
            .AddDataMigration<InteractionEventIndexMigrations>();

        services.AddPermissionProvider<ContactCenterPermissionProvider>();
    }
}
