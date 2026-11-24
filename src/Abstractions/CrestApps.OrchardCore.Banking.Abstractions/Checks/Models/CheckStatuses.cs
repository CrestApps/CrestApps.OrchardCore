using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// The moves a check's status may make.
/// </summary>
public static class CheckStatuses
{
    private static readonly TransitionTable<CheckStatus> _transitions = new(new Dictionary<CheckStatus, CheckStatus[]>
    {
        // A queued check is printed only by a print run, which numbers it; until then it can only be voided.
        [CheckStatus.Queued] = [CheckStatus.Printed, CheckStatus.Voided],
        [CheckStatus.Issued] = [CheckStatus.Printed, CheckStatus.Voided, CheckStatus.Reissued],
        [CheckStatus.Printed] = [CheckStatus.Voided, CheckStatus.StopPayment, CheckStatus.Cleared, CheckStatus.Reissued, CheckStatus.Stale],
        [CheckStatus.StopPayment] = [CheckStatus.Voided, CheckStatus.Reissued, CheckStatus.Cleared],
        [CheckStatus.Stale] = [CheckStatus.Voided, CheckStatus.Reissued, CheckStatus.Cleared],
        [CheckStatus.Voided] = [],
        [CheckStatus.Cleared] = [],
        // A replaced check the bank paid anyway: recorded so the register flags it.
        [CheckStatus.Reissued] = [CheckStatus.Cleared],
    });

    /// <summary>
    /// Gets the allowed moves. Voided and Cleared are final; a reissued check can still clear. Recording that a cleared
    /// check was not paid after all (<see cref="Services.ICheckService.UnclearAsync"/>) is not a move of this table: it
    /// returns the check to the status it cleared from (<see cref="CheckRecord.ClearedFrom"/>).
    /// </summary>
    public static TransitionTable<CheckStatus> Transitions
        => _transitions;

    /// <summary>
    /// Gets whether a check can still be edited (<see cref="Services.ICheckService.UpdateAsync"/>): it waits to print,
    /// queued without a number or issued with one, and it is neither history nor a spoiled sheet.
    /// </summary>
    /// <param name="check">The check.</param>
    /// <returns><see langword="true"/> when its payee, amount, date, memo and address can still change.</returns>
    public static bool CanEdit(CheckRecord check)
    {
        ArgumentNullException.ThrowIfNull(check);

        return !check.IsHistorical && !check.IsSpoiled && check.Status is CheckStatus.Queued or CheckStatus.Issued;
    }
}
