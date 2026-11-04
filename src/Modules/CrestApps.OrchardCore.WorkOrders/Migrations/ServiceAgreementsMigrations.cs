using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.WorkOrders.Migrations;

/// <summary>
/// Creates the service agreement table.
/// </summary>
internal sealed class ServiceAgreementsMigrations : DataMigration
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await ServiceAgreementsIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    /// <summary>
    /// Adds the CRM account column, which replaced the Companies module's company.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ServiceAgreementIndex>(table =>
        {
            table.AddColumn<string>("AccountId", column => column.Nullable().WithLength(64));
            table.CreateIndex("IDX_ServiceAgreementIndex_Account", "AccountId");
        }, collection: WorkOrdersConstants.CollectionName);

        return 2;
    }
}
