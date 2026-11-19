using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Records a payment received outside the site, such as a bank transfer, on a transaction.
/// </summary>
public sealed class RecordTransactionPaymentTask : TransactionTaskActivity<RecordTransactionPaymentTask>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordTransactionPaymentTask"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager.</param>
    /// <param name="commands">The ledger commands.</param>
    /// <param name="expressionEvaluator">The evaluator of the Liquid fields.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RecordTransactionPaymentTask(
        ITransactionManager transactionManager,
        ITransactionCommandService commands,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IStringLocalizer<RecordTransactionPaymentTask> stringLocalizer)
        : base(transactionManager, commands, expressionEvaluator, stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Record Transaction Payment"];

    /// <summary>
    /// Gets or sets the amount received, a Liquid expression.
    /// </summary>
    public string Amount
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets how the payment was made, such as a bank transfer, a Liquid expression.
    /// </summary>
    public string PaymentMethod
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the payment's reference, such as a check number, a Liquid expression.
    /// </summary>
    public string Reference
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a note kept on the timeline, a Liquid expression.
    /// </summary>
    public string Note
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

        return await Commands.RecordPaymentAsync(transactionId, new TransactionPaymentRequest
        {
            Amount = amount,
            PaymentMethod = await EvaluateAsync(PaymentMethod, workflowContext),
            Reference = await EvaluateAsync(Reference, workflowContext),
            Note = await EvaluateAsync(Note, workflowContext),
        });
    }
}
