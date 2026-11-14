using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Services;

/// <summary>
/// Keeps the service level timers of records. The module that owns a record tells the tracker what happens to it
/// (created, waiting, answered, resolved); the tracker counts the time on the policy's hours, and a background task
/// raises the warning and the breach through the registered <see cref="IServiceLevelObserver"/>s.
/// </summary>
/// <remarks>
/// Call the tracker while holding the record's own lock, so two changes to one record don't race on its timers.
/// </remarks>
public interface IServiceLevelTracker
{
    /// <summary>
    /// Starts the timer of a metric for a record. A timer of the same metric that is still active is cancelled first,
    /// so a metric that repeats (the next response) starts a new cycle.
    /// </summary>
    /// <param name="start">The record, the metric, the policy and the priority.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timer, or <see langword="null"/> when the policy isn't found or sets no target for the metric at
    /// that priority.</returns>
    Task<ServiceLevelTimer> StartAsync(ServiceLevelStart start, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses the running timers of a record.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="metric">One metric, or <see langword="null"/> for every metric.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timers that were paused.</returns>
    Task<IReadOnlyList<ServiceLevelTimer>> PauseAsync(RecordRef subject, string metric = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes the paused timers of a record. The time left is added on the timer's hours from now.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="metric">One metric, or <see langword="null"/> for every metric.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timers that were resumed.</returns>
    Task<IReadOnlyList<ServiceLevelTimer>> ResumeAsync(RecordRef subject, string metric = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the active timer of a metric: the work it measured was done.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="metric">The metric.</param>
    /// <param name="atUtc">When the work was done, in UTC, or <see langword="null"/> for now.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The completed timer, or <see langword="null"/> when the record has no active timer of the metric.</returns>
    Task<ServiceLevelTimer> CompleteAsync(RecordRef subject, string metric, DateTime? atUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a metric again after it was answered, such as when a solved record is reopened: its newest answered cycle
    /// goes on from the time it had used, keeping a warning or breach already raised. A metric still counting is left as
    /// it is.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="metric">The metric.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The counting timer, or <see langword="null"/> when the metric has none to reopen.</returns>
    Task<ServiceLevelTimer> ReopenAsync(RecordRef subject, string metric, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the active timers of a record without measuring them, such as when the record is closed as spam or
    /// deleted.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="metric">One metric, or <see langword="null"/> for every metric.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timers that were cancelled.</returns>
    Task<IReadOnlyList<ServiceLevelTimer>> CancelAsync(RecordRef subject, string metric = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the active timers of a record new targets, after its priority or its policy changed. The time already
    /// used counts against the new target. A metric the new policy has no target for is cancelled.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="policyId">The policy to read the targets from.</param>
    /// <param name="priority">The record's priority key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timers that are still active.</returns>
    Task<IReadOnlyList<ServiceLevelTimer>> RetargetAsync(RecordRef subject, string policyId, string priority, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timers of a record, newest first.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="activeOnly">Whether only running and paused timers are returned.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The timers.</returns>
    Task<IReadOnlyList<ServiceLevelTimer>> GetTimersAsync(RecordRef subject, bool activeOnly = false, CancellationToken cancellationToken = default);
}
