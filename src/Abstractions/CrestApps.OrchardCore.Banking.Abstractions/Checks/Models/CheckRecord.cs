using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.SourceAccess;

namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// One check in the register: drawn on one of the business's bank accounts, with its number, payee and amount, and
/// what it pays for (an open source kind and id, such as a pay statement). It never holds an account number.
/// </summary>
public sealed class CheckRecord : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the bank account the check is drawn on.
    /// </summary>
    public string BankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the bank account's name when the check was issued.
    /// </summary>
    public string BankAccountName { get; set; }

    /// <summary>
    /// Gets or sets the check number: from the bank account's number series, typed for a check written by hand, or
    /// given by the print run for a check printed on pre-printed stock. It is 0 while the check is
    /// <see cref="CheckStatus.Queued"/>.
    /// </summary>
    public long Number { get; set; }

    /// <summary>
    /// Gets or sets how the check got, or gets, its number.
    /// </summary>
    public CheckNumbering Numbering { get; set; }

    /// <summary>
    /// Gets or sets who is paid, when the payee is a party the site knows.
    /// </summary>
    public PartyRef Payee { get; set; }

    /// <summary>
    /// Gets or sets the payee's name as it prints.
    /// </summary>
    public string PayeeName { get; set; }

    /// <summary>
    /// Gets or sets the payee's address as it prints, for a window envelope.
    /// </summary>
    public Address PayeeAddress { get; set; }

    /// <summary>
    /// Gets or sets the amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the three-letter currency code.
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
    /// Gets or sets the kind of record the check pays, such as <c>PayStatement</c>; empty for a check written by hand.
    /// </summary>
    public string SourceKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the check pays.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the access scope within the source kind, such as the pay group of a paycheck: the
    /// <see cref="ISourceAccess"/> of the source kind decides who sees the checks of each scope.
    /// </summary>
    public string AccessScope { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public CheckStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the check was issued.
    /// </summary>
    public DateTime IssuedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the check was last printed.
    /// </summary>
    public DateTime? PrintedUtc { get; set; }

    /// <summary>
    /// Gets or sets the print run the check was last printed in.
    /// </summary>
    public string PrintRunId { get; set; }

    /// <summary>
    /// Gets or sets when the check was voided, stopped, reissued or went stale.
    /// </summary>
    public DateTime? ClosedUtc { get; set; }

    /// <summary>
    /// Gets or sets why the check was voided, stopped or reissued.
    /// </summary>
    public string StatusReason { get; set; }

    /// <summary>
    /// Gets or sets the date the check was voided, in the business's calendar: a module that booked the payment
    /// reverses it on this date. Empty for a check voided before this date was kept.
    /// </summary>
    public DateOnly? VoidedOn { get; set; }

    /// <summary>
    /// Gets or sets the date the bank paid the check.
    /// </summary>
    public DateOnly? ClearedOn { get; set; }

    /// <summary>
    /// Gets or sets the status the check had when it cleared, which <see cref="Services.ICheckService.UnclearAsync"/>
    /// returns it to; <see langword="null"/> for a check that has not cleared, or that cleared before this was kept
    /// (it returns to <see cref="CheckStatus.Printed"/>).
    /// </summary>
    public CheckStatus? ClearedFrom { get; set; }

    /// <summary>
    /// Gets or sets the check that replaced this one.
    /// </summary>
    public string ReissuedById { get; set; }

    /// <summary>
    /// Gets or sets the check this one replaced.
    /// </summary>
    public string ReissueOfId { get; set; }

    /// <summary>
    /// Gets or sets whether the check was imported from another system: it is history and is never printed.
    /// </summary>
    public bool IsHistorical { get; set; }

    /// <summary>
    /// Gets or sets whether the record is a sheet of pre-printed stock that misprinted in a print run: it is voided,
    /// keeps the sheet's number so the number is never used again, and pays nothing (it has no
    /// <see cref="SourceId"/>).
    /// </summary>
    public bool IsSpoiled { get; set; }

    /// <summary>
    /// Gets or sets the check a spoiled sheet was printed for, which waits to print again.
    /// </summary>
    public string SpoiledForId { get; set; }

    /// <summary>
    /// Gets or sets when the check was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the key the check was issued with, or <see langword="null"/>.
    /// </summary>
    public string IdempotencyKey { get; set; }
}
