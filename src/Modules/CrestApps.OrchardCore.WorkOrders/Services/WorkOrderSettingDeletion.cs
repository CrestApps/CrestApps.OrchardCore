using CrestApps.Core.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// The kinds of records work orders are set up with, as the Work Order Settings pages name them.
/// </summary>
internal static class WorkOrderSettingKinds
{
    /// <summary>
    /// Work order types.
    /// </summary>
    public const string Types = "types";

    /// <summary>
    /// Checklist templates.
    /// </summary>
    public const string Checklists = "checklists";

    /// <summary>
    /// Service level policies: the shared Service Levels policies of the Work orders scope.
    /// </summary>
    public const string ServiceLevels = "service-levels";

    /// <summary>
    /// Sub-statuses.
    /// </summary>
    public const string SubStatuses = "sub-statuses";

    /// <summary>
    /// Gets every kind, in the order the pages list them.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Types, Checklists, ServiceLevels, SubStatuses];
}

/// <summary>
/// What became of a request to delete a work order settings record.
/// </summary>
internal enum WorkOrderSettingDeleteOutcome
{
    /// <summary>
    /// The record doesn't exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// The record is in use and was kept: a type by work orders, any other record by work order types.
    /// </summary>
    InUse,

    /// <summary>
    /// The record was deleted.
    /// </summary>
    Deleted,
}

/// <summary>
/// The result of a request to delete a work order settings record.
/// </summary>
/// <param name="Outcome">What became of it.</param>
/// <param name="Name">The record's name, when it exists.</param>
internal sealed record WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome Outcome, string Name);

/// <summary>
/// Deletes work order settings records nothing uses, as the Work Order Settings pages do: a type while no work order is
/// of it, and a checklist while no type names it. Sub-statuses are always deleted. A service level is a shared Service
/// Levels policy, deleted as the Service Levels page deletes it.
/// </summary>
internal sealed class WorkOrderSettingDeletion
{
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderSettingManager<ChecklistTemplate> _checklists;
    private readonly IServiceLevelPolicyManager _policies;
    private readonly IEnumerable<IServiceLevelPolicyUsageProvider> _policyUsages;
    private readonly WorkOrderSettingManager<WorkOrderSubStatus> _subStatuses;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderSettingDeletion"/> class.
    /// </summary>
    /// <param name="types">The types.</param>
    /// <param name="checklists">The checklists.</param>
    /// <param name="policies">The shared service level policies.</param>
    /// <param name="policyUsages">What tells whether a shared service level policy is in use.</param>
    /// <param name="subStatuses">The sub-statuses.</param>
    /// <param name="session">The session.</param>
    public WorkOrderSettingDeletion(
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderSettingManager<ChecklistTemplate> checklists,
        IServiceLevelPolicyManager policies,
        IEnumerable<IServiceLevelPolicyUsageProvider> policyUsages,
        WorkOrderSettingManager<WorkOrderSubStatus> subStatuses,
        ISession session)
    {
        _types = types;
        _checklists = checklists;
        _policies = policies;
        _policyUsages = policyUsages;
        _subStatuses = subStatuses;
        _session = session;
    }

    /// <summary>
    /// Deletes a record of a kind when nothing uses it.
    /// </summary>
    /// <param name="kind">One of <see cref="WorkOrderSettingKinds"/>.</param>
    /// <param name="id">The record.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What became of it, or <see langword="null"/> when the kind is unknown.</returns>
    public async Task<WorkOrderSettingDeleteResult> DeleteAsync(string kind, string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            return kind is WorkOrderSettingKinds.Types or WorkOrderSettingKinds.Checklists or WorkOrderSettingKinds.SubStatuses
                ? new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.NotFound, null)
                : null;
        }

        return kind switch
        {
            WorkOrderSettingKinds.Types => await DeleteTypeAsync(id, cancellationToken),
            WorkOrderSettingKinds.Checklists => await DeleteUnusedAsync(_checklists, id, item => item.Name, type => type.ChecklistTemplateIds.Contains(id), cancellationToken),
            WorkOrderSettingKinds.SubStatuses => await DeleteUnusedAsync(_subStatuses, id, item => item.Name, _ => false, cancellationToken),
            _ => null,
        };
    }

    /// <summary>
    /// Deletes a work order service level, a shared Service Levels policy of the Work orders scope, as the Service
    /// Levels page does: not while a record of any module names it or timers still count on it.
    /// </summary>
    /// <param name="id">The policy.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What became of it, with what uses it when it was kept.</returns>
    public async Task<(WorkOrderSettingDeleteResult Result, IReadOnlyList<string> Usages)> DeletePolicyAsync(string id, CancellationToken cancellationToken = default)
    {
        var policy = string.IsNullOrEmpty(id) ? null : await _policies.FindByIdAsync(id, cancellationToken);

        if (policy is null || !string.Equals(policy.Scope, WorkOrdersConstants.ServiceLevelScope, StringComparison.OrdinalIgnoreCase))
        {
            return (new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.NotFound, null), []);
        }

        var usages = new List<string>();

        foreach (var provider in _policyUsages)
        {
            usages.AddRange(await provider.GetUsagesAsync(policy, cancellationToken));
        }

        if (usages.Count > 0)
        {
            return (new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.InUse, policy.Name), usages);
        }

        await _policies.DeleteAsync(policy, cancellationToken);

        return (new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.Deleted, policy.Name), []);
    }

    private async Task<WorkOrderSettingDeleteResult> DeleteTypeAsync(string id, CancellationToken cancellationToken)
    {
        var item = await _types.FindByIdAsync(id, cancellationToken);

        if (item is null)
        {
            return new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.NotFound, null);
        }

        if (await _session.QueryIndex<WorkOrderIndex>(index => index.TypeId == id, collection: WorkOrdersConstants.CollectionName).FirstOrDefaultAsync(cancellationToken) is not null)
        {
            return new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.InUse, item.Name);
        }

        await _types.DeleteAsync(item, cancellationToken);

        return new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.Deleted, item.Name);
    }

    private async Task<WorkOrderSettingDeleteResult> DeleteUnusedAsync<T>(WorkOrderSettingManager<T> manager, string id, Func<T, string> nameOf, Func<WorkOrderType, bool> uses, CancellationToken cancellationToken)
        where T : CatalogItem, new()
    {
        var item = await manager.FindByIdAsync(id, cancellationToken);

        if (item is null)
        {
            return new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.NotFound, null);
        }

        if ((await _types.GetAllAsync(cancellationToken)).Any(uses))
        {
            return new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.InUse, nameOf(item));
        }

        await manager.DeleteAsync(item, cancellationToken);

        return new WorkOrderSettingDeleteResult(WorkOrderSettingDeleteOutcome.Deleted, nameOf(item));
    }
}
