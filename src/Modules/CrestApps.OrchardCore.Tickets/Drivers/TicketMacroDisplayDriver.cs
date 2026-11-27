using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// Adds to a reply template's editor what using it on a ticket also changes (a macro): the status, the priority and the
/// team. Kept in the template's properties; the ticket composer applies them when the template is used.
/// </summary>
internal sealed class TicketMacroDisplayDriver : DisplayDriver<MessageTemplate>
{
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ICatalog<TicketPriority> _priorities;

    internal readonly IStringLocalizer S;

    public TicketMacroDisplayDriver(
        ICatalog<TicketStatus> statuses,
        ICatalog<TicketPriority> priorities,
        IStringLocalizer<TicketMacroDisplayDriver> stringLocalizer)
    {
        _statuses = statuses;
        _priorities = priorities;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(MessageTemplate template, BuildEditorContext context)
        => Initialize<TicketMacroViewModel>("TicketMacro_Edit", async model =>
        {
            model.StatusId = MessagingTicketReplyTemplateSource.Read(template, TicketReplyTemplate.PropertyKeys.StatusId);
            model.PriorityId = MessagingTicketReplyTemplateSource.Read(template, TicketReplyTemplate.PropertyKeys.PriorityId);
            model.TeamId = MessagingTicketReplyTemplateSource.Read(template, TicketReplyTemplate.PropertyKeys.TeamId);
            model.Statuses = [.. (await _statuses.GetAllAsync()).OrderBy(status => status.Order).Select(status => new SelectListItem(status.Name, status.ItemId, status.ItemId == model.StatusId))];
            model.Priorities = [.. (await _priorities.GetAllAsync()).OrderByDescending(priority => priority.Rank).Select(priority => new SelectListItem(priority.Name, priority.ItemId, priority.ItemId == model.PriorityId))];
        }).Location(l => l.Zone("Content", "5").Card(S["On a Ticket, Also"], "50"));

    public override async Task<IDisplayResult> UpdateAsync(MessageTemplate template, UpdateEditorContext context)
    {
        var model = new TicketMacroViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        Set(template, TicketReplyTemplate.PropertyKeys.StatusId, model.StatusId);
        Set(template, TicketReplyTemplate.PropertyKeys.PriorityId, model.PriorityId);
        Set(template, TicketReplyTemplate.PropertyKeys.TeamId, model.TeamId);

        return Edit(template, context);
    }

    private static void Set(MessageTemplate template, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            template.Properties?.Remove(key);

            return;
        }

        template.Properties[key] = value.Trim();
    }
}
