using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerPacingCycleRecorder"/>.
/// </summary>
/// <remarks>
/// <para>
/// A cycle's record is written through <see cref="IContactCenterScopeExecutor"/> on a scope of its own that commits on its
/// own, once the cycle's scope has ended. Writing it with the cycle's own session would lose exactly the cycles worth
/// seeing: one that threw, lost a write race or ran out of time is rolled back, and its record with it. Waiting for the
/// cycle's scope to end also keeps the write from contending with a transaction the cycle still holds open.
/// </para>
/// <para>
/// A cycle that ends the way the queue's latest record did without placing a call is folded into that record rather than
/// added, for up to <see cref="FoldWindow"/>, so a queue that keeps finding no agent costs one row an hour.
/// </para>
/// </remarks>
internal sealed class DialerPacingCycleRecorder : IDialerPacingCycleRecorder
{
    /// <summary>
    /// How long consecutive cycles that ended the same way may be folded into one record.
    /// </summary>
    internal static readonly TimeSpan FoldWindow = TimeSpan.FromHours(1);

    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingCycleRecorder"/> class.
    /// </summary>
    /// <param name="scopeExecutor">The executor that writes the record on a scope of its own.</param>
    /// <param name="clock">The clock that stamps when a cycle completed.</param>
    /// <param name="logger">The logger.</param>
    public DialerPacingCycleRecorder(
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock,
        ILogger<DialerPacingCycleRecorder> logger)
    {
        _scopeExecutor = scopeExecutor;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(DialerPacingCycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        Complete(cycle);

        // Written once the cycle's own scope has ended, so the record never shares, or waits on, the cycle's transaction.
        if (!_scopeExecutor.ScheduleAfterCommit<IServiceProvider>(services => WriteAsync(services, cycle)))
        {
            await _scopeExecutor.ExecuteAsync(services => WriteAsync(services, cycle));
        }
    }

    /// <inheritdoc/>
    public async Task RecordNowAsync(DialerPacingCycle cycle)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        Complete(cycle);

        await _scopeExecutor.ExecuteAsync(services => WriteAsync(services, cycle));
    }

    /// <summary>
    /// Whether a cycle that just ended may be folded into the queue's latest record: both ended the same way without
    /// placing a call, with the same profile, and the record began less than <see cref="FoldWindow"/> before the cycle.
    /// </summary>
    /// <param name="latest">The queue's latest record, or <see langword="null"/>.</param>
    /// <param name="next">The cycle that just ended.</param>
    internal static bool CanFold(DialerPacingCycle latest, DialerPacingCycle next)
        => latest is not null &&
            next is not null &&
            latest.Outcome == next.Outcome &&
            IsFoldable(next.Outcome) &&
            string.Equals(latest.DialerProfileId, next.DialerProfileId, StringComparison.Ordinal) &&
            next.CompletedUtc >= latest.CompletedUtc &&
            next.StartedUtc - latest.FirstStartedUtc < FoldWindow;

    /// <summary>
    /// Folds a cycle that just ended into the queue's latest record.
    /// </summary>
    /// <param name="latest">The queue's latest record.</param>
    /// <param name="next">The cycle that just ended.</param>
    internal static void Fold(DialerPacingCycle latest, DialerPacingCycle next)
    {
        latest.CycleCount += Math.Max(1, next.CycleCount);
        latest.StartedUtc = next.StartedUtc;
        latest.CompletedUtc = next.CompletedUtc;
        latest.Detail = next.Detail ?? latest.Detail;
        latest.RoutingSucceeded += next.RoutingSucceeded;
        latest.RoutingNoAgent += next.RoutingNoAgent;
        latest.Suppressed += next.Suppressed;
    }

    private void Complete(DialerPacingCycle cycle)
    {
        cycle.CompletedUtc = _clock.UtcNow;

        if (cycle.StartedUtc == default || cycle.StartedUtc > cycle.CompletedUtc)
        {
            cycle.StartedUtc = cycle.CompletedUtc;
        }

        cycle.FirstStartedUtc = cycle.StartedUtc;
        cycle.CycleCount = Math.Max(1, cycle.CycleCount);

        if (string.IsNullOrEmpty(cycle.CampaignId) && !string.IsNullOrEmpty(cycle.QueueId))
        {
            cycle.CampaignId = ContactCenterConstants.CampaignQueue.GetCampaignId(cycle.QueueId);
        }
    }

    private async Task WriteAsync(IServiceProvider services, DialerPacingCycle cycle)
    {
        try
        {
            var store = services.GetRequiredService<IDialerPacingCycleStore>();
            var latest = await store.FindLatestByQueueAsync(cycle.QueueId, outcomes: null, CancellationToken.None);

            if (CanFold(latest, cycle))
            {
                Fold(latest, cycle);
                await store.UpdateAsync(latest, CancellationToken.None);
            }
            else
            {
                await store.CreateAsync(cycle, CancellationToken.None);
            }

            await services.GetRequiredService<ISession>().SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Pacing goes on whether or not its record could be written; the record is what this logs instead.
            _logger.LogWarning(
                ex,
                "The record of a pacing cycle of queue '{QueueId}' could not be written. The cycle ended with outcome {Outcome}.",
                cycle.QueueId.SanitizeLogValue(),
                cycle.Outcome);
        }
    }

    private static bool IsFoldable(DialerPacingCycleOutcome outcome)
        => outcome is DialerPacingCycleOutcome.NothingDue
            or DialerPacingCycleOutcome.NoAgents
            or DialerPacingCycleOutcome.HeldBack
            or DialerPacingCycleOutcome.LockNotAcquired;
}
