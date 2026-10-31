using CrestApps.OrchardCore.Skills.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Skills.Drivers;

/// <summary>
/// The rows of the skill list.
/// </summary>
internal sealed class SkillRowDisplayDriver : DisplayDriver<Skill>
{
    public override IDisplayResult Display(Skill skill, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        return Combine(
            View("Skill_Fields_SummaryAdmin", skill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Skill_Tags_SummaryAdmin", skill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("Skill_Buttons_SummaryAdmin", skill).Location("Actions:5"),
            View("Skill_ActionsMenu_SummaryAdmin", skill).Location("ActionsMenu:10"));
    }
}
