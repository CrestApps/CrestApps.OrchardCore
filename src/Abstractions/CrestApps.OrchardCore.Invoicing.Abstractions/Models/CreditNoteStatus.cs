namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// The life cycle of a credit note.
/// </summary>
public enum CreditNoteStatus
{
    /// <summary>
    /// Being written: no number and no effect on any balance.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Issued: numbered, locked and applied as its disposition says.
    /// </summary>
    Issued = 1,

    /// <summary>
    /// Voided before anything was refunded. Final.
    /// </summary>
    Void = 2,
}
