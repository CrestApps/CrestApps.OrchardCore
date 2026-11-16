using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Controllers;

/// <summary>
/// Time an agent logs on a ticket after the fact, with Time Tracker on.
/// </summary>
[Admin]
public sealed class TicketTimeController : Controller
{
    private readonly ITicketService _tickets;
    private readonly ITicketAccessService _access;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public TicketTimeController(
        ITicketService tickets,
        ITicketAccessService access,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<TicketTimeController> htmlLocalizer)
    {
        _tickets = tickets;
        _access = access;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Logs time the user spent on the ticket.
    /// </summary>
    /// <param name="id">The ticket.</param>
    /// <param name="model">How long, when, what and whether it is billed.</param>
    /// <returns>Back to the ticket.</returns>
    [HttpPost]
    [Admin("ticket/{id}/time/log", "TicketsLogTime")]
    public async Task<IActionResult> Log(string id, TicketTimeLogViewModel model)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var time = HttpContext.RequestServices.GetService<TicketTime>();

        if (time is null || await _tickets.GetAsync(id) is not { } ticket)
        {
            return NotFound();
        }

        if (ticket.GetOrCreate<TicketPart>().ClosedUtc.HasValue
            || !await _access.CanAsync(User, ticket, TicketOperation.Reply, cancellationToken))
        {
            return Forbid();
        }

        var minutes = TicketTimeDuration.ParseMinutes(model?.Spent);

        if (minutes is null)
        {
            await _notifier.ErrorAsync(H["Enter the time spent, such as 30, 45m, 1h 30m or 1:30."]);

            return RedirectToAction("Display", "Tickets", new { id });
        }

        var result = await time.LogAsync(
            ticket,
            new TicketTimeLog
            {
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                UserName = await NameAsync(cancellationToken),
                Actor = User.Identity?.Name,
                Minutes = minutes.Value,
                StartUtc = model.Started is { } started ? await _localClock.ToUtcLenientAsync(started) : null,
                Note = model.Note,
                Billable = model.Billable,
            },
            cancellationToken);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Your time is logged on the ticket."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", string.Join(" ", result.Errors)]);
        }

        return RedirectToAction("Display", "Tickets", new { id });
    }

    // The name the user's time shows, as their own clocking in gives it.
    private async Task<string> NameAsync(CancellationToken cancellationToken)
    {
        var name = User.Identity?.Name;

        if (HttpContext.RequestServices.GetService<IDisplayNameProvider>() is { } names
            && await HttpContext.RequestServices.GetRequiredService<UserManager<IUser>>().GetUserAsync(User) is { } user)
        {
            name = await names.GetAsync(user, cancellationToken) ?? name;
        }

        return name;
    }
}
