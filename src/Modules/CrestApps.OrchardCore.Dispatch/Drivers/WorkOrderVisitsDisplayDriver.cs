using CrestApps.OrchardCore.Dispatch.Controllers;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Dispatch.Drivers;

/// <summary>
/// Shows a work order's visits on its page, with a way to schedule the next one.
/// </summary>
internal sealed class WorkOrderVisitsDisplayDriver : DisplayDriver<WorkOrderPanels>
{
    private readonly IDispatchService _dispatch;
    private readonly DispatchTimeZone _timeZone;
    private readonly Core.Services.DispatchResources _resources;

    public WorkOrderVisitsDisplayDriver(IDispatchService dispatch, DispatchTimeZone timeZone, Core.Services.DispatchResources resources)
    {
        _dispatch = dispatch;
        _timeZone = timeZone;
        _resources = resources;
    }

    public override IDisplayResult Display(WorkOrderPanels model, BuildDisplayContext context)
        => Initialize<WorkOrderVisitsViewModel>("WorkOrderVisits", async view =>
        {
            view.WorkOrder = model.WorkOrder;
            view.CanManage = model.CanManage;
            view.TimeZoneId = await _timeZone.GetAsync();
            view.Visits = [.. await _dispatch.ListForWorkOrderAsync(model.WorkOrder.ItemId)];

            foreach (var workerId in view.Visits.SelectMany(visit => visit.WorkerIds).Distinct(StringComparer.Ordinal))
            {
                view.WorkerNames[workerId] = (await _resources.FindWorkerAsync(workerId))?.DisplayName ?? workerId;
            }
        }).Location("Content:5");
}
