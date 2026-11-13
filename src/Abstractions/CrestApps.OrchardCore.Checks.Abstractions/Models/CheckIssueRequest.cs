using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// What to issue a check for.
/// </summary>
public sealed class CheckIssueRequest
{
    /// <summary>
    /// Gets or sets the business's bank account the check is drawn on.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets who is paid, when the site knows them as a party.
    /// </summary>
    public PartyRef Payee { get; set; }

    /// <summary>
    /// Gets or sets the payee's name as it prints.
    /// </summary>
    public string PayeeName { get; set; }

    /// <summary>
    /// Gets or sets the payee's address as it prints.
    /// </summary>
    public Address PayeeAddress { get; set; }

    /// <summary>
    /// Gets or sets the amount; it must be more than zero.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency; the bank account's when empty.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the date printed on the check.
    /// </summary>
    public DateOnly CheckDate { get; set; }

    /// <summary>
    /// Gets or sets the memo.
    /// </summary>
    public string Memo { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the check pays, such as <c>PayStatement</c>.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the check pays.
    /// </summary>
    public string SourceId { get; set; }
}
