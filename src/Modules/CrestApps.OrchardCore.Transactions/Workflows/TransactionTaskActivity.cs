using System.Globalization;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// A workflow task that runs a ledger command on one transaction. Its fields are Liquid expressions. It ends with
/// <c>Done</c> and writes the transaction to the workflow output, with <c>NotFound</c> when no transaction has the id, or
/// with <c>Failed</c> when the ledger refused the command (the reasons are in the output's <c>Errors</c>).
/// </summary>
/// <typeparam name="TActivity">The task type.</typeparam>
public abstract class TransactionTaskActivity<TActivity> : TaskActivity<TActivity>
    where TActivity : ITask
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionTaskActivity{TActivity}"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="commands">The ledger commands.</param>
    /// <param name="expressionEvaluator">The evaluator of the Liquid fields.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected TransactionTaskActivity(
        ITransactionManager transactionManager,
        ITransactionCommandService commands,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer stringLocalizer)
    {
        TransactionManager = transactionManager;
        Commands = commands;
        _expressionEvaluator = expressionEvaluator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the transaction manager.
    /// </summary>
    protected ITransactionManager TransactionManager { get; }

    /// <summary>
    /// Gets the ledger commands.
    /// </summary>
    protected ITransactionCommandService Commands { get; }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc />
    public override LocalizedString Category => S["Transactions"];

    /// <summary>
    /// Gets or sets the id of the transaction, a Liquid expression.
    /// </summary>
    public string TransactionId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the name of the workflow output the task writes, read in Liquid as <c>Workflow.Output.Name</c>.
    /// </summary>
    public string OutputName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["NotFound"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var transactionId = await EvaluateAsync(TransactionId, workflowContext);

        if (string.IsNullOrEmpty(transactionId) || await TransactionManager.FindByIdAsync(transactionId) is null)
        {
            return Outcome("NotFound");
        }

        var result = await RunAsync(transactionId, workflowContext);
        var output = result.Transaction is null
            ? new Dictionary<string, object>(StringComparer.Ordinal) { [TransactionWorkflowInput.TransactionId] = transactionId }
            : TransactionWorkflowInput.ToDictionary(result.Transaction, result.Event);

        output["Errors"] = result.Errors.ToArray();
        workflowContext.Output[string.IsNullOrWhiteSpace(OutputName) ? "Transaction" : OutputName.Trim()] = output;

        return Outcome(result.Succeeded ? "Done" : "Failed");
    }

    /// <summary>
    /// Runs the task's command on the transaction.
    /// </summary>
    /// <param name="transactionId">The transaction's id.</param>
    /// <param name="workflowContext">The workflow, for evaluating the other fields.</param>
    /// <returns>The result of the command.</returns>
    protected abstract Task<TransactionCommandResult> RunAsync(string transactionId, WorkflowExecutionContext workflowContext);

    /// <summary>
    /// Evaluates a Liquid field.
    /// </summary>
    /// <param name="expression">The field.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The value, trimmed, or <see langword="null"/> when the field or its value is empty.</returns>
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
    /// Evaluates a Liquid field that holds an amount, written with a dot as the decimal separator.
    /// </summary>
    /// <param name="expression">The field.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The amount, or zero when the field is empty or not a number.</returns>
    protected async Task<decimal> EvaluateAmountAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        var value = await EvaluateAsync(expression, workflowContext);

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;
    }

    /// <summary>
    /// Returns a failed result for a field the task could not read.
    /// </summary>
    /// <param name="message">Why the task failed.</param>
    /// <returns>The result.</returns>
    protected static TransactionCommandResult Fail(string message)
    {
        return new TransactionCommandResult().Fail(message);
    }

    /// <summary>
    /// Reads an adjustment kind by its name, without regard to case.
    /// </summary>
    /// <param name="value">The name.</param>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> when the name is a kind.</returns>
    protected static bool TryParseAdjustmentKind(string value, out TransactionAdjustmentKind kind)
    {
        return Enum.TryParse(value, ignoreCase: true, out kind) && Enum.IsDefined(kind);
    }
}
