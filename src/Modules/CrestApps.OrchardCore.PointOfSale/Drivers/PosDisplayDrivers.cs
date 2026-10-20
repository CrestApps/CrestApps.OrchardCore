using CrestApps.OrchardCore.PointOfSale.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.PointOfSale.Drivers;

/// <summary>
/// Renders a register as a row of the registers admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class RegisterDisplayDriver : DisplayDriver<Register>
{
    public override IDisplayResult Display(Register register, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = register.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("Register_Fields_SummaryAdmin", register)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Register_Description_SummaryAdmin", register)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("Register_Status_SummaryAdmin", register)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("Register_Buttons_SummaryAdmin", register).Location("Actions:5"));
    }
}

/// <summary>
/// Renders a shift as a row of the shifts admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class RegisterShiftDisplayDriver : DisplayDriver<RegisterShift>
{
    public override IDisplayResult Display(RegisterShift shift, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = shift.RegisterName?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("RegisterShift_Fields_SummaryAdmin", shift)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Register:1"),
            View("RegisterShift_Status_SummaryAdmin", shift)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("RegisterShift_Meta_SummaryAdmin", shift)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Opened:1"),
            View("RegisterShift_Buttons_SummaryAdmin", shift).Location("Actions:5"));
    }
}
