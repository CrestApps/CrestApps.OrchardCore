using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql.Sql;

namespace CrestApps.OrchardCore.WorkOrders.Migrations;

/// <summary>
/// Creates the work order index tables.
/// </summary>
internal sealed class WorkOrdersMigrations : DataMigration
{
    private readonly WorkOrderServiceLevelUpgrade _upgrade;
    private readonly ResumableSteps _steps;

    public WorkOrdersMigrations(WorkOrderServiceLevelUpgrade upgrade, ResumableSteps steps)
    {
        _upgrade = upgrade;
        _steps = steps;
    }

    /// <summary>
    /// Creates the tables.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await WorkOrdersIndexSchema.CreateAsync(SchemaBuilder);

        return 6;
    }

    /// <summary>
    /// Adds the CRM account column, which replaced the Companies module's company.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<WorkOrderIndex>(table =>
        {
            table.AddColumn<string>("AccountId", column => column.Nullable().WithLength(64));
            table.CreateIndex("IDX_WorkOrderIndex_Account", "AccountId");
        }, collection: WorkOrdersConstants.CollectionName);

        return 2;
    }

    /// <summary>
    /// Moves the policies and clocks Work Orders kept to the shared Service Levels block, which this feature depends on,
    /// so its tables exist by now.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await _upgrade.CopyPoliciesAsync();

        // The open work orders get their timers in the background, a few batches a minute, until none is left: not in
        // the tenant's activation, where a large store would hold the activation lock and a failure would be final.
        await _steps.RequestAsync(WorkOrderServiceLevelTimersStep.StepName);

        return 3;
    }

    /// <summary>
    /// Moves the notes stored on work orders to their timelines, which this feature depends on.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        // The notes move in the background, a few batches a minute, until none is left, after the Timeline tables
        // created in the same activation have committed.
        await _steps.RequestAsync(WorkOrderNotesStep.StepName);

        return 4;
    }

    /// <summary>
    /// Keeps the service level policies page for the roles that managed it as part of the work order settings: the
    /// policies moved to the shared Service Levels block, which has its own permission.
    /// </summary>
    /// <returns>The migration version.</returns>
    public static int UpdateFrom4()
    {
        // The roles are documents; they are written once this step's own transaction has committed.
        ShellScope.AddDeferredTask(scope => RolePermissionMigrations.GrantAsync(
            scope.ServiceProvider,
            new Dictionary<string, string[]>
            {
                [WorkOrdersPermissions.ManageWorkOrderSettings.Name] = [ServiceLevelsPermissions.ManageServiceLevelPolicies.Name],
            },
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<WorkOrdersMigrations>()));

        return 5;
    }

    /// <summary>
    /// Keeps the conditions of checklist questions as shared rules (<see cref="Models.ChecklistQuestion.Rule"/>). The
    /// checklists already copied onto work orders keep the older form, which is read the same way.
    /// </summary>
    /// <returns>The migration version.</returns>
    public static int UpdateFrom5()
    {
        // The templates are documents; they are written once this step's own transaction has committed.
        ShellScope.AddDeferredTask(scope => ChecklistRuleUpgrade.ConvertAsync(
            scope.ServiceProvider.GetRequiredService<CrestApps.Core.Services.ICatalog<Models.ChecklistTemplate>>(),
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<WorkOrdersMigrations>()));

        return 6;
    }
}
