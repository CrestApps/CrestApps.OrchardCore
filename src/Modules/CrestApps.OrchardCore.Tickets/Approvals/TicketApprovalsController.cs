using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Approvals;

/// <summary>
/// The approval page of a ticket: what is asked and the decision, for the approvers of its steps, who may not see the
/// ticket otherwise.
/// </summary>
[Feature(TicketsConstants.Features.Approvals)]
public sealed class TicketApprovalsController : Controller
{
    private readonly ITicketService _tickets;
    private readonly ITicketAccessService _access;
    private readonly ITimelineService _timeline;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public TicketApprovalsController(ITicketService tickets, ITicketAccessService access, ITimelineService timeline, INotifier notifier, IHtmlLocalizer<TicketApprovalsController> htmlLocalizer)
    {
        _tickets = tickets;
        _access = access;
        _timeline = timeline;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows what a ticket asks and its approval, to an approver of one of its steps or someone who may see the ticket.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <returns>The page.</returns>
    [Admin("ticket/{id}/approval", "TicketApprovals")]
    public async Task<IActionResult> Index(string id)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket || ticket.GetOrCreate<TicketPart>() is not { Approval: { } approval } part)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isApprover = approval.Steps.Any(step => step.ApproverIds.Contains(userId, StringComparer.Ordinal));
        var canOpen = await _access.CanAsync(User, ticket, TicketOperation.ViewAssigned, cancellationToken);

        if (!isApprover && !canOpen)
        {
            return Forbid();
        }

        // The request as the requester wrote it: its first public message.
        var query = new TimelineQuery { Subject = new RecordRef(TicketsConstants.RecordKind, id), Kinds = [TicketsConstants.EntryKinds.Reply], Take = 1 };
        var newest = await _timeline.ListAsync(query, cancellationToken);

        if (newest.Total > 1)
        {
            query.Skip = newest.Total - 1;
            newest = await _timeline.ListAsync(query, cancellationToken);
        }

        return View(new TicketApprovalViewModel
        {
            TicketId = id,
            Ticket = ticket,
            Approval = approval,
            CanDecide = approval.Current?.ApproverIds.Contains(userId, StringComparer.Ordinal) == true,
            CanOpenTicket = canOpen,
            RequesterName = part.RequesterContact?.Name ?? part.RequesterContact?.Email,
            FirstMessage = newest.Entries.Count > 0 ? newest.Entries[0].Body : null,
        });
    }

    /// <summary>
    /// Records the user's decision on the step the ticket waits for.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="approve">Whether they approve.</param>
    /// <param name="comment">Their comment.</param>
    /// <param name="backToTicket">Whether they decided from the ticket page, which they go back to.</param>
    /// <returns>Back to the ticket or the approval page.</returns>
    [HttpPost]
    [Admin("ticket/{id}/approval/decide", "TicketApprovalsDecide")]
    public async Task<IActionResult> Decide(string id, bool approve, string comment, bool backToTicket = false)
    {
        var result = await _tickets.DecideApprovalAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, approve, comment, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);
        }
        else
        {
            await _notifier.SuccessAsync(approve ? H["You approved the request."] : H["You refused the request; the requester was told why."]);
        }

        return backToTicket
            ? RedirectToAction("Display", "Tickets", new { area = TicketsConstants.Features.Tickets, id })
            : RedirectToAction(nameof(Index), new { id });
    }
}
