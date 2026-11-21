using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// The base of the events about surveys themselves, filtered by survey and kind.
/// </summary>
public abstract class SurveyEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    protected SurveyEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Surveys"];

    /// <summary>
    /// Gets or sets the ids of the surveys that start the workflow; empty for every survey.
    /// </summary>
    public IList<string> SurveyIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the survey kinds that start the workflow; empty for every kind.
    /// </summary>
    public IList<string> Kinds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        ArgumentNullException.ThrowIfNull(workflowContext);

        return SurveyResponseEventActivity.Allows(SurveyIds, workflowContext.Input, SurveyWorkflowInput.SurveyId)
            && SurveyResponseEventActivity.Allows(Kinds, workflowContext.Input, SurveyWorkflowInput.Kind)
            && Matches(workflowContext);
    }

    /// <inheritdoc />
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc />
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <summary>
    /// Applies the filters of a derived event.
    /// </summary>
    /// <param name="workflowContext">The workflow context.</param>
    /// <returns><see langword="true"/> when the event applies.</returns>
    protected virtual bool Matches(WorkflowExecutionContext workflowContext)
        => true;
}
