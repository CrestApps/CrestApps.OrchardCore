using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The ticket page's <b>Planning</b> panel: impact and urgency, the estimate, the billing type and who solved it.
/// </summary>
internal sealed class TicketPlanningDisplayDriver : ContentPartDisplayDriver<TicketPart>
{
    private readonly ITicketAccessService _access;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ILocalClock _localClock;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketPlanningDisplayDriver(ITicketAccessService access, ICatalog<TicketDesk> desks, ILocalClock localClock, IHttpContextAccessor httpContextAccessor)
    {
        _access = access;
        _desks = desks;
        _localClock = localClock;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(TicketPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != TicketsConstants.DisplayTypes.DetailAdmin)
        {
            return null;
        }

        return Initialize<TicketPlanningViewModel>("TicketPart_Planning", async model =>
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var cancellationToken = httpContext?.RequestAborted ?? default;

            model.TicketId = part.ContentItem.ContentItemId;
            model.Part = part;
            model.EstimatedStart = part.EstimatedStartUtc is { } start
                ? (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(start, DateTimeKind.Utc)))).DateTime
                : null;
            model.HasPriorityMatrix = await _desks.FindByIdAsync(part.DeskId, cancellationToken) is { PriorityMatrix.Count: > 0 };
            var user = httpContext?.User;
            model.CanEdit = user is not null
                && !TicketLifecycle.IsReadOnly(part)
                && await _access.CanAsync(user, part.ContentItem, TicketOperation.Edit, cancellationToken);

            if (!string.IsNullOrEmpty(part.SolvedById) && httpContext is not null)
            {
                model.SolvedByName = part.SolvedById;

                if (await httpContext.RequestServices.GetRequiredService<UserManager<IUser>>().FindByIdAsync(part.SolvedById) is { } solver)
                {
                    model.SolvedByName = httpContext.RequestServices.GetService<IDisplayNameProvider>() is { } names
                        ? await names.GetAsync(solver, cancellationToken) ?? solver.UserName
                        : solver.UserName;
                }
            }
        }).Location(TicketsConstants.DisplayTypes.DetailAdmin, "Sidebar:1.5");
    }
}
