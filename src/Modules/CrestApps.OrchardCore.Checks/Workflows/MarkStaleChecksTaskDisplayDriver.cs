using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Checks.Workflows;

/// <summary>
/// Edits the Mark Stale Checks task.
/// </summary>
public sealed class MarkStaleChecksTaskDisplayDriver : ActivityDisplayDriver<MarkStaleChecksTask, MarkStaleChecksTaskViewModel>
{
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
