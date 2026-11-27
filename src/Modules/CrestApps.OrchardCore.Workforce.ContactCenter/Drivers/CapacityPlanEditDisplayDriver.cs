using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Builds the editor of a capacity plan, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class CapacityPlanEditDisplayDriver : DisplayDriver<CapacityPlanEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapacityPlanEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CapacityPlanEditDisplayDriver(IStringLocalizer<CapacityPlanEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(CapacityPlanEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<CapacityPlanEditViewModel>("CapacityPlanDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<CapacityPlanEditViewModel>("CapacityPlanWork_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Work"], "20")),
            Initialize<CapacityPlanEditViewModel>("CapacityPlanAgents_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Agents"], "30")),
            Initialize<CapacityPlanEditViewModel>("CapacityPlanHiringClasses_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Hiring Classes"], "40")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(CapacityPlanEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.PlanningGroupId, m => m.StartDate, m => m.Weeks, m => m.WeeklyWorkloadHours, m => m.GrowthPercentPerYear, m => m.HoursPerFte, m => m.CurrentFte, m => m.AttritionPercentPerMonth, m => m.HiringClasses);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(CapacityPlanEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CapacityPlanEditViewModel source, CapacityPlanEditViewModel model)
    {
        model.Name = source.Name;
        model.PlanningGroupId = source.PlanningGroupId;
        model.StartDate = source.StartDate;
        model.Weeks = source.Weeks;
        model.WeeklyWorkloadHours = source.WeeklyWorkloadHours;
        model.GrowthPercentPerYear = source.GrowthPercentPerYear;
        model.HoursPerFte = source.HoursPerFte;
        model.CurrentFte = source.CurrentFte;
        model.AttritionPercentPerMonth = source.AttritionPercentPerMonth;
        model.HiringClasses = source.HiringClasses;
        model.PlanningGroupOptions = source.PlanningGroupOptions;
    }
}
