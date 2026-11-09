using CrestApps.OrchardCore.Outreach.BackgroundTasks;
using CrestApps.OrchardCore.Outreach.Core.Indexes;
using CrestApps.OrchardCore.Outreach.Core.Services;
using CrestApps.OrchardCore.Outreach.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Outreach;

/// <summary>
/// Registers the outreach engine, its storage and the task that sends due batches.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Runs and batches live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(OutreachConstants.CollectionName));
        services.AddDataMigration<OutreachMigrations>()
            .AddIndexProvider<OutreachRunIndexProvider>()
            .AddIndexProvider<OutreachBatchIndexProvider>();
        services.AddOutreachEngine();
        services.AddRetentionPolicy<OutreachBatchRetentionPolicy>();
        services.AddSingleton<IBackgroundTask, OutreachBackgroundTask>();
    }
}
