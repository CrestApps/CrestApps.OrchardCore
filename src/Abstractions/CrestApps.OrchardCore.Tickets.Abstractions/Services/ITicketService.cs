using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Opens and changes tickets. Every change takes the ticket's lock, records what changed on its timeline, keeps its
/// service-level timers in step and tells the <see cref="ITicketObserver"/>s. It doesn't check who may make the
/// change; a page that acts for a user asks <see cref="ITicketAccessService"/> first.
/// </summary>
public interface ITicketService
{
    /// <summary>
    /// Opens a ticket: numbers it, files it under a desk, team and priority, writes what was asked as its first reply,
    /// starts its service-level timers and assigns it when it names no assignee and its team has members.
    /// </summary>
    /// <param name="request">The new ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket, or why it wasn't opened.</returns>
    Task<TicketResult> CreateAsync(TicketCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a ticket by its content item id.
    /// </summary>
    /// <param name="ticketId">The ticket's content item id.</param>
    /// <returns>The ticket, or <see langword="null"/>.</returns>
    Task<ContentItem> GetAsync(string ticketId);

    /// <summary>
    /// Gets a ticket by its number.
    /// </summary>
    /// <param name="number">The ticket number, such as <c>T-1001</c>.</param>
    /// <returns>The ticket, or <see langword="null"/>.</returns>
    Task<ContentItem> FindByNumberAsync(string number);

    /// <summary>
    /// Changes a ticket's properties. A change of status follows the life cycle, and a closed or merged ticket can't
    /// change.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="update">What changes.</param>
    /// <param name="actor">Who changes it, for the timeline.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket, or why it wasn't changed.</returns>
    Task<TicketResult> UpdateAsync(string ticketId, TicketUpdate update, TicketActor actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a public reply or an internal note. The first public agent reply completes the first-response target; a
    /// requester's reply starts a next-response target and reopens a solved ticket inside the desk's reopen window; a
    /// reply to a closed ticket opens a follow-up ticket instead.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="reply">The reply.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket the reply went on, which is the follow-up when the ticket was closed.</returns>
    Task<TicketResult> ReplyAsync(string ticketId, TicketReply reply, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a ticket to the next available member of its team, as the team's strategy chooses, with the type's
    /// required skills.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="actor">Who asks, for the timeline.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket, or why it couldn't be assigned.</returns>
    Task<TicketResult> AutoAssignAsync(string ticketId, TicketActor actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges a ticket into another: its requester and participants join the other as participants, both get a note
    /// that links them, and the merged ticket closes.
    /// </summary>
    /// <param name="ticketId">The ticket that goes away.</param>
    /// <param name="intoTicketId">The ticket it goes into.</param>
    /// <param name="actor">Who merges, for the timeline.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket it went into, or why it couldn't be merged.</returns>
    Task<TicketResult> MergeAsync(string ticketId, string intoTicketId, TicketActor actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links a ticket to a record, or removes the link.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="record">The record.</param>
    /// <param name="kind">How they relate; <see langword="null"/> removes the link.</param>
    /// <param name="actor">Who links, for the timeline.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket, or why it couldn't be linked.</returns>
    Task<TicketResult> LinkAsync(string ticketId, RecordRef record, string kind, TicketActor actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a ticket, with its timeline and service-level timers.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket as it was, or why it couldn't be deleted.</returns>
    Task<TicketResult> DeleteAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a ticket's own fields (the ticket type's fields and its title) under the ticket's lock, and records the
    /// change on its timeline. The ticket's values (status, priority, desk, team, assignee) change through
    /// <see cref="UpdateAsync"/>.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="edit">Changes the item; returns <see langword="false"/> when the change is refused.</param>
    /// <param name="actor">Who edits, for the timeline.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The ticket, or why it couldn't change.</returns>
    Task<TicketResult> EditAsync(string ticketId, Func<ContentItem, Task<bool>> edit, TicketActor actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists tickets.
    /// </summary>
    /// <param name="query">Which tickets, narrowed to those the user may view.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A page of tickets and how many match.</returns>
    Task<(IReadOnlyList<ContentItem> Tickets, int Total)> ListAsync(TicketScopedQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the requester's rating of a solved or closed ticket; a second rating replaces the first.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="score">The score, from <see cref="TicketSatisfaction.MinScore"/> to <see cref="TicketSatisfaction.MaxScore"/>.</param>
    /// <param name="comment">What the requester added, if anything.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TicketResult> RateAsync(string ticketId, int score, string comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the requester was asked to rate the ticket, so they aren't asked again.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TicketResult> RecordRatingRequestAsync(string ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a ticket wait for approval: the steps, in order, each decided by one of its approvers. The ticket moves to
    /// the first waiting status while it waits.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="steps">The steps; a step without an approver is left out.</param>
    /// <param name="waitingStatusId">The status the ticket waits in, or <see langword="null"/> for the first waiting status.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TicketResult> StartApprovalAsync(string ticketId, IReadOnlyList<TicketApprovalStep> steps, string waitingStatusId = null, TicketActor actor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an approver's decision on the step a ticket waits for. An approval moves to the next step, or, after
    /// the last, back to work; a refusal solves the ticket and tells the requester why.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="approverId">The user deciding, one of the step's approvers.</param>
    /// <param name="approverName">Their name.</param>
    /// <param name="approve">Whether they approve.</param>
    /// <param name="comment">Their comment; a refusal needs one, which the requester reads.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<TicketResult> DecideApprovalAsync(string ticketId, string approverId, string approverName, bool approve, string comment, CancellationToken cancellationToken = default);
}
