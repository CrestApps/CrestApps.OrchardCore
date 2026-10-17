using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class CadenceDisplayDriver : DisplayDriver<Cadence>
{
    public override Task<IDisplayResult> DisplayAsync(Cadence schedule, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = schedule.DisplayText?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("Cadence_Fields_SummaryAdmin", schedule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Cadence_Buttons_SummaryAdmin", schedule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Actions:5"),
            View("Cadence_DefaultTags_SummaryAdmin", schedule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Steps:5"),
            View("Cadence_DefaultMeta_SummaryAdmin", schedule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
        );
    }

    public override IDisplayResult Edit(Cadence schedule, BuildEditorContext context)
    {
        void Populate(CadenceViewModel model)
        {
            model.DisplayText = schedule.DisplayText;
            model.Description = schedule.Description;
            model.Enabled = context.IsNew || schedule.Enabled;
            model.Steps = schedule.Steps is { Count: > 0 }
                ? schedule.Steps.Select(step => new CadenceStep
                {
                    DelayMinutes = step.DelayMinutes,
                    IsAiGenerated = step.IsAiGenerated,
                    Message = step.Message,
                }).ToList()
                : [];
        }

        return Combine(
            Initialize<CadenceViewModel>("CadenceGeneral_Edit", Populate).Location("Content:1%General;1"),
            Initialize<CadenceViewModel>("CadenceSteps_Edit", Populate).Location("Content:1%Follow-up steps;2"));
    }

    public override async Task<IDisplayResult> UpdateAsync(Cadence schedule, UpdateEditorContext context)
    {
        var model = new CadenceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        schedule.DisplayText = model.DisplayText?.Trim();
        schedule.Description = model.Description?.Trim();
        schedule.Enabled = model.Enabled;

        // Keep only rows the user actually filled in (a positive delay), trim their text, and preserve order.
        schedule.Steps = (model.Steps ?? [])
            .Where(step => step is not null && step.DelayMinutes > 0)
            .Select(step => new CadenceStep
            {
                DelayMinutes = step.DelayMinutes,
                IsAiGenerated = step.IsAiGenerated,
                Message = step.Message?.Trim(),
            })
            .ToList();

        // Storage rules (name required, defined steps need message text) live on the CadenceHandler so the editor, a
        // recipe, and a deployment plan all enforce the same set; the controller runs them via CatalogEntryValidation.
        return Edit(schedule, context);
    }
}
