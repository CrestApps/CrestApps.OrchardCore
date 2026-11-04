using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.WorkOrders.Navigation;

/// <summary>
/// Adds the work order pages to the Field Service menu.
/// </summary>
public sealed class WorkOrdersAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrdersAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkOrdersAdminMenu(IStringLocalizer<WorkOrdersAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Field Service"], S["Field Service"].PrefixPosition("7"), fieldService => fieldService
            .AddClass("field-service")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-screwdriver-wrench")
            .Id("fieldService")
            .Add(S["Work Orders"], S["Work Orders"].PrefixPosition("10"), workOrders => workOrders
                .Id("workOrders")
                .Action("Index", "WorkOrders", WorkOrdersConstants.Features.WorkOrders)
                .Permission(WorkOrdersPermissions.ViewWorkOrders)
                .LocalNav())
            .Add(S["Work Order Types"], S["Work Order Types"].PrefixPosition("60"), types => types
                .Id("workOrderTypes")
                .Action("Index", "WorkOrderSettings", new { area = WorkOrdersConstants.Features.WorkOrders, kind = "types" })
                .Permission(WorkOrdersPermissions.ManageWorkOrderSettings)
                .LocalNav())
            .Add(S["Checklists"], S["Checklists"].PrefixPosition("61"), checklists => checklists
                .Id("workOrderChecklists")
                .Action("Index", "WorkOrderSettings", new { area = WorkOrdersConstants.Features.WorkOrders, kind = "checklists" })
                .Permission(WorkOrdersPermissions.ManageWorkOrderSettings)
                .LocalNav())
            .Add(S["Service Levels"], S["Service Levels"].PrefixPosition("62"), policies => policies
                .Id("workOrderServiceLevels")
                .Action("Index", "WorkOrderSettings", new { area = WorkOrdersConstants.Features.WorkOrders, kind = "service-levels" })
                .Permission(WorkOrdersPermissions.ManageWorkOrderSettings)
                .LocalNav())
            .Add(S["Sub-statuses"], S["Sub-statuses"].PrefixPosition("63"), subStatuses => subStatuses
                .Id("workOrderSubStatuses")
                .Action("Index", "WorkOrderSettings", new { area = WorkOrdersConstants.Features.WorkOrders, kind = "sub-statuses" })
                .Permission(WorkOrdersPermissions.ManageWorkOrderSettings)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
