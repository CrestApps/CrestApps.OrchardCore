using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Implements Power dialing: the system reserves agents and places a controlled, capped number of
/// calls per pacing cycle. The per-agent call count is hard-capped at <see cref="MaxCallsPerAgent"/>
/// because reliable answer-rate forecasting and abandonment controls are required before higher
/// over-dialing ratios can be used safely.
/// </summary>
public sealed class PowerDialerStrategy : DialerStrategyBase
{
    /// <summary>
    /// The maximum number of calls per agent allowed for Power dialing until predictive pacing exists.
    /// </summary>
    public const int MaxCallsPerAgent = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerDialerStrategy"/> class.
    /// </summary>
    /// <param name="assignmentService">The assignment service used to reserve agents and activities.</param>
    /// <param name="attemptService">The attempt service that applies compliance and places each call.</param>
    public PowerDialerStrategy(
        IActivityAssignmentService assignmentService,
        IDialerAttemptService attemptService)
        : base(assignmentService, attemptService)
    {
    }

    /// <inheritdoc/>
    public override DialerMode Mode => DialerMode.Power;

    /// <inheritdoc/>
    protected override int GetMaxAttemptsPerCycle(DialerProfile profile)
    {
        return Math.Clamp(profile.CallsPerAgent, 1, MaxCallsPerAgent);
    }
}
