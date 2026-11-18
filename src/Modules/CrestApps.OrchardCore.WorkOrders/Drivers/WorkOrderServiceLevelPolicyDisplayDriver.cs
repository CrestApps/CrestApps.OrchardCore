using CrestApps.Core;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.WorkOrders.Drivers;

/// <summary>
/// Adds the Work Orders card to the editor of a shared service level policy written for work orders: the statuses its
/// timers stop in. The choice is kept in the policy's <c>Properties</c>.
/// </summary>
internal sealed class WorkOrderServiceLevelPolicyDisplayDriver : DisplayDriver<ServiceLevelPolicy>
{
    private static readonly WorkOrderStatus[] _pausable =
    [
        WorkOrderStatus.Scheduled,
        WorkOrderStatus.Ready,
        WorkOrderStatus.InProgress,
        WorkOrderStatus.OnHold,
    ];

    public override IDisplayResult Edit(ServiceLevelPolicy policy, BuildEditorContext context)
    {
        if (!IsForWorkOrders(policy))
        {
            return null;
        }

        return Initialize<WorkOrderServiceLevelPolicyViewModel>("WorkOrderServiceLevelPolicy_Edit", model =>
        {
            var settings = policy.TryGet<WorkOrderServiceLevelSettings>(out var stored)
                ? stored
                : new WorkOrderServiceLevelSettings { PauseStatuses = context.IsNew ? [WorkOrderStatus.OnHold] : [] };

            model.Statuses = [.. _pausable];
            model.PauseStatuses = [.. settings.PauseStatuses];
        }).Location("Content:5%Work orders;5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ServiceLevelPolicy policy, UpdateEditorContext context)
    {
        if (!IsForWorkOrders(policy))
        {
            return null;
        }

        var model = new WorkOrderServiceLevelPolicyViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        policy.Put(new WorkOrderServiceLevelSettings
        {
            PauseStatuses = [.. (model.PauseStatuses ?? []).Where(_pausable.Contains).Distinct()],
        });

        return Edit(policy, context);
    }

    private static bool IsForWorkOrders(ServiceLevelPolicy policy)
        => string.Equals(policy.Scope, WorkOrdersConstants.ServiceLevelScope, StringComparison.OrdinalIgnoreCase);
}
