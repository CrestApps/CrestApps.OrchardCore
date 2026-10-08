using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Implements Predictive dialing: more calls than agents, paced by the campaign's measured abandonment rate.
/// <para>
/// A profile on <see cref="PredictivePacingModel.ReservedPerCall"/> reserves an agent for every call it places, so a
/// person who answers always has an agent waiting: <see cref="IDialerAbandonmentPolicyService"/> decides whether dialing is
/// permitted at all, and <see cref="PredictiveDialerPacing"/> reduces the per-agent ratio toward one call per agent as the
/// measured rate climbs toward the cap.
/// </para>
/// <para>
/// A profile on <see cref="PredictivePacingModel.OverDial"/> places calls without reserving agents, sized by
/// <see cref="IPredictiveOverDialPacer"/>, and an agent is claimed when a person answers. Whenever the pacer cannot prove
/// over-dialing is safe -- a statistic missing or below its floor, the abandonment rate near the cap -- the cycle falls
/// back to the reserve-then-dial loop, and when the policy forbids dialing nothing is dialed. The strategy is registered by
/// the Paced Dialing feature with Power and Progressive, so without it a Predictive profile resolves to no strategy and is
/// not dialed.
/// </para>
/// </summary>
public sealed class PredictiveDialerStrategy : DialerStrategyBase
{
    private readonly IDialerAbandonmentPolicyService _abandonmentPolicy;
    private readonly IPredictiveOverDialPacer _overDialPacer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictiveDialerStrategy"/> class.
    /// </summary>
    /// <param name="assignmentService">The assignment service used to reserve agents and activities.</param>
    /// <param name="attemptService">The attempt service that applies compliance and places each call.</param>
    /// <param name="abandonmentPolicy">The abandonment policy that gates and paces predictive dialing.</param>
    /// <param name="overDialPacer">The pacer that places calls without reserved agents for an over-dialing profile.</param>
    public PredictiveDialerStrategy(
        IActivityAssignmentService assignmentService,
        IDialerAttemptService attemptService,
        IDialerAbandonmentPolicyService abandonmentPolicy,
        IPredictiveOverDialPacer overDialPacer)
        : base(assignmentService, attemptService)
    {
        _abandonmentPolicy = abandonmentPolicy;
        _overDialPacer = overDialPacer;
    }

    /// <inheritdoc/>
    public override DialerMode Mode => DialerMode.Predictive;

    /// <inheritdoc/>
    public override async Task<int> RunCycleAsync(DialerProfile profile, string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        if (profile.PredictivePacingModel != PredictivePacingModel.OverDial)
        {
            return await base.RunCycleAsync(profile, queueId, cancellationToken);
        }

        var result = await _overDialPacer.RunCycleAsync(profile, queueId, cancellationToken);

        // The pacer could not prove over-dialing safe this cycle: the reserve-then-dial loop, which cannot abandon a call,
        // dials instead. A cycle that did not run, or that the policy suppressed, dials nothing.
        return result.UseReservedFallback
            ? await base.RunCycleAsync(profile, queueId, cancellationToken)
            : result.Dialed;
    }

    /// <inheritdoc/>
    protected override async Task<int> GetMaxAttemptsPerCycleAsync(DialerProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var evaluation = await _abandonmentPolicy.EvaluateAsync(profile, cancellationToken);

        // The policy fails closed: when a cap is enforced but the statistics cannot be proven, nothing is
        // dialled at all. Guessing here means guessing with somebody's regulated campaign.
        if (!evaluation.IsPermitted)
        {
            return 0;
        }

        // Permitted but unmeasured means the campaign has not yet abandoned enough calls to have a rate. Pacing
        // as though the rate were at the cap gives one call per agent, which is the pacing that cannot abandon,
        // and the ratio opens up once there is a measurement to open it with.
        var measured = evaluation.StatisticsAvailable
            ? evaluation.RatePercent
            : profile.MaxAbandonmentRatePercent;

        // The base loop reserves one agent per attempt, so the per-cycle ceiling is the per-agent ratio.
        return PredictiveDialerPacing.CalculatePace(profile, availableAgents: 1, measured);
    }
}
