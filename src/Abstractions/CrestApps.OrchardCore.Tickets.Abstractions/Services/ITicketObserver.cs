using CrestApps.OrchardCore.Timeline.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Hears what happens to tickets once it is saved: the one place workflows, webhooks, notifications and the audit trail
/// are fed from. Register one with <c>AddTicketObserver</c>. An observer that fails is logged and never undoes the
/// change.
/// </summary>
public interface ITicketObserver
{
    /// <summary>
    /// A ticket was opened.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A ticket's properties changed.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="changes">What changed, by field, such as <c>Status</c> or <c>Assignee</c>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task UpdatedAsync(ContentItem ticket, IReadOnlyList<TimelineFieldChange> changes, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A reply or a note was added.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="entry">The timeline entry.</param>
    /// <param name="fromRequester">Whether the requester wrote it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task RepliedAsync(ContentItem ticket, TimelineEntry entry, bool fromRequester, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A ticket was merged into another.
    /// </summary>
    /// <param name="ticket">The ticket that went away.</param>
    /// <param name="into">The ticket it went into.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task MergedAsync(ContentItem ticket, ContentItem into, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
