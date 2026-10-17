namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// An installment plan of the person.
/// </summary>
public sealed class CommerceInstallmentPlanRecord
{
    /// <summary>
    /// Gets or sets the plan identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets what the plan pays for.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the user the plan belongs to.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the customer's name recorded on the plan.
    /// </summary>
    public string CustomerName { get; set; }

    /// <summary>
    /// Gets or sets the customer's email recorded on the plan.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the status of the plan.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency of the amounts.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the total paid over the life of the plan.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the down payment.
    /// </summary>
    public decimal DownPaymentAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount received so far.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Gets or sets the number of payments after the down payment.
    /// </summary>
    public int InstallmentCount { get; set; }

    /// <summary>
    /// Gets or sets how often the payments fall due.
    /// </summary>
    public string Frequency { get; set; }

    /// <summary>
    /// Gets or sets how the payments are collected.
    /// </summary>
    public string CollectionMethod { get; set; }

    /// <summary>
    /// Gets or sets the description of the saved payment method, for example <c>Visa •••• 4242</c>.
    /// </summary>
    public string PaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets the note recorded on the plan.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets the ledger transactions of the plan's payments.
    /// </summary>
    public IList<string> TransactionIds { get; init; } = [];

    /// <summary>
    /// Gets or sets when the plan was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last payment was received, when it was.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the plan was canceled, when it was.
    /// </summary>
    public DateTime? CanceledUtc { get; set; }
}
