using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Implements Progressive dialing: the system places one call per agent as agents become available,
/// draining the available agents reserved during the cycle. A safety bound caps the number of calls
/// started in a single cycle to protect against runaway pacing.
/// </summary>
public sealed class ProgressiveDialerStrategy : DialerStrategyBase
{
    /// <summary>
    /// The maximum number of calls a single Progressive cycle may start as a safety bound.
    /// </summary>
    public const int MaxCallsPerCycle = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressiveDialerStrategy"/> class.
    /// </summary>
    /// <param name="assignmentService">The assignment service used to reserve agents and activities.</param>
    /// <param name="attemptService">The attempt service that applies compliance and places each call.</param>
    public ProgressiveDialerStrategy(
        IActivityAssignmentService assignmentService,
        IDialerAttemptService attemptService)
        : base(assignmentService, attemptService)
    {
    }

    /// <inheritdoc/>
    public override DialerMode Mode => DialerMode.Progressive;

    /// <inheritdoc/>
    protected override int GetMaxAttemptsPerCycle(DialerProfile profile)
    {
        return MaxCallsPerCycle;
    }
}
