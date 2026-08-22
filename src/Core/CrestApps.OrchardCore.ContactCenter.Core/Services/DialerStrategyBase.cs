using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the shared reserve-then-dial pacing loop used by automated dialing strategies. Each cycle
/// reserves an agent through routing and asks the attempt service to place a compliant call, stopping
/// once the mode's per-cycle limit is reached or no further agent can be reserved.
/// </summary>
public abstract class DialerStrategyBase : IDialerStrategy
{
    private readonly IActivityAssignmentService _assignmentService;
    private readonly IDialerAttemptService _attemptService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerStrategyBase"/> class.
    /// </summary>
    /// <param name="assignmentService">The assignment service used to reserve agents and activities.</param>
    /// <param name="attemptService">The attempt service that applies compliance and places each call.</param>
    protected DialerStrategyBase(
        IActivityAssignmentService assignmentService,
        IDialerAttemptService attemptService)
    {
        _assignmentService = assignmentService;
        _attemptService = attemptService;
    }

    /// <inheritdoc/>
    public abstract DialerMode Mode { get; }

    /// <summary>
    /// Gets the maximum number of attempts the strategy may start in a single pacing cycle.
    /// </summary>
    /// <param name="profile">The dialer profile being run.</param>
    /// <returns>The per-cycle attempt limit.</returns>
    protected abstract int GetMaxAttemptsPerCycle(DialerProfile profile);

    /// <inheritdoc/>
    public async Task<int> RunCycleAsync(DialerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var maxAttempts = Math.Max(GetMaxAttemptsPerCycle(profile), 1);
        var attempted = 0;
        var started = 0;

        var reservation = await _assignmentService.AssignNextAsync(profile.QueueId, cancellationToken);

        while (reservation is not null && attempted < maxAttempts)
        {
            attempted++;

            if (await _attemptService.TryDialAsync(profile, reservation, cancellationToken))
            {
                started++;
            }

            if (attempted < maxAttempts)
            {
                reservation = await _assignmentService.AssignNextAsync(profile.QueueId, cancellationToken);
            }
        }

        return started;
    }
}
