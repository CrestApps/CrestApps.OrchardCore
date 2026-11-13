using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Checks.Core.Indexes;

/// <summary>
/// Finds checks by bank account, number, status, payee, source and dates.
/// </summary>
public sealed class CheckRecordIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the bank account the check is drawn on.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the check number.
    /// </summary>
    public long Number { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the payee's name.
    /// </summary>
    public string PayeeName { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the date printed on the check, at midnight.
    /// </summary>
    public DateTime CheckDate { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the check pays.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the check pays.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets when the check was issued.
    /// </summary>
    public DateTime IssuedUtc { get; set; }
}
