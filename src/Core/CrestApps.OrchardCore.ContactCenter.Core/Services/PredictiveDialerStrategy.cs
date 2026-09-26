using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Implements Predictive dialing: more calls than agents, paced by the campaign's measured abandonment rate.
/// <para>
/// The mode was previously blocked — present in the enum and the editor, resolving to no strategy — so an
/// operator could select it and get a campaign that silently never dialled. It is enabled here because the
/// pacing now has a hard gate: <see cref="IDialerAbandonmentPolicyService"/> decides whether dialing is
/// permitted at all, and <see cref="PredictiveDialerPacing"/> reduces the over-dial ratio toward one call per
/// agent as the measured rate climbs toward the cap. A profile that does not enforce a cap paces at one call
/// per agent, which cannot abandon.
/// </para>
/// </summary>
public sealed class PredictiveDialerStrategy : DialerStrategyBase
{
    private readonly IDialerAbandonmentPolicyService _abandonmentPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictiveDialerStrategy"/> class.
    /// </summary>
    /// <param name="assignmentService">The assignment service used to reserve agents and activities.</param>
    /// <param name="attemptService">The attempt service that applies compliance and places each call.</param>
    /// <param name="abandonmentPolicy">The abandonment policy that gates and paces predictive dialing.</param>
    public PredictiveDialerStrategy(
        IActivityAssignmentService assignmentService,
        IDialerAttemptService attemptService,
        IDialerAbandonmentPolicyService abandonmentPolicy)
        : base(assignmentService, attemptService)
    {
        _abandonmentPolicy = abandonmentPolicy;
    }

    /// <inheritdoc/>
    public override DialerMode Mode => DialerMode.Predictive;

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
