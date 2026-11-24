using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using CrestApps.OrchardCore.Scheduling.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Classes.Drivers;

/// <summary>
/// The row of a session on the sessions page of a class.
/// </summary>
internal sealed class ClassSessionRowDisplayDriver : DisplayDriver<ClassSessionRow>
{
    public override Task<IDisplayResult> DisplayAsync(ClassSessionRow row, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape shape)
        {
            shape.Classes.Add("item");
        }

        var results = new List<IDisplayResult>
        {
            View("ClassSessionRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ClassSessionRow_Dates_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Dates:1"),
            View("ClassSessionRow_Places_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Places:5"),
            View("ClassSessionRow_Status_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("ClassSessionRow_Buttons_SummaryAdmin", row).Location("Actions:5"),
        };

        // A cancelled session has no menu action left, so it gets no empty menu.
        if (row.Session.Status != SchedulingSessionStatus.Cancelled)
        {
            results.Add(View("ClassSessionRow_ActionsMenu_SummaryAdmin", row).Location("ActionsMenu:10"));
        }

        return CombineAsync(results.ToArray());
    }
}

/// <summary>
/// The row of a registration on the roster of a session, with its attendance inputs.
/// </summary>
internal sealed class ClassRosterRowDisplayDriver : DisplayDriver<ClassRosterRow>
{
    public override Task<IDisplayResult> DisplayAsync(ClassRosterRow row, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape shape)
        {
            shape.Classes.Add("item");
        }

        return CombineAsync(
            View("ClassRosterRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ClassRosterRow_Status_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("ClassRosterRow_Attendance_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Attendance:5"));
    }
}

/// <summary>
/// The row of a person waiting for a place in a session.
/// </summary>
internal sealed class ClassWaitlistRowDisplayDriver : DisplayDriver<ClassWaitlistRow>
{
    public override Task<IDisplayResult> DisplayAsync(ClassWaitlistRow row, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape shape)
        {
            shape.Classes.Add("item");
        }

        return CombineAsync(
            View("ClassWaitlistRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ClassWaitlistRow_State_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:5"));
    }
}
