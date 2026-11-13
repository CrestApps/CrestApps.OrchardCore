using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Disbursements.Models;

/// <summary>
/// What to pay.
/// </summary>
public sealed class DisbursementRequest
{
    /// <summary>
    /// Gets or sets who is paid.
    /// </summary>
    public PartyRef Payee { get; set; }

    /// <summary>
    /// Gets or sets the payee's name.
    /// </summary>
    public string PayeeName { get; set; }

    /// <summary>
    /// Gets or sets the payee's address, for a check.
    /// </summary>
    public Address PayeeAddress { get; set; }

    /// <summary>
    /// Gets or sets whether the payee is a business.
    /// </summary>
    public bool IsBusinessPayee { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency; the source account's when empty.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets how it is paid.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the business's bank account the money leaves from.
    /// </summary>
    public string SourceBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the payee's bank account, for an ACH payment.
    /// </summary>
    public string DestinationBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the kind of record it pays.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record it pays.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the access scope within the source kind, such as the employer of a pay statement: the
    /// <c>ISourceAccess</c> of the source kind decides who sees the disbursements of each scope.
    /// </summary>
    public string AccessScope { get; set; }

    /// <summary>
    /// Gets or sets a memo.
    /// </summary>
    public string Memo { get; set; }

    /// <summary>
    /// Gets or sets the date the money should arrive.
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key, built from stable business ids such as the pay statement and the bank account
    /// row. When empty, one is built from the source kind, source id, method and destination.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets whether it is a zero-amount prenote.
    /// </summary>
    public bool IsPrenote { get; set; }
}
