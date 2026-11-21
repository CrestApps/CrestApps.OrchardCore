using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// The base of the events about survey responses, filtered by survey, kind, collector and context kind. A change a
/// workflow task made starts it only when <see cref="IncludeWorkflowChanges"/> is on, so a workflow does not start
/// itself again.
/// </summary>
public abstract class SurveyResponseEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    protected SurveyResponseEventActivity(IStringLocalizer stringLocalizer)
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
    /// Gets or sets the ids of the surveys whose responses start the workflow; empty for every survey.
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

    /// <summary>
    /// Gets or sets the ids of the collectors whose responses start the workflow; empty for every collector.
    /// </summary>
    public IList<string> CollectorIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the context kinds (such as LearningEnrollment) whose responses start the workflow; empty for every
    /// response.
    /// </summary>
    public IList<string> ContextKinds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets whether a change a workflow task made starts the workflow too.
    /// </summary>
    public bool IncludeWorkflowChanges
    {
        get => GetProperty(() => false);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc />
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        ArgumentNullException.ThrowIfNull(workflowContext);

        var input = workflowContext.Input;

        if (!IncludeWorkflowChanges && IsWorkflowChange(input))
        {
            return false;
        }

        return Allows(SurveyIds, input, SurveyWorkflowInput.SurveyId)
            && Allows(Kinds, input, SurveyWorkflowInput.Kind)
            && Allows(CollectorIds, input, SurveyWorkflowInput.CollectorId)
            && Allows(ContextKinds, input, SurveyWorkflowInput.ContextKind)
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

    /// <summary>
    /// Tells whether a filter list allows the input value of a key.
    /// </summary>
    /// <param name="allowed">The filter; empty allows everything.</param>
    /// <param name="input">The workflow input.</param>
    /// <param name="key">The input key.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    internal static bool Allows(IList<string> allowed, IDictionary<string, object> input, string key)
    {
        if (allowed is null || allowed.Count == 0)
        {
            return true;
        }

        var value = input.TryGetValue(key, out var raw) ? Text(raw) : null;

        return value is not null && allowed.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads an input value as text.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    internal static string Text(object value)
        => value switch
        {
            null => null,
            string text => text,
            JsonValue json when json.TryGetValue<string>(out var text) => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => value.ToString(),
        };

    private static bool IsWorkflowChange(IDictionary<string, object> input)
    {
        if (input.TryGetValue(SurveyWorkflowInput.CausedByWorkflow, out var caused) && string.Equals(Text(caused), bool.TrueString, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return input.TryGetValue(SurveyWorkflowInput.Actor, out var actor) && string.Equals(Text(actor), SurveyWorkflowInput.WorkflowActor, StringComparison.Ordinal);
    }
}
