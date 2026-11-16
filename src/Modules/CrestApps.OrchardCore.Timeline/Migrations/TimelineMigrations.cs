using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Timeline.Migrations;

/// <summary>
/// Creates the index table of timeline entries.
/// </summary>
internal sealed class TimelineMigrations : DataMigration
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await TimelineIndexSchema.CreateAsync(SchemaBuilder);

        return 3;
    }

    /// <summary>
    /// Adds the external id an entry has outside the site, such as an e-mail's message id.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TimelineEntryIndex>(table =>
        {
            table.AddColumn<string>("ExternalId", column => column.Nullable().WithLength(TimelineConstants.MaxExternalIdLength));
            table.CreateIndex("IDX_TL_Entry_External", "ExternalId");
        }, collection: TimelineConstants.CollectionName);

        return 2;
    }

    /// <summary>
    /// Indexes the files attached to entries, so a record lists all its files in one place; the entries made before are
    /// indexed by a resumable step.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await TimelineIndexSchema.CreateAttachmentIndexAsync(SchemaBuilder);

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<ResumableSteps>().RequestAsync(TimelineFileIndexStep.StepName));

        return 3;
    }
}
