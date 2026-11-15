using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Makes a new ticket wait for its type's approval steps (<see cref="TicketApprovalSettings"/>). A step's approvers
/// are its users and, when the step says so, the requester's manager, from <see cref="ITicketRequesterManagerSource"/>.
/// A step left with no approver is skipped.
/// </summary>
public sealed class TicketApprovalStarter : ITicketObserver
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IServiceProvider _services;
    private readonly IEnumerable<ITicketRequesterManagerSource> _managers;

    internal readonly IStringLocalizer S;

    // The ticket service is resolved when used: it runs the observers, so taking it here would be a cycle.
    public TicketApprovalStarter(IContentDefinitionManager contentDefinitionManager, IServiceProvider services, IEnumerable<ITicketRequesterManagerSource> managers, IStringLocalizer<TicketApprovalStarter> stringLocalizer)
    {
        S = stringLocalizer;
        _contentDefinitionManager = contentDefinitionManager;
        _services = services;
        _managers = managers;
    }

    /// <inheritdoc/>
    public async Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(ticket.ContentType);
        var settings = typeDefinition?.Parts.FirstOrDefault(part => part.PartDefinition.Name == TicketsConstants.PartName)?.GetSettings<TicketApprovalSettings>();

        if (settings?.Steps is not { Count: > 0 } definitions)
        {
            return;
        }

        var requester = ticket.GetOrCreate<TicketPart>().Requester;
        string manager = null;

        if (definitions.Any(definition => definition.RequesterManager) && requester.Kind == PartyKinds.User && !string.IsNullOrEmpty(requester.Id))
        {
            foreach (var source in _managers)
            {
                if ((manager = await source.FindManagerUserIdAsync(requester.Id, cancellationToken)) is not null)
                {
                    break;
                }
            }
        }

        var steps = definitions
            .Select(definition => new TicketApprovalStep
            {
                Name = definition.Name,
                ApproverIds = [.. (definition.ApproverIds ?? []).Concat(definition.RequesterManager && manager is not null ? [manager] : [])],
            })
            .ToList();

        var tickets = _services.GetRequiredService<ITicketService>();

        if (steps.Any(step => step.ApproverIds.Count > 0))
        {
            // Steps without an approver go along, to be skipped and named on the timeline.
            await tickets.StartApprovalAsync(ticket.ContentItemId, steps, settings.WaitingStatusId, TicketActor.Site, cancellationToken);

            return;
        }

        // No step has anyone to decide it: the ticket isn't held, and the agents are told why.
        await tickets.ReplyAsync(ticket.ContentItemId, new TicketReply
        {
            IsInternal = true,
            Body = S["This ticket type asks for approval, but none of its steps has an approver (such as a requester without a manager), so the ticket wasn't held."],
            Actor = TicketActor.Site,
        }, cancellationToken);
    }
}
