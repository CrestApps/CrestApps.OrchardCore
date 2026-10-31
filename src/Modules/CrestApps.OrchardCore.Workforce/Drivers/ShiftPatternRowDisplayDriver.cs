using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// The rows of the rotation list.
/// </summary>
internal sealed class ShiftPatternRowDisplayDriver : DisplayDriver<ShiftPattern>
{
    private readonly ITeamManager _teamManager;

    public ShiftPatternRowDisplayDriver(ITeamManager teamManager)
    {
        _teamManager = teamManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(ShiftPattern item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        var team = string.IsNullOrEmpty(item.TeamId) ? null : await _teamManager.FindByIdAsync(item.TeamId);

        void Populate(RotationRowViewModel model)
        {
            model.Pattern = item;
            model.TeamName = team?.Name;
        }

        return Combine(
            Initialize<RotationRowViewModel>("ShiftPattern_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<RotationRowViewModel>("ShiftPattern_Tags_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            Initialize<RotationRowViewModel>("ShiftPattern_Buttons_SummaryAdmin", Populate).Location("Actions:5"),
            Initialize<RotationRowViewModel>("ShiftPattern_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:10"));
    }
}
