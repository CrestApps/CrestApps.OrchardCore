using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The rules of a credit note's life cycle.
/// </summary>
public static class CreditNoteLifecycle
{
    private static readonly TransitionTable<CreditNoteStatus> _transitions = new(new Dictionary<CreditNoteStatus, CreditNoteStatus[]>
    {
        [CreditNoteStatus.Draft] = [CreditNoteStatus.Issued],
        [CreditNoteStatus.Issued] = [CreditNoteStatus.Void],
        [CreditNoteStatus.Void] = [],
    });

    /// <summary>
    /// Returns whether a credit note may move from one status to another.
    /// </summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The status asked for.</param>
    /// <returns><see langword="true"/> when the move is allowed.</returns>
    public static bool CanMove(CreditNoteStatus from, CreditNoteStatus to)
        => _transitions.CanMove(from, to);

    /// <summary>
    /// Returns whether a credit note may still be changed or deleted: only while it is a draft.
    /// </summary>
    /// <param name="creditNote">The credit note.</param>
    /// <returns><see langword="true"/> for a draft.</returns>
    public static bool CanEdit(CreditNote creditNote)
    {
        ArgumentNullException.ThrowIfNull(creditNote);

        return creditNote.Status == CreditNoteStatus.Draft;
    }

    /// <summary>
    /// Returns whether an issued credit note may be voided: only while no money was given back for it.
    /// </summary>
    /// <param name="creditNote">The credit note.</param>
    /// <returns><see langword="true"/> when it may be voided.</returns>
    public static bool CanVoid(CreditNote creditNote)
    {
        ArgumentNullException.ThrowIfNull(creditNote);

        return CanMove(creditNote.Status, CreditNoteStatus.Void) && creditNote.AmountRefunded <= 0m;
    }
}
