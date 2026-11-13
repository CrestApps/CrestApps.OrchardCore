using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Dispatch.Drivers;

/// <summary>
/// The Technicians card of the work order editor: who dispatch suggests first for the job and who it never sends,
/// picked among the staff the dispatch settings name as technicians.
/// </summary>
internal sealed class WorkOrderTechniciansDisplayDriver : DisplayDriver<WorkOrder>
{
    private readonly IDispatchSettingsProvider _settingsProvider;

    public WorkOrderTechniciansDisplayDriver(IDispatchSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public override IDisplayResult Edit(WorkOrder workOrder, BuildEditorContext context)
        => Initialize<WorkOrderTechniciansViewModel>("WorkOrderTechnicians_Edit", async model =>
        {
            model.PreferredWorkerIds = [.. workOrder.PreferredWorkerIds ?? []];
            model.ExcludedWorkerIds = [.. workOrder.ExcludedWorkerIds ?? []];
            model.Capabilities = string.Join(",", (await _settingsProvider.GetAsync()).GetTechnicianCapabilities());
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
