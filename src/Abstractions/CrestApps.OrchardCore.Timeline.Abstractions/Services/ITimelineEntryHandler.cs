using CrestApps.OrchardCore.Timeline.Models;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Takes part in adding timeline entries: checks or completes an entry before it is saved, and acts on it after, such
/// as telling the people it mentions. Register one with
/// <see cref="TimelineServiceCollectionExtensions.AddTimelineEntryHandler{THandler}"/>.
/// </summary>
public interface ITimelineEntryHandler
{
    /// <summary>
    /// Runs before an entry is saved. Add a message to <paramref name="errors"/> to refuse it.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="errors">The reasons the entry is refused.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the check is done.</returns>
    Task AddingAsync(TimelineEntry entry, IList<string> errors, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Runs after an entry is saved.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the work is done.</returns>
    Task AddedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Runs after an entry's body was redacted.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the work is done.</returns>
    Task RedactedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after a change that doesn't rewrite what an entry says was saved, such as a delivery status.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task UpdatedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an entry was deleted with its record, such as to delete the files attached to it.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task DeletedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
