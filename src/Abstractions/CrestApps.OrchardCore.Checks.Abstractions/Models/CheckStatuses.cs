using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// The moves a check's status may make.
/// </summary>
public static class CheckStatuses
{
    private static readonly TransitionTable<CheckStatus> _transitions = new(new Dictionary<CheckStatus, CheckStatus[]>
    {
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
    /// Gets the allowed moves. Voided and Cleared are final; a reissued check can still clear.
    /// </summary>
    public static TransitionTable<CheckStatus> Transitions
        => _transitions;
}
