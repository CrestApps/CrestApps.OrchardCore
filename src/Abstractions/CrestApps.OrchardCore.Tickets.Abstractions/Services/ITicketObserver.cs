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

    /// <summary>
    /// A ticket was deleted.
    /// </summary>
    /// <param name="ticket">The ticket, as it was.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task DeletedAsync(ContentItem ticket, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A ticket was given to another assignee, or unassigned. Raised by every change that does it (an agent, team
    /// assignment, a workflow), after the ticket is saved.
    /// </summary>
    /// <param name="ticket">The ticket; its <see cref="Models.TicketPart.AssigneeId"/> is the new assignee, if any.</param>
    /// <param name="previousAssigneeId">Who had it before, if anyone.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task AssignedAsync(ContentItem ticket, string previousAssigneeId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A ticket moved to another status: solved, reopened, closed or any other move. Raised by every change that does
    /// it, after the ticket is saved.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="previous">The status it had, if it still exists.</param>
    /// <param name="current">The status it has now.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task StatusChangedAsync(ContentItem ticket, Models.TicketStatus previous, Models.TicketStatus current, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// A ticket is close to missing a service-level target, or missed it. Raised once for each.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="metric">The target: first response, next response or resolution.</param>
    /// <param name="breached">Whether the target was missed; otherwise it is close.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ServiceLevelAsync(ContentItem ticket, string metric, bool breached, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Called after a requester rated a ticket.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="satisfaction">The rating.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task RatedAsync(ContentItem ticket, Models.TicketSatisfaction satisfaction, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
