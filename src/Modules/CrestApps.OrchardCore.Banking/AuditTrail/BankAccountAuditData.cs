namespace CrestApps.OrchardCore.Banking.AuditTrail;

/// <summary>
/// What a bank account event records. It holds the masked number only, never the protected or clear one.
/// </summary>
public sealed class BankAccountAuditData
{
    /// <summary>
    /// Gets or sets the account's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the owner as <c>Kind:Id</c>; empty for the business's own accounts.
    /// </summary>
    public string Owner { get; set; }

    /// <summary>
    /// Gets or sets the account number, masked.
    /// </summary>
    public string MaskedAccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the routing number.
    /// </summary>
    public string RoutingNumber { get; set; }

    /// <summary>
    /// Gets or sets the status after the change.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the status before the change.
    /// </summary>
    public string PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets every change the save made.
    /// </summary>
    public IList<string> Changes { get; set; } = [];

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }
}
