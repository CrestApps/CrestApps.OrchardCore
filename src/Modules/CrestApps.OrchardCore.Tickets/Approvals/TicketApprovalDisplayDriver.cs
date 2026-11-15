using System.Security.Claims;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Approvals;

/// <summary>
/// The ticket page's <b>Approval</b> panel: each step and its decision, and the approve and refuse buttons for an
/// approver of the step the ticket waits for.
/// </summary>
internal sealed class TicketApprovalDisplayDriver : ContentPartDisplayDriver<TicketPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketApprovalDisplayDriver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(TicketPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != TicketsConstants.DisplayTypes.DetailAdmin || part.Approval is not { } approval)
        {
            return null;
        }

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        return Initialize<TicketApprovalViewModel>("TicketPart_Approval", model =>
        {
            model.TicketId = part.ContentItem.ContentItemId;
            model.Approval = approval;
            model.CanDecide = !string.IsNullOrEmpty(userId) && approval.Current?.ApproverIds.Contains(userId, StringComparer.Ordinal) == true;
        }).Location(TicketsConstants.DisplayTypes.DetailAdmin, "Sidebar:0");
    }
}
