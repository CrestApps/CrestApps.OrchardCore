using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a respondent completes a page of a survey, optionally only some pages.
/// </summary>
public sealed class SurveyPageCompletedEvent : SurveyResponseEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPageCompletedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyPageCompletedEvent(IStringLocalizer<SurveyPageCompletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyPageCompletedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Page Completed"];

    /// <summary>
    /// Gets or sets the names of the pages that start the workflow; empty for every page.
    /// </summary>
    public IList<string> PageNames
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override bool Matches(WorkflowExecutionContext workflowContext)
        => Allows(PageNames, workflowContext.Input, SurveyWorkflowInput.PageName);
}
