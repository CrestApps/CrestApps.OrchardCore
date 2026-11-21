using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class GradeScaleRowDisplayDriver : DisplayDriver<GradeScale>
{
    public override IDisplayResult Display(GradeScale scale, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        void Populate(GradeScaleRowViewModel model) => model.Scale = scale;

        return Combine(
            Initialize<GradeScaleRowViewModel>("GradeScale_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<GradeScaleRowViewModel>("GradeScale_Bands_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Bands:1"),
            Initialize<GradeScaleRowViewModel>("GradeScale_Buttons_SummaryAdmin", Populate).Location("Actions:5"),
            Initialize<GradeScaleRowViewModel>("GradeScale_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:10"));
    }
}
