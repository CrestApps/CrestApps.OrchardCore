using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a respondent is screened out, optionally only by some endings.
/// </summary>
public sealed class SurveyResponseScreenedOutEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseScreenedOutEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyResponseScreenedOutEvent(IStringLocalizer<SurveyResponseScreenedOutEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyResponseScreenedOutEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Response Screened Out"];

    /// <summary>
    /// Gets or sets the names of the endings that start the workflow; empty for every ending.
    /// </summary>
    public IList<string> EndingNames
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override bool Matches(WorkflowExecutionContext workflowContext)
        => Allows(EndingNames, workflowContext.Input, SurveyWorkflowInput.EndingName);
}
