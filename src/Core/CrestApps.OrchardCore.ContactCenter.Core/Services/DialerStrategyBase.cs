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
    /// Gets the maximum number of attempts the strategy may start in a single pacing cycle. It is asynchronous
    /// because a mode whose pacing depends on measured campaign statistics has to read them, and stashing that
    /// answer on a field would leak it between cycles and across threads.
    /// </summary>
    /// <param name="profile">The dialer profile being run.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The per-cycle attempt limit; zero to place nothing.</returns>
    protected abstract Task<int> GetMaxAttemptsPerCycleAsync(DialerProfile profile, CancellationToken cancellationToken);

    /// <inheritdoc/>
    public async Task<int> RunCycleAsync(DialerProfile profile, string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var maxAttempts = await GetMaxAttemptsPerCycleAsync(profile, cancellationToken);

        // Zero is a decision, not a default: a paced mode returns it when its own safety policy says nothing may
        // be dialled, and clamping it up to one would place the call the policy just refused.
        if (maxAttempts < 1)
        {
            return 0;
        }

        var attempted = 0;
        var started = 0;

        var reservation = await _assignmentService.AssignNextAsync(queueId, cancellationToken);

        while (reservation is not null && attempted < maxAttempts)
        {
            attempted++;

            if (await _attemptService.TryDialAsync(profile, reservation, cancellationToken))
            {
                started++;
            }

            if (attempted < maxAttempts)
            {
                reservation = await _assignmentService.AssignNextAsync(queueId, cancellationToken);
            }
        }

        return started;
    }
}
