using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of an activity code, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class ActivityCodeEditDisplayDriver : DisplayDriver<ActivityCodeEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityCodeEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ActivityCodeEditDisplayDriver(IStringLocalizer<ActivityCodeEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ActivityCodeEditViewModel model, BuildEditorContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<ActivityCodeEditViewModel>("ActivityCodeDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<ActivityCodeEditViewModel>("ActivityCodeCounting_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["How It Counts"], "20")),
        };

        // The stages are offered only when the time clock keeps time in Time Tracker.
        if (model.StageOptions is not null)
        {
            results.Add(Initialize<ActivityCodeEditViewModel>("ActivityCodeTimeTracker_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Time Tracker"], "30")));
        }

        return Combine([.. results]);
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ActivityCodeEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Code, m => m.Kind, m => m.Color, m => m.IsPaid, m => m.CountsAsWork, m => m.CountsTowardCoverage, m => m.Enabled, m => m.StageId);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(ActivityCodeEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(ActivityCodeEditViewModel source, ActivityCodeEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Code = source.Code;
        model.Kind = source.Kind;
        model.Color = source.Color;
        model.IsPaid = source.IsPaid;
        model.CountsAsWork = source.CountsAsWork;
        model.CountsTowardCoverage = source.CountsTowardCoverage;
        model.Enabled = source.Enabled;
        model.StageId = source.StageId;
        model.KindOptions = source.KindOptions;
        model.StageOptions = source.StageOptions;
    }
}
