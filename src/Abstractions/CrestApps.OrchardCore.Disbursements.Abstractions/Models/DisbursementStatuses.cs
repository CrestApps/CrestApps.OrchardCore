using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Disbursements.Models;

/// <summary>
/// The moves a disbursement's status may make.
/// </summary>
public static class DisbursementStatuses
{
    private static readonly TransitionTable<DisbursementStatus> _transitions = new(new Dictionary<DisbursementStatus, DisbursementStatus[]>
    {
        [DisbursementStatus.Pending] = [DisbursementStatus.Batched, DisbursementStatus.Submitted, DisbursementStatus.Settled, DisbursementStatus.Failed, DisbursementStatus.Canceled],
        [DisbursementStatus.Batched] = [DisbursementStatus.Pending, DisbursementStatus.Submitted, DisbursementStatus.Settled, DisbursementStatus.Failed, DisbursementStatus.Canceled],
        [DisbursementStatus.Submitted] = [DisbursementStatus.Settled, DisbursementStatus.Returned, DisbursementStatus.Failed, DisbursementStatus.Canceled],
        [DisbursementStatus.Settled] = [DisbursementStatus.Returned, DisbursementStatus.Reversed],
        [DisbursementStatus.Returned] = [],
        [DisbursementStatus.Failed] = [],
        [DisbursementStatus.Canceled] = [],
        [DisbursementStatus.Reversed] = [],
    });

    /// <summary>
    /// Gets the allowed moves. Returned, Failed, Canceled and Reversed are final.
    /// </summary>
    public static TransitionTable<DisbursementStatus> Transitions
        => _transitions;
}
