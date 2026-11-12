using CrestApps.OrchardCore.WorkOrders.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.WorkOrders.Drivers;

/// <summary>
/// The technicians a job should go to, or must not go to, picked from staff. Dispatch reads them when it suggests who
/// takes the job.
/// </summary>
internal sealed class WorkOrderTechniciansDisplayDriver : DisplayDriver<WorkOrder>
{
    public override IDisplayResult Edit(WorkOrder workOrder, BuildEditorContext context)
        => Initialize<WorkOrderTechniciansViewModel>("WorkOrderTechnicians_Edit", model =>
        {
            model.PreferredWorkerIds = [.. workOrder.PreferredWorkerIds ?? []];
            model.ExcludedWorkerIds = [.. workOrder.ExcludedWorkerIds ?? []];
        }).Location("Content:6");

    public override async Task<IDisplayResult> UpdateAsync(WorkOrder workOrder, UpdateEditorContext context)
    {
        var model = new WorkOrderTechniciansViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var preferred = Clean(model.PreferredWorkerIds);
        var excluded = Clean(model.ExcludedWorkerIds);

        // Someone can't be both wanted and kept away: being kept away wins.
        workOrder.PreferredWorkerIds = [.. preferred.Where(id => !excluded.Contains(id))];
        workOrder.ExcludedWorkerIds = [.. excluded];

        return Edit(workOrder, context);
    }

    private static List<string> Clean(IEnumerable<string> ids)
        => [.. (ids ?? []).SelectMany(id => (id ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// The values of the technicians card of the work order editor.
/// </summary>
public class WorkOrderTechniciansViewModel
{
    /// <summary>
    /// Gets or sets the staff members the job should go to.
    /// </summary>
    public IList<string> PreferredWorkerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the staff members the job must not go to.
    /// </summary>
    public IList<string> ExcludedWorkerIds { get; set; } = [];
}
