using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Learning.Lti.Workflows;

/// <summary>
/// Edits the grading progress values of the External Score Received event.
/// </summary>
public sealed class ExternalScoreReceivedEventDisplayDriver : ActivityDisplayDriver<ExternalScoreReceivedEvent, ExternalScoreReceivedEventViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalScoreReceivedEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ExternalScoreReceivedEventDisplayDriver(IStringLocalizer<ExternalScoreReceivedEventDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ExternalScoreReceivedEvent activity, BuildEditorContext context)
    {
        return Initialize<ExternalScoreReceivedEventViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Trigger"], "10"));
    }

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
