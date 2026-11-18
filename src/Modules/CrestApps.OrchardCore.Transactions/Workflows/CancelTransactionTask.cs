using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Cancels a transaction that is no longer owed.
/// </summary>
public sealed class CancelTransactionTask : TransactionTaskActivity<CancelTransactionTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CancelTransactionTask"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="commands">The ledger commands.</param>
    /// <param name="expressionEvaluator">The evaluator of the Liquid fields.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CancelTransactionTask(
        ITransactionManager transactionManager,
        ITransactionCommandService commands,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<CancelTransactionTask> stringLocalizer)
        : base(transactionManager, commands, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Cancel Transaction"];

    /// <summary>
    /// Gets or sets why the transaction is canceled, a Liquid expression.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override async Task<TransactionCommandResult> RunAsync(string transactionId, WorkflowExecutionContext workflowContext)
    {
        return await Commands.CancelAsync(transactionId, await EvaluateAsync(Reason, workflowContext));
    }
}
