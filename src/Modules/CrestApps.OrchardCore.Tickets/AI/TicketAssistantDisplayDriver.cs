using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// The ticket page's <b>AI assistant</b> panel, on a desk with an assistant: suggest a reply into the composer, for an
/// agent who may answer, and triage, for one who may change the ticket.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
internal sealed class TicketAssistantDisplayDriver : ContentPartDisplayDriver<TicketPart>
{
    private readonly TicketAssistant _assistant;
    private readonly ITicketAccessService _access;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TicketAssistantDisplayDriver(TicketAssistant assistant, ITicketAccessService access, IHttpContextAccessor httpContextAccessor)
    {
        _assistant = assistant;
        _access = access;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(TicketPart part, BuildPartDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? default;

        if (context.DisplayType != TicketsConstants.DisplayTypes.DetailAdmin
            || user is null
            || part.ClosedUtc.HasValue
            || await _assistant.SettingsAsync(part.ContentItem) is null)
        {
            return null;
        }

        var canReply = await _access.CanAsync(user, part.ContentItem, TicketOperation.Reply, cancellationToken);
        var canEdit = await _access.CanAsync(user, part.ContentItem, TicketOperation.Edit, cancellationToken);

        if (!canReply && !canEdit)
        {
            return null;
        }

        return Initialize<TicketAssistantViewModel>("TicketPart_Assistant", model =>
        {
            model.TicketId = part.ContentItem.ContentItemId;
            model.CanSuggest = canReply;
            model.CanTriage = canEdit;
        }).Location(TicketsConstants.DisplayTypes.DetailAdmin, "Sidebar:7");
    }
}
