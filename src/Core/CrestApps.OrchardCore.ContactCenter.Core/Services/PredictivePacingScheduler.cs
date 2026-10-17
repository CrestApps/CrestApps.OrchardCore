using System.Collections.Concurrent;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IPredictivePacingScheduler"/>.
/// </summary>
/// <remarks>
/// A request arms one deadline per queue (<c>predictive-pacing:{queueId}</c>) a debounce after it, and further requests
/// while it is armed are merged into it rather than pushing it back, so a busy queue is still paced. The run services any
/// answered call still waiting for an agent first, then runs the queue's pacing cycle through the dialer, and asks to run
/// again after the pacing interval while the cycle keeps placing calls. A queue whose waiting work is not dialed by an
/// over-dialing profile is remembered as such for a minute, so the events of other campaigns cost a lookup at most once a
/// minute.
/// </remarks>
public sealed class PredictivePacingScheduler : IPredictivePacingScheduler
{
    private static readonly TimeSpan _notOverDialedMemory = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _notOverDialedUntil = new(StringComparer.Ordinal);
    private readonly IContactCenterDeadlineScheduler _deadlineScheduler;
    private readonly IClock _clock;
    private readonly ContactCenterPredictiveDialingOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictivePacingScheduler"/> class.
    /// </summary>
    /// <param name="deadlineScheduler">The in-process deadline scheduler the runs are held on.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The predictive dialing options.</param>
    /// <param name="logger">The logger.</param>
    public PredictivePacingScheduler(
        IContactCenterDeadlineScheduler deadlineScheduler,
        IClock clock,
        IOptions<ContactCenterPredictiveDialingOptions> options,
        ILogger<PredictivePacingScheduler> logger)
    {
        _deadlineScheduler = deadlineScheduler;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// The key of the deadline that paces the queue.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    public static string GetDeadlineKey(string queueId)
        => $"predictive-pacing:{queueId}";

    /// <inheritdoc/>
    public void Request(string queueId)
        => Request(queueId, _options.PacingDebounce);

    /// <inheritdoc/>
    public void RequestRetry(string queueId)
        => Request(queueId, _options.PacingLockRetryDelay);

    private void Request(string queueId, TimeSpan delay)
    {
        if (!ContactCenterConstants.IsCampaignQueue(queueId))
        {
            return;
        }

        var now = _clock.UtcNow;

        if (_notOverDialedUntil.TryGetValue(queueId, out var until))
        {
            if (until > now)
            {
                return;
            }

            _notOverDialedUntil.TryRemove(queueId, out _);
        }

        // Merged into the run already armed: pushing it back on every event would let a busy queue starve.
        if (!_pending.TryAdd(queueId, 0))
        {
            return;
        }

        _deadlineScheduler.Schedule(GetDeadlineKey(queueId), now.Add(delay), (services, cancellationToken) => RunAsync(services, queueId, cancellationToken));
    }

    private async Task<DateTime?> RunAsync(IServiceProvider services, string queueId, CancellationToken cancellationToken)
    {
        // A request arriving from here on arms the next run.
        _pending.TryRemove(queueId, out _);

        var queueItemStore = services.GetRequiredService<IQueueItemStore>();
        var profileReader = services.GetRequiredService<IDialerProfileReader>();

        // Answered calls already waiting for an agent come before any new call. They are connected in a scope of their
        // own, so a claim that loses a race cannot spend the session the pacing cycle commits.
        if (services.GetService<IPredictiveAgentConnector>() is not null)
        {
            await services.GetRequiredService<IContactCenterScopeExecutor>().ExecuteAsync<IPredictiveAgentConnector>(connector =>
                connector.ServiceWaitingAsync(queueId, cancellationToken));
        }

        var head = await queueItemStore.FindNextWaitingAsync(queueId, cancellationToken);

        if (head is null || string.IsNullOrEmpty(head.DialerProfileId))
        {
            return null;
        }

        var profile = await profileReader.FindByIdAsync(head.DialerProfileId, cancellationToken);

        if (profile is null ||
            !profile.Enabled ||
            profile.Mode != DialerMode.Predictive ||
            profile.PredictivePacingModel != PredictivePacingModel.OverDial)
        {
            _notOverDialedUntil[queueId] = _clock.UtcNow.Add(_notOverDialedMemory);

            return null;
        }

        var recorder = services.GetService<IDialerPacingCycleRecorder>();
        var observation = recorder is null ? null : services.GetService<DialerPacingCycleObservation>();
        var startedUtc = _clock.UtcNow;
        int started;

        observation?.Begin();

        try
        {
            started = await services.GetRequiredService<IDialerService>().RunCycleAsync(profile, queueId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && recorder is not null)
        {
            observation?.End();

            // The scope this cycle ran on fails with the exception, and what waits for a failed scope never runs.
            await recorder.RecordNowAsync(new DialerPacingCycle
            {
                QueueId = queueId,
                DialerProfileId = profile.ItemId,
                Outcome = DialerPacingCycleOutcome.Failed,
                Detail = DialerPacingScheduler.DescribeFailure(ex),
                StartedUtc = startedUtc,
            });

            throw;
        }

        observation?.End();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Paced over-dialing queue '{QueueId}' on request: {Started} call(s) placed.", queueId.SanitizeLogValue(), started);
        }

        if (recorder is not null)
        {
            var record = await DialerPacingScheduler.DescribeCompletedCycleAsync(services, queueId, profile, startedUtc, started, observation, cancellationToken);

            if (started == 0)
            {
                await DescribeOverDialDecisionAsync(services, record, startedUtc, cancellationToken);
            }

            await recorder.RecordAsync(record);
        }

        // While calls are being placed the queue is paced again soon; once nothing is placed it waits for the next event
        // (an agent freeing up, a call answered or ending) or the minute dialer pacing task.
        return started > 0 ? _clock.UtcNow.Add(_options.PacingInterval) : null;
    }

    /// <summary>
    /// Refines the record of an over-dial cycle that placed nothing with what the over-dial pacer decided, which it keeps on
    /// the queue's pacing record.
    /// </summary>
    /// <param name="services">The services of the scope the cycle ran on.</param>
    /// <param name="record">The cycle's record.</param>
    /// <param name="startedUtc">When the cycle started; a pacing decision older than this was not made by it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    internal static async Task DescribeOverDialDecisionAsync(
        IServiceProvider services,
        DialerPacingCycle record,
        DateTime startedUtc,
        CancellationToken cancellationToken)
    {
        var stateStore = services.GetService<IPredictivePacingStateStore>();

        if (stateStore is null)
        {
            return;
        }

        PredictivePacingState state;

        try
        {
            state = await stateStore.FindByQueueIdAsync(record.QueueId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return;
        }

        var decision = state?.LastCycleUtc is { } lastCycleUtc && lastCycleUtc >= startedUtc
            ? state.LastDecision
            : null;

        // The pacer records every cycle it runs under the queue's pacing lock. One that left no decision behind never got
        // the lock, or was rolled back after failing or losing a write race, which the pacer logs.
        if (decision is null)
        {
            record.Outcome = DialerPacingCycleOutcome.LockNotAcquired;
            record.Detail = "The over-dial cycle did not run: another cycle held the queue's pacing lock, or it failed or lost a write race (see the log).";

            return;
        }

        if (decision.Mode == PredictivePacingDecisionMode.Suppressed)
        {
            record.Outcome = DialerPacingCycleOutcome.HeldBack;
            record.Detail = $"The abandonment policy does not permit the profile to dial ({decision.Reason}).";

            return;
        }

        if (decision.Mode == PredictivePacingDecisionMode.OverDial && decision.DialCount == 0)
        {
            if (decision.AvailableAgents + decision.FreeingAgents == 0)
            {
                record.Outcome = DialerPacingCycleOutcome.NoAgents;
                record.Detail = "No agent is available or about to be.";
            }
            else
            {
                record.Outcome = DialerPacingCycleOutcome.HeldBack;
                record.Detail = $"The calls already in flight cover the agents ({decision.Reason}).";
            }
        }
    }
}
