using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Reports.Subscriptions.Services;

/// <summary>
/// Creates the index of report subscriptions.
/// </summary>
public sealed class ReportSubscriptionMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ReportSubscriptionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("ReportName", column => column.WithLength(255))
            .Column<bool>("IsEnabled")
            .Column<DateTime>("NextRunUtc", column => column.Nullable())
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<ReportSubscriptionIndex>(SchemaBuilder, "IDX_ReportSubscriptionIndex_Due", null, "IsEnabled", "NextRunUtc");

        return 1;
    }
}
