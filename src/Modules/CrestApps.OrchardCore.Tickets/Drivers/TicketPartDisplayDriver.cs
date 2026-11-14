using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The panels of the ticket page (the <c>DetailAdmin</c> display of a ticket), in its <c>Sidebar</c> zone: the
/// ticket's values, the requester with their other tickets, the service levels and the links. Other modules add their
/// own panels with drivers of their own. The ticket's values change through the ticket service, never an editor of
/// this part, so the part has no editor.
/// </summary>
internal sealed class TicketPartDisplayDriver : ContentPartDisplayDriver<TicketPart>
{
    private const string Zone = "Sidebar";
    private const int PreviousTicketsShown = 5;

    private readonly ITicketService _tickets;
    private readonly ITicketAccessService _access;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IServiceLevelTracker _serviceLevels;
    private readonly IPartyDirectory _parties;
    private readonly IRecordDirectory _records;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public TicketPartDisplayDriver(
        ITicketService tickets,
        ITicketAccessService access,
        ICatalog<TicketStatus> statuses,
        ICatalog<TicketPriority> priorities,
        ICatalog<TicketDesk> desks,
        IServiceLevelTracker serviceLevels,
        IPartyDirectory parties,
        IRecordDirectory records,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IServiceProvider serviceProvider,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<TicketPartDisplayDriver> stringLocalizer)
    {
        _tickets = tickets;
        _access = access;
        _statuses = statuses;
        _priorities = priorities;
        _desks = desks;
        _serviceLevels = serviceLevels;
        _parties = parties;
        _records = records;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _serviceProvider = serviceProvider;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(TicketPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != TicketsConstants.DisplayTypes.DetailAdmin)
        {
            return null;
        }

        var ticket = part.ContentItem;

        return Combine(
            Initialize<TicketPropertiesViewModel>("TicketPart_Properties", model => BuildPropertiesAsync(model, ticket, part))
                .Location(TicketsConstants.DisplayTypes.DetailAdmin, Zone + ":1"),
            Initialize<TicketRequesterViewModel>("TicketPart_Requester", model => BuildRequesterAsync(model, ticket, part))
                .Location(TicketsConstants.DisplayTypes.DetailAdmin, Zone + ":2"),
            Initialize<TicketServiceLevelsViewModel>("TicketPart_ServiceLevels", async model =>
            {
                var timers = await _serviceLevels.GetTimersAsync(new RecordRef(TicketsConstants.RecordKind, ticket.ContentItemId), activeOnly: false, CancellationToken);
                model.Timers = [.. timers.OrderByDescending(timer => timer.StartedUtc)];
                model.NowUtc = _clock.UtcNow;
            }).Location(TicketsConstants.DisplayTypes.DetailAdmin, Zone + ":3"),
            Initialize<TicketLinksViewModel>("TicketPart_Links", model => BuildLinksAsync(model, ticket, part))
                .Location(TicketsConstants.DisplayTypes.DetailAdmin, Zone + ":4"));
    }

    private ClaimsPrincipal User
        => _httpContextAccessor.HttpContext?.User;

    private CancellationToken CancellationToken
        => _httpContextAccessor.HttpContext?.RequestAborted ?? default;

    private async ValueTask BuildPropertiesAsync(TicketPropertiesViewModel model, ContentItem ticket, TicketPart part)
    {
        var scopes = await _access.GetViewScopesAsync(User, CancellationToken);

        model.Ticket = ticket;
        model.Part = part;
        model.AssigneeName = string.IsNullOrEmpty(part.AssigneeId) ? null : await _parties.GetNameAsync(new PartyRef(PartyKinds.User, part.AssigneeId), CancellationToken) ?? part.AssigneeId;
        model.TeamName = string.IsNullOrEmpty(part.TeamId) || _serviceProvider.GetService<ITeamManager>() is not { } teams
            ? null
            : (await teams.FindByIdAsync(part.TeamId, CancellationToken))?.Name;
        model.IsReadOnly = TicketLifecycle.IsReadOnly(part);
        model.CanEdit = !model.IsReadOnly && await _access.CanAsync(User, ticket, TicketOperation.Edit, CancellationToken);
        model.CanAssign = !model.IsReadOnly && await _access.CanAsync(User, ticket, TicketOperation.Assign, CancellationToken);
        model.Statuses = [.. (await _statuses.GetAllAsync(CancellationToken))
            .OrderBy(status => status.Order)
            .Select(status => new SelectListItem(status.Name, status.ItemId, status.ItemId == part.StatusId))];
        model.Priorities = [.. (await _priorities.GetAllAsync(CancellationToken))
            .OrderByDescending(priority => priority.Rank)
            .Select(priority => new SelectListItem(priority.Name, priority.ItemId, priority.ItemId == part.PriorityId))];

        // A ticket moves only to a desk the user may see; its own desk stays listed.
        model.Desks = [.. (await _desks.GetAllAsync(CancellationToken))
            .Where(desk => desk.ItemId == part.DeskId || scopes.ContainsKey(desk.ItemId))
            .OrderBy(desk => desk.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(desk => new SelectListItem(desk.Name, desk.ItemId, desk.ItemId == part.DeskId))];
    }

    private async ValueTask BuildRequesterAsync(TicketRequesterViewModel model, ContentItem ticket, TicketPart part)
    {
        var participants = part.Participants.Where(participant => !participant.IsEmpty).ToList();
        var names = await _parties.GetNamesAsync(part.Requester.IsEmpty ? participants : [part.Requester, .. participants], CancellationToken);

        model.Part = part;
        model.RequesterName = !part.Requester.IsEmpty && names.TryGetValue(part.Requester, out var name) && !string.IsNullOrEmpty(name)
            ? name
            : part.RequesterContact?.Name ?? part.RequesterContact?.Email;
        model.Participants = [.. participants.Select(participant => names.GetValueOrDefault(participant, participant.Id))];

        if (part.Requester.IsEmpty)
        {
            return;
        }

        var scoped = await _access.ApplyScopeAsync(User, new TicketQuery
        {
            Requester = part.Requester.ToString(),
            Sort = TicketSort.Created,
            Take = PreviousTicketsShown + 1,
        }, CancellationToken);

        if (scoped is null)
        {
            return;
        }

        var (others, total) = await _tickets.ListAsync(scoped, CancellationToken);
        var statuses = (await _statuses.GetAllAsync(CancellationToken)).ToDictionary(status => status.ItemId, StringComparer.Ordinal);

        model.PreviousTickets = [.. others
            .Where(other => other.ContentItemId != ticket.ContentItemId)
            .Take(PreviousTicketsShown)
            .Select(other => new TicketRow
            {
                Ticket = other,
                Part = other.GetOrCreate<TicketPart>(),
                Status = statuses.GetValueOrDefault(other.GetOrCreate<TicketPart>().StatusId ?? string.Empty),
            })];
        model.PreviousTotal = Math.Max(0, total - 1);
    }

    private async ValueTask BuildLinksAsync(TicketLinksViewModel model, ContentItem ticket, TicketPart part)
    {
        var summaries = await _records.GetSummariesAsync(part.Links.Select(link => link.Record), CancellationToken);

        model.Ticket = ticket;
        model.CanEdit = !TicketLifecycle.IsReadOnly(part) && await _access.CanAsync(User, ticket, TicketOperation.Edit, CancellationToken);
        model.Links = [.. part.Links.Select(link => new TicketLinkView
        {
            Link = link,
            Name = summaries.TryGetValue(link.Record, out var summary) ? summary.Title : link.Record.Id,
            Url = summary?.AdminUrl,
            KindName = _records.GetSource(link.Record.Kind)?.DisplayName is { } displayName ? _localizerFactory.Localize(displayName).Value : link.Record.Kind,
        })];

        foreach (var source in await _records.GetSearchableSourcesAsync(User, CancellationToken))
        {
            model.Kinds.Add(new SelectListItem(_localizerFactory.Localize(source.DisplayName).Value, source.Kind));
        }
    }
}
