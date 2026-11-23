namespace CrestApps.OrchardCore.Banking.Checks.AuditTrail;

/// <summary>
/// What a check event records: the check, and what it was and became. It never holds an account number.
/// </summary>
public sealed class CheckAuditData
{
    /// <summary>
    /// Gets or sets the bank account the check is drawn on.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the check number, or 0 while it waits for a print run.
    /// </summary>
    public long Number { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the check pays.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the check pays.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the payee before the change.
    /// </summary>
    public string PreviousPayeeName { get; set; }

    /// <summary>
    /// Gets or sets the payee after the change.
    /// </summary>
    public string PayeeName { get; set; }

    /// <summary>
    /// Gets or sets the amount before the change.
    /// </summary>
    public decimal PreviousAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount after the change.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the check date before the change, as yyyy-MM-dd.
    /// </summary>
    public string PreviousCheckDate { get; set; }

    /// <summary>
    /// Gets or sets the check date after the change, as yyyy-MM-dd.
    /// </summary>
    public string CheckDate { get; set; }

    /// <summary>
    /// Gets or sets the memo before the change.
    /// </summary>
    public string PreviousMemo { get; set; }

    /// <summary>
    /// Gets or sets the memo after the change.
    /// </summary>
    public string Memo { get; set; }

    /// <summary>
    /// Gets or sets whether the payee's address changed.
    /// </summary>
    public bool AddressChanged { get; set; }

    /// <summary>
    /// Gets or sets why the check changed.
    /// </summary>
    public string Reason { get; set; }
}
