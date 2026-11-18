using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.WorkOrders;

/// <summary>
/// The ticket page's <b>Work order</b> panel: an agent who may change the ticket and manage work orders creates a work
/// order of a type from the ticket. The work orders made show in the ticket's links.
/// </summary>
[RequireFeatures(WorkOrdersConstants.Features.WorkOrders)]
internal sealed class TicketWorkOrderDisplayDriver : ContentPartDisplayDriver<TicketPart>
{
    private readonly ITicketAccessService _access;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketWorkOrderDisplayDriver(
        ITicketAccessService access,
        WorkOrderSettingManager<WorkOrderType> types,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _access = access;
        _types = types;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(TicketPart part, BuildPartDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? default;

        if (context.DisplayType != TicketsConstants.DisplayTypes.DetailAdmin
            || user is null
            || !await _access.CanAsync(user, part.ContentItem, TicketOperation.Edit, cancellationToken)
            || !await _authorizationService.AuthorizeAsync(user, WorkOrdersPermissions.ManageWorkOrders))
        {
            return null;
        }

        var types = (await _types.GetAllAsync()).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        if (types.Count == 0)
        {
            return null;
        }

        return Initialize<TicketWorkOrderViewModel>("TicketPart_WorkOrder", model =>
        {
            model.TicketId = part.ContentItem.ContentItemId;
            model.Types = [.. types.Select(type => new SelectListItem(type.Name, type.ItemId))];
            model.Priorities = [.. Enum.GetValues<WorkOrderPriority>().Select(priority => new SelectListItem(priority.ToString(), priority.ToString()))];
        }).Location(TicketsConstants.DisplayTypes.DetailAdmin, "Sidebar:5");
    }
}
