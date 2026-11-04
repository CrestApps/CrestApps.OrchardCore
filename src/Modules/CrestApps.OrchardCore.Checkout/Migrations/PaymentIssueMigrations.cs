using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checkout.Migrations;

/// <summary>
/// Creates the index of payment issues: money operations that failed and must be finished by hand, listed newest first
/// and filtered by whether they are still open.
/// </summary>
internal sealed class PaymentIssueMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PaymentIssueIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Kind", column => column.Nullable().WithLength(64))
            .Column<PaymentIssueStatus>("Status")
            .Column<DateTime>("CreatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentIssueIndex>(table =>
        {
            table.CreateIndex("IDX_PaymentIssueIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PaymentIssueIndex_Status", "Status", "CreatedUtc");
        });

        return 1;
    }
}
