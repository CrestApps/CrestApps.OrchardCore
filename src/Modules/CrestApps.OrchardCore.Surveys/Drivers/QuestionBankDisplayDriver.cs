using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Renders the rows of the question banks list.
/// </summary>
internal sealed class QuestionBankDisplayDriver : DisplayDriver<QuestionBank>
{
    /// <inheritdoc />
    public override IDisplayResult Display(QuestionBank bank, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{bank.Title} {bank.Name}".ToLowerInvariant();
        }

        return Combine(
            View("QuestionBank_Fields_SummaryAdmin", bank)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("QuestionBank_Description_SummaryAdmin", bank)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("QuestionBank_Tags_SummaryAdmin", bank)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Tags:5"),
            View("QuestionBank_Meta_SummaryAdmin", bank)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
            View("QuestionBank_Buttons_SummaryAdmin", bank).Location("Actions:5"),
            View("QuestionBank_ActionsMenu_SummaryAdmin", bank).Location("ActionsMenu:10"));
    }
}
