using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// The protected information of tickets: items such as a login a requester shares (a label and a secret value), kept
/// encrypted in documents of their own, never on the ticket itself, so the ticket's index, list, search, reports, data
/// sets, exports, API, webhooks, workflows, e-mails, notifications and AI tools never carry them. The values are read
/// only by <see cref="ReadValuesAsync"/>, which the caller authorizes; every change and every reveal is an internal event
/// on the ticket's timeline, with the labels, never the values.
/// </summary>
public interface ITicketProtectedInformation
{
    /// <summary>
    /// The longest label.
    /// </summary>
    const int MaxLabelLength = 100;

    /// <summary>
    /// The longest value.
    /// </summary>
    const int MaxValueLength = 2000;

    /// <summary>
    /// The most items a ticket holds.
    /// </summary>
    const int MaxItemsPerTicket = 20;

    /// <summary>
    /// Lists a ticket's items, without their values, oldest first.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<TicketProtectedItem>> ListAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds which of some tickets hold protected information, for the lists' lock badge.
    /// </summary>
    /// <param name="ticketIds">The tickets.</param>
    /// <param name="requesterSharedOnly">Whether to count only the items the requester shared, for the requester's own lists.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ids of those that hold some.</returns>
    Task<IReadOnlySet<string>> FindTicketsWithItemsAsync(IEnumerable<string> ticketIds, bool requesterSharedOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an item to a ticket, its value encrypted, and notes it on the ticket's timeline (the label only).
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="label">What it is, such as "VPN login".</param>
    /// <param name="value">The secret.</param>
    /// <param name="actor">Who adds it.</param>
    /// <param name="fromRequester">Whether the requester adds it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The item, or <see langword="null"/> when the label or value is empty or too long, or the ticket holds the most items.</returns>
    Task<TicketProtectedItem> AddAsync(string ticketId, string label, string value, TicketActor actor, bool fromRequester = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an item from a ticket and notes it on the ticket's timeline.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="actor">Who removes it.</param>
    /// <param name="requesterSharedOnly">Whether only an item the requester shared may be removed (a requester removing theirs).</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it was removed.</returns>
    Task<bool> RemoveAsync(string ticketId, string itemId, TicketActor actor, bool requesterSharedOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a ticket's items with their values. The caller has checked who may see them and their password.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="requesterSharedOnly">Whether to read only the items the requester shared: a requester never sees what agents
    /// keep on their ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The items with their values.</returns>
    Task<IReadOnlyList<TicketProtectedValue>> ReadValuesAsync(string ticketId, bool requesterSharedOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notes on a ticket's timeline that someone showed its items, or tried to and gave a wrong password.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="actor">Who.</param>
    /// <param name="succeeded">Whether the password was right.</param>
    /// <param name="requesterSharedOnly">Whether only the items the requester shared were shown (the requester's own reveal).</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task RecordRevealAsync(string ticketId, TicketActor actor, bool succeeded, bool requesterSharedOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a ticket's items to another, such as the ticket it is merged into.
    /// </summary>
    /// <param name="fromTicketId">The ticket they leave.</param>
    /// <param name="toTicketId">The ticket they go to.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many moved.</returns>
    Task<int> MoveAsync(string fromTicketId, string toTicketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a ticket's items, in the current transaction.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were deleted.</returns>
    Task<int> DeleteAllAsync(string ticketId, CancellationToken cancellationToken = default);
}
