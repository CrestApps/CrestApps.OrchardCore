namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// The editor of a transaction workflow task. Each task shows the fields it has; all of them are Liquid expressions.
/// </summary>
public class TransactionTaskViewModel
{
    /// <summary>
    /// Gets or sets the id of the transaction.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public string Amount { get; set; }

    /// <summary>
    /// Gets or sets how the payment was made.
    /// </summary>
    public string PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets the payment's reference.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the kind of adjustment.
    /// </summary>
    public string AdjustmentKind { get; set; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the name of the workflow output the task writes.
    /// </summary>
    public string OutputName { get; set; }
}
