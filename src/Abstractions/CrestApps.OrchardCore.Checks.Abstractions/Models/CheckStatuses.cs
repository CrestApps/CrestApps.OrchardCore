using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// The moves a check's status may make.
/// </summary>
public static class CheckStatuses
{
    /// <summary>
    /// Gets the allowed moves. Voided, Cleared and Reissued are final.
    /// </summary>
    public static TransitionTable<CheckStatus> Transitions { get; } = new(new Dictionary<CheckStatus, CheckStatus[]>
    {
        [CheckStatus.Issued] = [CheckStatus.Printed, CheckStatus.Voided, CheckStatus.Reissued],
        [CheckStatus.Printed] = [CheckStatus.Voided, CheckStatus.StopPayment, CheckStatus.Cleared, CheckStatus.Reissued, CheckStatus.Stale],
        [CheckStatus.StopPayment] = [CheckStatus.Voided, CheckStatus.Reissued, CheckStatus.Cleared],
        [CheckStatus.Stale] = [CheckStatus.Voided, CheckStatus.Reissued, CheckStatus.Cleared],
        [CheckStatus.Voided] = [],
        [CheckStatus.Cleared] = [],
        [CheckStatus.Reissued] = [],
    });
}
