using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// The ticket composer's reply templates from Messaging: the templates of the Tickets scope, rendered with the ticket as
/// <c>{{ Ticket }}</c> (number, subject, status, requester's name) and the requester as <c>{{ Contact.DisplayName }}</c>,
/// with the changes the template makes to a ticket from its properties.
/// </summary>
internal sealed class MessagingTicketReplyTemplateSource : ITicketReplyTemplateSource
{
    private readonly IMessageTemplateManager _templates;
    private readonly IMessageTemplateRenderer _renderer;

    public MessagingTicketReplyTemplateSource(IMessageTemplateManager templates, IMessageTemplateRenderer renderer)
    {
        _templates = templates;
        _renderer = renderer;
    }

    public async Task<IReadOnlyList<TicketReplyTemplate>> ListAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var models = Models(ticket);
        var list = new List<TicketReplyTemplate>();

        foreach (var template in await _templates.ListByScopeAsync(TicketsConstants.ReplyTemplateScope, cancellationToken))
        {
            list.Add(await ToReplyTemplateAsync(template, models, cancellationToken));
        }

        return list;
    }

    public async Task<TicketReplyTemplate> FindAsync(string templateId, ContentItem ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (string.IsNullOrEmpty(templateId) || await _templates.FindByIdAsync(templateId, cancellationToken) is not { } template
            || !string.Equals(template.Scope, TicketsConstants.ReplyTemplateScope, StringComparison.Ordinal))
        {
            return null;
        }

        return await ToReplyTemplateAsync(template, Models(ticket), cancellationToken);
    }

    private async Task<TicketReplyTemplate> ToReplyTemplateAsync(MessageTemplate template, IReadOnlyDictionary<string, object> models, CancellationToken cancellationToken)
        => new()
        {
            Id = template.ItemId,
            Name = template.Name,
            Body = await _renderer.RenderAsync(template, models, cancellationToken),
            StatusId = Read(template, TicketReplyTemplate.PropertyKeys.StatusId),
            PriorityId = Read(template, TicketReplyTemplate.PropertyKeys.PriorityId),
            TeamId = Read(template, TicketReplyTemplate.PropertyKeys.TeamId),
        };

    private static Dictionary<string, object> Models(ContentItem ticket)
    {
        var model = TicketEmailModel.From(ticket, status: null);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Ticket"] = model,
            ["Contact"] = new { DisplayName = model.RequesterName },
        };
    }

    // The properties are read back from storage as JSON values; anything empty is no change.
    internal static string Read(MessageTemplate template, string key)
        => template.Properties is not null && template.Properties.TryGetValue(key, out var value) && value?.ToString() is { Length: > 0 } text ? text : null;
}
