using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Controllers;

/// <summary>
/// Saves a ticket's planning values: impact and urgency, the estimate and the billing type.
/// </summary>
[Admin]
public sealed class TicketPlanningController : Controller
{
    private readonly ITicketService _tickets;
    private readonly ITicketAccessService _access;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public TicketPlanningController(
        ITicketService tickets,
        ITicketAccessService access,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<TicketPlanningController> htmlLocalizer)
    {
        _tickets = tickets;
        _access = access;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Saves the planning values.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="model">The values.</param>
    /// <returns>Back to the ticket.</returns>
    [HttpPost]
    [Admin("ticket/{id}/planning", "TicketsPlanning")]
    public async Task<IActionResult> Update(string id, TicketPlanningUpdateViewModel model)
    {
        var cancellationToken = HttpContext.RequestAborted;

        if (await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (!await _access.CanAsync(User, ticket, TicketOperation.Edit, cancellationToken))
        {
            return Forbid();
        }

        int? minutes = null;

        if (!string.IsNullOrWhiteSpace(model?.Estimate))
        {
            minutes = TicketTimeDuration.ParseMinutes(model.Estimate);

            if (minutes is null)
            {
                await _notifier.ErrorAsync(H["Enter the estimated time, such as 45m, 2h or 1:30."]);

                return RedirectToAction("Display", "Tickets", new { id });
            }
        }

        var result = await _tickets.UpdateAsync(
            id,
            new TicketUpdate
            {
                SetPlanning = true,
                Impact = model?.Impact,
                Urgency = model?.Urgency,
                BillingType = model?.BillingType,
                EstimatedStartUtc = model?.EstimatedStart is { } start ? await _localClock.ToUtcLenientAsync(start) : null,
                EstimatedMinutes = minutes,
            },
            TicketActor.User(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name),
            cancellationToken);

        if (!result.Succeeded)
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);
        }

        return RedirectToAction("Display", "Tickets", new { id });
    }
}
