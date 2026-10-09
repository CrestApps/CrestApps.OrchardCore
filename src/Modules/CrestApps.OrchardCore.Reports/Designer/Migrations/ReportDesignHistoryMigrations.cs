using CrestApps.OrchardCore.Reports.Designer.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Reports.Designer.Migrations;

/// <summary>
/// Creates the index tables of report drafts and versions.
/// </summary>
internal sealed class ReportDesignHistoryMigrations : DataMigration
{
    // Report identifiers are generated ids, or names of at most 64 characters when they come from a recipe.
    private const int DesignIdLength = 64;

    /// <summary>
    /// Creates the index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ReportDesignDraftIndex>(table => table
            .Column<string>(nameof(ReportDesignDraftIndex.DesignId), column => column.WithLength(DesignIdLength)));

        await SchemaBuilder.AlterIndexTableAsync<ReportDesignDraftIndex>(table => table
            .CreateIndex("IDX_ReportDesignDraftIndex_DesignId", "DocumentId", nameof(ReportDesignDraftIndex.DesignId)));

        await SchemaBuilder.CreateMapIndexTableAsync<ReportDesignVersionIndex>(table => table
            .Column<string>(nameof(ReportDesignVersionIndex.DesignId), column => column.WithLength(DesignIdLength))
            .Column<int>(nameof(ReportDesignVersionIndex.Number))
            .Column<string>(nameof(ReportDesignVersionIndex.DisplayText), column => column.Nullable().WithLength(ReportDesignVersionIndexProvider.MaxDisplayTextLength))
            .Column<DateTime>(nameof(ReportDesignVersionIndex.CreatedUtc))
            .Column<string>(nameof(ReportDesignVersionIndex.CreatedByName), column => column.Nullable().WithLength(ReportDesignVersionIndexProvider.MaxDisplayTextLength))
            .Column<int>(nameof(ReportDesignVersionIndex.RestoredFrom), column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<ReportDesignVersionIndex>(table => table
            .CreateIndex("IDX_ReportDesignVersionIndex_DesignId", "DocumentId", nameof(ReportDesignVersionIndex.DesignId), nameof(ReportDesignVersionIndex.Number)));

        return 1;
    }

    /// <summary>
    /// Adds the columns that find reports that were never published.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ReportDesignDraftIndex>(table => table
            .AddColumn<bool>(nameof(ReportDesignDraftIndex.IsUnpublished), column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<ReportDesignDraftIndex>(table => table
            .AddColumn<string>(nameof(ReportDesignDraftIndex.OwnerId), column => column.Nullable().WithLength(DesignIdLength)));

        return 2;
    }
}
