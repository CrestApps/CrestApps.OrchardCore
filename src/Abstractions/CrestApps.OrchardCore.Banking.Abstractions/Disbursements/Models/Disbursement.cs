using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Banking.Disbursements.Models;

/// <summary>
/// Money the business sends to one payee: an amount, a method, the business's account it leaves from, the payee's
/// account it goes to, and the record it pays. Its <see cref="IdempotencyKey"/> is built from business ids, so asking
/// twice for the same payment gives the same disbursement.
/// </summary>
public sealed class Disbursement : CatalogItem, IModifiedUtcAwareModel
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
    /// Gets or sets whether the payee is a business, which a bank file states (CCD rather than PPD).
    /// </summary>
    public bool IsBusinessPayee { get; set; }

    /// <summary>
    /// Gets or sets the amount; zero only for a prenote.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the three-letter currency code.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets how it is paid, from <see cref="DisbursementsConstants.Methods"/> or one a provider adds.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the provider that sent it, by name.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the business's bank account the money leaves from.
    /// </summary>
    public string SourceBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the payee's bank account the money goes to; empty for a check or a manual payment.
    /// </summary>
    public string DestinationBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the kind of record it pays, such as <c>PayStatement</c>.
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
    /// Gets or sets a memo for the payee.
    /// </summary>
    public string Memo { get; set; }

    /// <summary>
    /// Gets or sets the key that makes asking twice give the same disbursement, built from business ids.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets whether it is a zero-amount test entry that verifies the payee's account.
    /// </summary>
    public bool IsPrenote { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public DisbursementStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the batch it was sent in.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets the date the money should arrive.
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the trace number of its bank file entry, or the provider's reference.
    /// </summary>
    public string TraceNumber { get; set; }

    /// <summary>
    /// Gets or sets the check that pays it, for the check method.
    /// </summary>
    public string CheckId { get; set; }

    /// <summary>
    /// Gets or sets the bank's return or the provider's failure code, such as <c>R01</c>.
    /// </summary>
    public string FailureCode { get; set; }

    /// <summary>
    /// Gets or sets why it was returned, failed or cancelled.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// Gets or sets when it was recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was sent.
    /// </summary>
    public DateTime? SubmittedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it settled, was returned, failed or was cancelled.
    /// </summary>
    public DateTime? ClosedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether it was imported from another system: it is history and is never sent.
    /// </summary>
    public bool IsHistorical { get; set; }

    /// <summary>
    /// Gets or sets when it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
