using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Takes an amount off what is owed on a transaction without money being received: a discount, a waiver, a credit or a
/// write-off.
/// </summary>
public sealed class AdjustTransactionTask : TransactionTaskActivity<AdjustTransactionTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdjustTransactionTask"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="commands">The ledger commands.</param>
    /// <param name="expressionEvaluator">The evaluator of the Liquid fields.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdjustTransactionTask(
        ITransactionManager transactionManager,
        ITransactionCommandService commands,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<AdjustTransactionTask> stringLocalizer)
        : base(transactionManager, commands, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Adjust Transaction"];

    /// <summary>
    /// Gets or sets the amount taken off, a Liquid expression.
    /// </summary>
    public string Amount
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the kind of adjustment, <c>Discount</c>, <c>Waiver</c>, <c>Credit</c> or <c>WriteOff</c>, a Liquid
    /// expression.
    /// </summary>
    public string AdjustmentKind
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets why the amount is taken off, a Liquid expression.
    /// </summary>
    public string Reason
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override async Task<TransactionCommandResult> RunAsync(string transactionId, WorkflowExecutionContext workflowContext)
    {
        var amount = await EvaluateAmountAsync(Amount, workflowContext);

        if (amount <= 0m)
        {
            return Fail(S["The amount must be a number greater than zero."]);
        }

        if (!TryParseAdjustmentKind(await EvaluateAsync(AdjustmentKind, workflowContext), out var kind))
        {
            return Fail(S["The kind of adjustment must be Discount, Waiver, Credit or WriteOff."]);
        }

        return await Commands.AdjustAsync(transactionId, new TransactionAdjustmentRequest
        {
            Amount = amount,
            Kind = kind,
            Reason = await EvaluateAsync(Reason, workflowContext),
        });
    }
}
