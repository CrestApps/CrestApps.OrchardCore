using System.Globalization;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// The base of the Learning workflow tasks: reads Liquid fields and names the output the task writes. Changes a task
/// makes are recorded with the <see cref="LearningEventActivity.WorkflowActor"/> actor, so the Learning events do not
/// start the same workflow again.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public abstract class LearningTaskActivity<TActivity> : TaskActivity<TActivity>
    where TActivity : ITask
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected LearningTaskActivity(
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer stringLocalizer)
    {
        _expressionEvaluator = expressionEvaluator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Learning"];

    /// <summary>
    /// Gets or sets the name of the workflow output the task writes, read in Liquid as <c>Workflow.Output.Name</c>.
    /// </summary>
    public string OutputName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Evaluates a Liquid field.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when empty.</returns>
    protected async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Evaluates a Liquid field that holds an ISO 8601 time.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The time in UTC, or <see langword="null"/> when empty or not a time.</returns>
    protected async Task<DateTime?> EvaluateUtcAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        var value = await EvaluateAsync(expression, workflowContext);

        return value is not null && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)
            ? DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            : null;
    }

    /// <summary>
    /// Writes the task's result to the workflow output.
    /// </summary>
    /// <param name="workflowContext">The workflow.</param>
    /// <param name="defaultName">The output name when none is set.</param>
    /// <param name="value">The value.</param>
    protected void SetOutput(WorkflowExecutionContext workflowContext, string defaultName, object value)
    {
        workflowContext.Output[string.IsNullOrWhiteSpace(OutputName) ? defaultName : OutputName.Trim()] = value;
    }
}
