using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Edits the Mark Stale Checks task.
/// </summary>
public sealed class MarkStaleChecksTaskDisplayDriver : ActivityDisplayDriver<MarkStaleChecksTask, MarkStaleChecksTaskViewModel>
{
    internal readonly IStringLocalizer S;

    public MarkStaleChecksTaskDisplayDriver(IStringLocalizer<MarkStaleChecksTaskDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(MarkStaleChecksTask activity, BuildEditorContext context)
        => Initialize<MarkStaleChecksTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivity(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Stale Checks"], "10"));

    /// <inheritdoc />
    protected override void EditActivity(MarkStaleChecksTask activity, MarkStaleChecksTaskViewModel model)
    {
        model.AgeInDays = activity.AgeInDays;
    }

    /// <inheritdoc />
    protected override void UpdateActivity(MarkStaleChecksTaskViewModel model, MarkStaleChecksTask activity)
    {
        activity.AgeInDays = Math.Clamp(model.AgeInDays, 1, 3650);
    }
}

/// <summary>
/// The fields of the Mark Stale Checks task.
/// </summary>
public class MarkStaleChecksTaskViewModel
{
    /// <summary>
    /// Gets or sets how many days after its check date a printed check becomes stale.
    /// </summary>
    public int AgeInDays { get; set; } = MarkStaleChecksTask.DefaultAgeInDays;
}
