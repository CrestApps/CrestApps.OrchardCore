using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Builds the editor of a planning group, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class PlanningGroupEditDisplayDriver : DisplayDriver<PlanningGroupEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanningGroupEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PlanningGroupEditDisplayDriver(IStringLocalizer<PlanningGroupEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(PlanningGroupEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<PlanningGroupEditViewModel>("PlanningGroupDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<PlanningGroupEditViewModel>("PlanningGroupServiceGoal_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Service Goal"], "20")),
            Initialize<PlanningGroupEditViewModel>("PlanningGroupStaffing_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Staffing"], "30")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(PlanningGroupEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.QueueIds, m => m.Channel, m => m.TeamId, m => m.ServiceLevelPercent, m => m.ServiceLevelSeconds, m => m.AverageSpeedOfAnswerSeconds, m => m.MaxOccupancyPercent, m => m.Concurrency, m => m.PatienceSeconds, m => m.ShrinkagePercent, m => m.HistoryWeeks);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(PlanningGroupEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(PlanningGroupEditViewModel source, PlanningGroupEditViewModel model)
    {
        model.Name = source.Name;
        model.QueueIds = source.QueueIds;
        model.Channel = source.Channel;
        model.TeamId = source.TeamId;
        model.ServiceLevelPercent = source.ServiceLevelPercent;
        model.ServiceLevelSeconds = source.ServiceLevelSeconds;
        model.AverageSpeedOfAnswerSeconds = source.AverageSpeedOfAnswerSeconds;
        model.MaxOccupancyPercent = source.MaxOccupancyPercent;
        model.Concurrency = source.Concurrency;
        model.PatienceSeconds = source.PatienceSeconds;
        model.ShrinkagePercent = source.ShrinkagePercent;
        model.HistoryWeeks = source.HistoryWeeks;
        model.QueueOptions = source.QueueOptions;
        model.TeamOptions = source.TeamOptions;
    }
}
