using System.Security.Claims;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.TimeTracker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// The ticket page's <b>Time</b> panel: the time each person spent on the ticket and its entries, a button to start or
/// stop working on it and a form to log time spent, for an agent who may answer it, on a desk that records time.
/// </summary>
[RequireFeatures(TimeTrackerConstants.Features.TimeTracker)]
internal sealed class TicketTimeDisplayDriver : ContentPartDisplayDriver<TicketPart>
{
    private readonly TicketTime _time;
    private readonly ITicketAccessService _access;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public TicketTimeDisplayDriver(TicketTime time, ITicketAccessService access, IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _time = time;
        _access = access;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override async Task<IDisplayResult> DisplayAsync(TicketPart part, BuildPartDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? default;

        if (context.DisplayType != TicketsConstants.DisplayTypes.DetailAdmin
            || user is null
            || await _time.StageIdAsync(part.ContentItem) is null)
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var summary = await _time.SummarizeAsync(part.ContentItem, userId, cancellationToken);
        var canWork = !part.ClosedUtc.HasValue && await _access.CanAsync(user, part.ContentItem, TicketOperation.Reply, cancellationToken);

        if (summary.People.Count == 0 && !canWork)
        {
            return null;
        }

        var canCorrect = await _authorizationService.AuthorizeAsync(user, TimeTrackerPermissions.ManageTimeEntries);

        return Initialize<TicketTimeViewModel>("TicketPart_Time", model =>
        {
            model.CanCorrect = canCorrect;
            model.TicketId = part.ContentItem.ContentItemId;
            model.Summary = summary;
            model.CanWork = canWork;
        }).Location(TicketsConstants.DisplayTypes.DetailAdmin, "Sidebar:6");
    }
}
