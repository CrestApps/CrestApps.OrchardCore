using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// The base of the Surveys workflow tasks. Every text setting supports Liquid; the result is written to the workflow
/// output under <see cref="OutputName"/>.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public abstract class SurveyTaskActivity<TActivity> : TaskActivity<TActivity>
    where TActivity : ITask
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The Liquid evaluator.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    protected SurveyTaskActivity(IWorkflowExpressionEvaluator expressionEvaluator, IStringLocalizer stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Surveys"];

    /// <summary>
    /// Gets or sets the survey id.
    /// </summary>
    public string SurveyId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the response id.
    /// </summary>
    public string ResponseId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the e-mail address.
    /// </summary>
    public string Email
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the name of the person.
    /// </summary>
    public string DisplayName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the kind of the record the response will belong to.
    /// </summary>
    public string ContextKind
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the id of the record the response will belong to.
    /// </summary>
    public string ContextId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets in how many days a link expires.
    /// </summary>
    public string ExpiresInDays
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the extra minutes of an accommodation.
    /// </summary>
    public string ExtraMinutes
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the extra attempts of an accommodation.
    /// </summary>
    public string ExtraAttempts
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the name of a response property.
    /// </summary>
    public string PropertyName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the value of a response property.
    /// </summary>
    public string PropertyValue
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the workflow output name of the result.
    /// </summary>
    public string OutputName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets the outcomes of the task.
    /// </summary>
    protected virtual IEnumerable<string> OutcomeNames => ["Done", "Failed"];

    /// <summary>
    /// Gets the default output name.
    /// </summary>
    protected virtual string DefaultOutputName => "Survey";

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => OutcomeNames.Select(name => new Outcome(name, name switch
        {
            "Done" => S["Done"],
            "NotFound" => S["NotFound"],
            _ => S["Failed"],
        }));

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        ArgumentNullException.ThrowIfNull(workflowContext);

        var output = new Dictionary<string, object>(StringComparer.Ordinal);
        var outcome = await RunAsync(expression => EvaluateAsync(expression, workflowContext), output);

        workflowContext.Output[string.IsNullOrWhiteSpace(OutputName) ? DefaultOutputName : OutputName.Trim()] = output;

        return Outcome(outcome);
    }

    /// <summary>
    /// Does the task's work.
    /// </summary>
    /// <param name="evaluate">Evaluates a Liquid setting; returns <see langword="null"/> for an empty result.</param>
    /// <param name="output">The output to fill.</param>
    /// <returns>The outcome name.</returns>
    protected abstract Task<string> RunAsync(Func<string, Task<string>> evaluate, IDictionary<string, object> output);

    /// <summary>
    /// Reads a whole number from evaluated text.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The number, or 0.</returns>
    protected static int ToInt(string value)
        => int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : 0;

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
