using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Learning.Lti.Workflows;

/// <summary>
/// Edits the grading progress values of the External Score Received event.
/// </summary>
public sealed class ExternalScoreReceivedEventDisplayDriver : ActivityDisplayDriver<ExternalScoreReceivedEvent, ExternalScoreReceivedEventViewModel>
{
    /// <inheritdoc />
    protected override void EditActivity(ExternalScoreReceivedEvent activity, ExternalScoreReceivedEventViewModel model)
        => model.GradingProgress = string.Join(Environment.NewLine, activity.GradingProgress);

    /// <inheritdoc />
    protected override void UpdateActivity(ExternalScoreReceivedEventViewModel model, ExternalScoreReceivedEvent activity)
        => activity.GradingProgress = (model.GradingProgress ?? string.Empty)
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
