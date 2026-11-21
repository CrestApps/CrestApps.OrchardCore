using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Renders the rows of the questions of a question bank.
/// </summary>
internal sealed class BankQuestionDisplayDriver : DisplayDriver<BankQuestion>
{
    /// <inheritdoc />
    public override IDisplayResult Display(BankQuestion question, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = question.Question?.Title?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("BankQuestion_Checkbox_SummaryAdmin", question)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("BankQuestion_Fields_SummaryAdmin", question)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Question:1"),
            View("BankQuestion_Tags_SummaryAdmin", question)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Type:5"),
            View("BankQuestion_Meta_SummaryAdmin", question)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
            View("BankQuestion_Buttons_SummaryAdmin", question).Location("Actions:5"),
            View("BankQuestion_ActionsMenu_SummaryAdmin", question).Location("ActionsMenu:10"));
    }
}
