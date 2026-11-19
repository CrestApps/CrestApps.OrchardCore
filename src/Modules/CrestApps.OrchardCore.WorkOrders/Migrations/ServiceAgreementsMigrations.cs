using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.WorkOrders.Migrations;

/// <summary>
/// Service agreements moved to the Agreements module: a new site keeps nothing here; an older one moves its agreements
/// there, with the same ids and numbers, in a step that runs after the migration commits and again until every one has
/// moved (<see cref="ServiceAgreementsMoveStep"/>), and gives the roles that managed service agreements the agreement
/// permission. Every step is an instance method, which is how Orchard Core finds a migration's steps.
/// </summary>
internal sealed class ServiceAgreementsMigrations : DataMigration
{
    private readonly ResumableSteps _steps;

    public ServiceAgreementsMigrations(ResumableSteps steps)
    {
        _steps = steps;
    }

    /// <summary>
    /// Nothing to create: agreements are the Agreements module's. A site whose migration record was lost still runs the
    /// move, which finds nothing on a new site.
    /// </summary>
    /// <returns>The version.</returns>
    public Task<int> CreateAsync()
        => RequestMoveAsync(3);

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

    /// <summary>
    /// Moves the service agreements to the Agreements module, in a step that runs once the migration has committed (the
    /// agreement store is read in a scope that sees the Agreements tables) and again until every one has moved.
    /// </summary>
    /// <returns>The version.</returns>
    public Task<int> UpdateFrom2Async()
        => RequestMoveAsync(3);

    // The request is a row written in the migration's own transaction, so it can't be lost after the migration commits;
    // the step itself runs later, in a scope that sees the Agreements tables.
    private async Task<int> RequestMoveAsync(int version)
    {
        await _steps.RequestAsync(ServiceAgreementsMoveStep.StepName);

        return version;
    }
}
