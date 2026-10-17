using CrestApps.OrchardCore.Reports.Designer.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Reports.Designer.Migrations;

/// <summary>
/// Creates the index table of the stored results of scheduled views.
/// </summary>
internal sealed class ReportViewSnapshotMigrations : DataMigration
{
    // View identifiers are generated ids, or names of at most 64 characters when they come from a recipe.
    private const int ViewIdLength = 64;

    /// <summary>
    /// Creates the index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ReportViewSnapshotIndex>(table => table
            .Column<string>(nameof(ReportViewSnapshotIndex.ViewId), column => column.WithLength(ViewIdLength))
            .Column<DateTime>(nameof(ReportViewSnapshotIndex.RefreshedUtc), column => column.Nullable())
            .Column<DateTime>(nameof(ReportViewSnapshotIndex.AttemptedUtc))
            .Column<int>(nameof(ReportViewSnapshotIndex.RowCount))
            .Column<string>(nameof(ReportViewSnapshotIndex.LastError), column => column.Nullable().WithLength(ReportViewSnapshotIndexProvider.MaxErrorLength))
            .Column<DateTime>(nameof(ReportViewSnapshotIndex.LastErrorUtc), column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<ReportViewSnapshotIndex>(table => table
            .CreateIndex("IDX_ReportViewSnapshotIndex_ViewId", "DocumentId", nameof(ReportViewSnapshotIndex.ViewId)));

        return 1;
    }
}
