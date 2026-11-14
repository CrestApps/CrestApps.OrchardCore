using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// The default <see cref="ITicketService"/>.
/// </summary>
public sealed class DefaultTicketService : ITicketService
{
    /// <summary>
    /// The number series of tickets.
    /// </summary>
    public const string NumberSeries = "Tickets";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ITimelineService _timeline;
    private readonly IServiceLevelTracker _serviceLevels;
    private readonly IPartyDirectory _parties;
    private readonly INumberSequence _numbers;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<ITicketObserver> _observers;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTicketService"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="statuses">The ticket statuses.</param>
    /// <param name="priorities">The ticket priorities.</param>
    /// <param name="desks">The desks.</param>
    /// <param name="timeline">The timeline service.</param>
    /// <param name="serviceLevels">The service-level tracker.</param>
    /// <param name="parties">The party directory.</param>
    /// <param name="numbers">The number sequence.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="serviceProvider">The services, for the optional team assignment and skills.</param>
    /// <param name="observers">The ticket observers.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultTicketService(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        ICatalog<TicketStatus> statuses,
        ICatalog<TicketPriority> priorities,
        ICatalog<TicketDesk> desks,
        ITimelineService timeline,
        IServiceLevelTracker serviceLevels,
        IPartyDirectory parties,
        INumberSequence numbers,
        IDistributedLock distributedLock,
        IClock clock,
        IServiceProvider serviceProvider,
        IEnumerable<ITicketObserver> observers,
        ILogger<DefaultTicketService> logger,
        IStringLocalizer<DefaultTicketService> stringLocalizer)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _statuses = statuses;
        _priorities = priorities;
        _desks = desks;
        _timeline = timeline;
        _serviceLevels = serviceLevels;
        _parties = parties;
        _numbers = numbers;
        _distributedLock = distributedLock;
        _clock = clock;
        _serviceProvider = serviceProvider;
        _observers = observers;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the record a ticket is to its timeline, files and service-level timers.
    /// </summary>
    /// <param name="ticketId">The ticket's content item id.</param>
    /// <returns>The record.</returns>
    public static RecordRef SubjectOf(string ticketId)
        => new(TicketsConstants.RecordKind, ticketId);

    /// <inheritdoc/>
    public async Task<TicketResult> CreateAsync(TicketCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? TicketsConstants.DefaultContentType : request.ContentType.Trim();
        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        var partDefinition = typeDefinition?.Parts.FirstOrDefault(part => part.PartDefinition.Name == TicketsConstants.PartName);

        if (partDefinition is null)
        {
            return Refuse(S["{0} is not a ticket type.", contentType]);
        }

        var settings = partDefinition.GetSettings<TicketPartSettings>();
        var desks = await _desks.GetAllAsync(cancellationToken);
        var deskId = FirstNonEmpty(request.DeskId, settings.DefaultDeskId);
        var desk = string.IsNullOrEmpty(deskId)
            ? desks.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
            : desks.FirstOrDefault(item => item.ItemId == deskId);

        if (desk is null)
        {
            errors.Add(S["Choose a desk for the ticket."]);
        }
        else if (desk.TicketTypes.Count > 0 && !desk.TicketTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(S["The {0} desk does not take this kind of ticket.", desk.Name]);
        }

        var subject = request.Subject?.Trim();

        if (string.IsNullOrEmpty(subject))
        {
            errors.Add(S["Write a subject."]);
        }

        var status = TicketLifecycle.FirstOf(await _statuses.GetAllAsync(cancellationToken), TicketStatusCategory.New);

        if (status is null)
        {
            errors.Add(S["Add a ticket status in the New category first."]);
        }

        var priority = await ResolvePriorityAsync(FirstNonEmpty(request.PriorityId, settings.DefaultPriorityId), cancellationToken);

        if (priority is null)
        {
            errors.Add(S["Add a ticket priority first."]);
        }

        if (errors.Count > 0)
        {
            return new TicketResult { Errors = errors };
        }

        var now = _clock.UtcNow;
        var ticket = await _contentManager.NewAsync(contentType);
        var part = ticket.GetOrCreate<TicketPart>();

        part.Number = $"T-{await _numbers.NextAsync(NumberSeries, cancellationToken: cancellationToken)}";
        part.DeskId = desk.ItemId;
        part.StatusId = status.ItemId;
        part.StatusCategory = status.Category;
        part.StatusChangedUtc = now;
        part.PriorityId = priority.ItemId;
        part.PriorityRank = priority.Rank;
        part.Requester = request.Requester;
        part.RequesterContact = request.RequesterContact is { HasAnyValue: true } contact
            ? contact
            : request.Requester.IsEmpty ? null : await _parties.GetContactInfoAsync(request.Requester, cancellationToken);
        part.AccountId = request.AccountId;
        part.TeamId = FirstNonEmpty(request.TeamId, settings.DefaultTeamId, desk.DefaultTeamId);
        part.AssigneeId = request.AssigneeId;
        part.Channel = request.Channel;
        part.FollowUpOfId = request.FollowUpOfId;
        part.LastCustomerActivityUtc = now;

        ticket.DisplayText = subject;
        SetTitle(ticket, subject);

        if (!request.CreatedBy.IsEmpty && request.CreatedBy.Kind == PartyKinds.User)
        {
            ticket.Owner = request.CreatedBy.Id;
        }
        else if (!request.Requester.IsEmpty && request.Requester.Kind == PartyKinds.User)
        {
            ticket.Owner = request.Requester.Id;
        }

        ticket.Apply(part);

        await _contentManager.CreateAsync(ticket, VersionOptions.Published);

        if (!string.IsNullOrWhiteSpace(request.Body) || request.AttachmentIds.Count > 0)
        {
            await _timeline.AddAsync(new TimelineEntry
            {
                Subject = SubjectOf(ticket.ContentItemId),
                Kind = TicketsConstants.EntryKinds.Reply,
                Visibility = TimelineVisibility.Public,
                Author = request.Requester.IsEmpty ? request.CreatedBy : request.Requester,
                AuthorName = request.RequesterContact?.Name,
                Body = request.Body?.Trim(),
                AttachmentIds = [.. request.AttachmentIds],
                Channel = request.Channel,
                CreatedUtc = now,
            }, cancellationToken);

            part.ReplyCount = 1;
        }

        if (!string.IsNullOrEmpty(request.FollowUpOfId))
        {
            await AddEventAsync(ticket.ContentItemId, TicketActor.Site, S["Follow-up of a closed ticket."], [], cancellationToken);
        }

        var policyId = FirstNonEmpty(settings.ServiceLevelPolicyId, desk.ServiceLevelPolicyId);

        if (!string.IsNullOrEmpty(policyId))
        {
            foreach (var metric in new[] { ServiceLevelMetrics.FirstResponse, ServiceLevelMetrics.Resolution })
            {
                await _serviceLevels.StartAsync(new ServiceLevelStart
                {
                    Subject = SubjectOf(ticket.ContentItemId),
                    Metric = metric,
                    PolicyId = policyId,
                    Priority = priority.Name,
                    StartUtc = now,
                    Paused = status.PausesTimers,
                }, cancellationToken);
            }

            part.NextDueUtc = await GetNextDueAsync(ticket.ContentItemId, cancellationToken);
        }

        ticket.Apply(part);
        await _contentManager.UpdateAsync(ticket);

        if (string.IsNullOrEmpty(part.AssigneeId) && !string.IsNullOrEmpty(part.TeamId))
        {
            await AssignToTeamMemberAsync(ticket, part, settings, TicketActor.Site, cancellationToken);
        }

        await NotifyAsync(observer => observer.CreatedAsync(ticket, cancellationToken));

        return new TicketResult { Ticket = ticket };
    }

    /// <inheritdoc/>
    public async Task<ContentItem> GetAsync(string ticketId)
    {
        if (string.IsNullOrEmpty(ticketId))
        {
            return null;
        }

        var ticket = await _contentManager.GetAsync(ticketId, VersionOptions.Latest);

        return ticket?.Has<TicketPart>() == true ? ticket : null;
    }

    /// <inheritdoc/>
    public async Task<ContentItem> FindByNumberAsync(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var trimmed = number.Trim();
        var index = await _session.QueryIndex<TicketIndex>(item => item.Number == trimmed).FirstOrDefaultAsync();

        return index is null ? null : await GetAsync(index.ContentItemId);
    }

    /// <inheritdoc/>
    public Task<TicketResult> UpdateAsync(string ticketId, TicketUpdate update, TicketActor actor = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return WithLockAsync(ticketId, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();

            if (TicketLifecycle.IsReadOnly(part))
            {
                return Refuse(S["A closed or merged ticket can't change."], ticket);
            }

            var changes = new List<TimelineFieldChange>();
            var errors = await ApplyAsync(ticket, part, update, changes, cancellationToken);

            if (errors.Count > 0)
            {
                return new TicketResult { Ticket = ticket, Errors = errors };
            }

            if (changes.Count == 0)
            {
                return new TicketResult { Ticket = ticket };
            }

            await SaveAsync(ticket, part, cancellationToken);
            await AddEventAsync(ticket.ContentItemId, actor ?? TicketActor.Site, null, changes, cancellationToken);
            await NotifyAsync(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TicketResult> ReplyAsync(string ticketId, TicketReply reply, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reply);

        if (string.IsNullOrWhiteSpace(reply.Body) && reply.AttachmentIds.Count == 0)
        {
            return Refuse(S["Write something, or attach a file."]);
        }

        ContentItem followUpOf = null;
        var result = await WithLockAsync(ticketId, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();
            var now = _clock.UtcNow;

            if (!string.IsNullOrEmpty(part.MergedIntoId))
            {
                return Refuse(S["This ticket was merged into another; reply there."], ticket);
            }

            var desk = await _desks.FindByIdAsync(part.DeskId, cancellationToken);

            if (!reply.IsInternal && reply.FromRequester && TicketLifecycle.StartsFollowUp(part, desk, now))
            {
                followUpOf = ticket;

                return new TicketResult { Ticket = ticket };
            }

            if (part.StatusCategory == TicketStatusCategory.Closed)
            {
                return Refuse(S["A closed ticket can't change."], ticket);
            }

            var entry = new TimelineEntry
            {
                Subject = SubjectOf(ticket.ContentItemId),
                Kind = reply.IsInternal ? TicketsConstants.EntryKinds.Note : TicketsConstants.EntryKinds.Reply,
                Visibility = reply.IsInternal ? TimelineVisibility.Internal : TimelineVisibility.Public,
                Author = reply.Author,
                AuthorName = reply.AuthorName,
                Body = reply.Body?.Trim(),
                AttachmentIds = [.. reply.AttachmentIds],
                Channel = reply.Channel,
                CreatedUtc = now,
            };

            foreach (var (key, value) in reply.Properties ?? [])
            {
                entry.Properties[key] = value;
            }

            var added = await _timeline.AddAsync(entry, cancellationToken);

            if (!added.Succeeded)
            {
                return new TicketResult { Ticket = ticket, Errors = added.Errors };
            }

            var changes = new List<TimelineFieldChange>();
            var subject = SubjectOf(ticket.ContentItemId);
            string statusId = reply.SetStatusId;

            if (!reply.IsInternal)
            {
                part.ReplyCount++;

                if (reply.FromRequester)
                {
                    part.LastCustomerActivityUtc = now;

                    if (part.FirstResponseUtc is not null)
                    {
                        await StartNextResponseAsync(ticket, part, desk, now, cancellationToken);
                    }

                    if (statusId is null && part.StatusCategory is TicketStatusCategory.Solved or TicketStatusCategory.Pending)
                    {
                        statusId = TicketLifecycle.FirstOf(await _statuses.GetAllAsync(cancellationToken), TicketStatusCategory.Open)?.ItemId;
                    }
                }
                else
                {
                    part.LastAgentActivityUtc = now;

                    if (part.FirstResponseUtc is null)
                    {
                        part.FirstResponseUtc = now;
                        await _serviceLevels.CompleteAsync(subject, ServiceLevelMetrics.FirstResponse, now, cancellationToken);
                    }

                    await _serviceLevels.CompleteAsync(subject, ServiceLevelMetrics.NextResponse, now, cancellationToken);

                    if (statusId is null && part.StatusCategory == TicketStatusCategory.New)
                    {
                        statusId = TicketLifecycle.FirstOf(await _statuses.GetAllAsync(cancellationToken), TicketStatusCategory.Open)?.ItemId;
                    }
                }
            }

            if (!string.IsNullOrEmpty(statusId) && statusId != part.StatusId)
            {
                var errors = await ApplyAsync(ticket, part, new TicketUpdate { StatusId = statusId }, changes, cancellationToken);

                if (errors.Count > 0)
                {
                    return new TicketResult { Ticket = ticket, Errors = errors };
                }
            }

            await SaveAsync(ticket, part, cancellationToken);

            if (changes.Count > 0)
            {
                await AddEventAsync(ticket.ContentItemId, ActorOf(reply), null, changes, cancellationToken);
            }

            await NotifyAsync(observer => observer.RepliedAsync(ticket, added.Entry, reply.FromRequester, cancellationToken));

            if (changes.Count > 0)
            {
                await NotifyAsync(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));
            }

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);

        if (followUpOf is null || !result.Succeeded)
        {
            return result;
        }

        // A reply to a closed ticket, or to a solved one past its reopen window, opens a follow-up linked to it.
        var closed = followUpOf.GetOrCreate<TicketPart>();
        var followUp = await CreateAsync(new TicketCreateRequest
        {
            ContentType = followUpOf.ContentType,
            Subject = followUpOf.DisplayText,
            Body = reply.Body,
            AttachmentIds = reply.AttachmentIds,
            DeskId = closed.DeskId,
            PriorityId = closed.PriorityId,
            Requester = closed.Requester,
            RequesterContact = closed.RequesterContact,
            AccountId = closed.AccountId,
            TeamId = closed.TeamId,
            Channel = reply.Channel ?? closed.Channel,
            FollowUpOfId = followUpOf.ContentItemId,
        }, cancellationToken);

        if (followUp.Succeeded)
        {
            await LinkAsync(followUp.Ticket.ContentItemId, SubjectOf(followUpOf.ContentItemId), TicketLinkKinds.FollowUpOf, TicketActor.Site, cancellationToken);
        }

        return followUp;
    }

    /// <inheritdoc/>
    public Task<TicketResult> AutoAssignAsync(string ticketId, TicketActor actor = null, CancellationToken cancellationToken = default)
        => WithLockAsync(ticketId, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();

            if (TicketLifecycle.IsReadOnly(part))
            {
                return Refuse(S["A closed or merged ticket can't change."], ticket);
            }

            if (string.IsNullOrEmpty(part.TeamId))
            {
                return Refuse(S["Give the ticket a team first."], ticket);
            }

            var settings = await GetSettingsAsync(ticket.ContentType);

            return await AssignToTeamMemberAsync(ticket, part, settings, actor ?? TicketActor.Site, cancellationToken)
                ? new TicketResult { Ticket = ticket }
                : Refuse(S["Nobody in the team can take the ticket now."], ticket);
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<TicketResult> MergeAsync(string ticketId, string intoTicketId, TicketActor actor = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ticketId) || string.IsNullOrEmpty(intoTicketId) || ticketId == intoTicketId)
        {
            return Refuse(S["Choose another ticket to merge into."]);
        }

        actor ??= TicketActor.Site;

        var into = await GetAsync(intoTicketId);

        if (into is null || TicketLifecycle.IsReadOnly(into.GetOrCreate<TicketPart>()))
        {
            return Refuse(S["The ticket to merge into is closed or gone."]);
        }

        ContentItem merged = null;
        var result = await WithLockAsync(ticketId, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();

            if (TicketLifecycle.IsReadOnly(part))
            {
                return Refuse(S["A closed or merged ticket can't change."], ticket);
            }

            var closed = TicketLifecycle.FirstOf(await _statuses.GetAllAsync(cancellationToken), TicketStatusCategory.Closed);

            if (closed is null)
            {
                return Refuse(S["Add a ticket status in the Closed category first."], ticket);
            }

            var now = _clock.UtcNow;

            part.MergedIntoId = intoTicketId;
            part.StatusId = closed.ItemId;
            part.StatusCategory = closed.Category;
            part.StatusChangedUtc = now;
            part.ClosedUtc = now;
            part.NextDueUtc = null;

            await _serviceLevels.CancelAsync(SubjectOf(ticket.ContentItemId), cancellationToken: cancellationToken);
            await SaveAsync(ticket, part, cancellationToken);
            await AddEventAsync(ticket.ContentItemId, actor, S["Merged into {0}.", into.GetOrCreate<TicketPart>().Number], [], cancellationToken);
            merged = ticket;

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return result;
        }

        return await WithLockAsync(intoTicketId, async target =>
        {
            var targetPart = target.GetOrCreate<TicketPart>();
            var mergedPart = merged.GetOrCreate<TicketPart>();

            foreach (var party in new[] { mergedPart.Requester }.Concat(mergedPart.Participants))
            {
                if (!party.IsEmpty && party != targetPart.Requester && !targetPart.Participants.Contains(party))
                {
                    targetPart.Participants.Add(party);
                }
            }

            await SaveAsync(target, targetPart, cancellationToken);
            await AddEventAsync(target.ContentItemId, actor, S["{0} was merged into this ticket.", mergedPart.Number], [], cancellationToken);
            await NotifyAsync(observer => observer.MergedAsync(merged, target, cancellationToken));

            return new TicketResult { Ticket = target };
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TicketResult> LinkAsync(string ticketId, RecordRef record, string kind, TicketActor actor = null, CancellationToken cancellationToken = default)
    {
        if (record.IsEmpty || (record.Kind == TicketsConstants.RecordKind && record.Id == ticketId))
        {
            return Task.FromResult(Refuse(S["Choose a record to link."]));
        }

        return WithLockAsync(ticketId, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();
            var existing = part.Links.FirstOrDefault(link => link.Record == record);
            var changes = new List<TimelineFieldChange>();

            if (string.IsNullOrEmpty(kind))
            {
                if (existing is null)
                {
                    return new TicketResult { Ticket = ticket };
                }

                part.Links.Remove(existing);
                changes.Add(new TimelineFieldChange { Field = S["Link"], From = record.ToString() });
            }
            else if (existing is null)
            {
                part.Links.Add(new TicketLink { Record = record, Kind = kind });
                changes.Add(new TimelineFieldChange { Field = S["Link"], To = $"{kind}: {record}" });
            }
            else if (existing.Kind != kind)
            {
                changes.Add(new TimelineFieldChange { Field = S["Link"], From = $"{existing.Kind}: {record}", To = $"{kind}: {record}" });
                existing.Kind = kind;
            }
            else
            {
                return new TicketResult { Ticket = ticket };
            }

            await SaveAsync(ticket, part, cancellationToken);
            await AddEventAsync(ticket.ContentItemId, actor ?? TicketActor.Site, null, changes, cancellationToken);
            await NotifyAsync(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<ContentItem> Tickets, int Total)> ListAsync(TicketScopedQuery query, CancellationToken cancellationToken = default)
    {
        if (query?.Query is null)
        {
            return ([], 0);
        }

        var filter = query.Query;
        var none = new[] { "\u0000" };
        var all = query.AllDeskIds.Count > 0 ? query.AllDeskIds.ToArray() : none;
        var team = query.TeamDeskIds.Count > 0 ? query.TeamDeskIds.ToArray() : none;
        var assigned = query.AssignedDeskIds.Count > 0 ? query.AssignedDeskIds.ToArray() : none;
        var teams = query.TeamIds.Count > 0 ? query.TeamIds.ToArray() : none;
        var userId = string.IsNullOrEmpty(query.UserId) ? "\u0000" : query.UserId;

        var tickets = _session.Query<ContentItem, TicketIndex>(index =>
            index.DeskId.IsIn(all)
            || (index.DeskId.IsIn(team) && (index.TeamId.IsIn(teams) || index.AssigneeId == userId))
            || (index.DeskId.IsIn(assigned) && index.AssigneeId == userId));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            tickets = tickets.Where(index => index.Number == search || index.Subject.Contains(search));
        }

        if (filter.DeskIds.Count > 0)
        {
            var desks = filter.DeskIds.ToArray();
            tickets = tickets.Where(index => index.DeskId.IsIn(desks));
        }

        if (filter.Categories.Count > 0)
        {
            var categories = filter.Categories.ToArray();
            tickets = tickets.Where(index => index.StatusCategory.IsIn(categories));
        }

        if (filter.StatusIds.Count > 0)
        {
            var statuses = filter.StatusIds.ToArray();
            tickets = tickets.Where(index => index.StatusId.IsIn(statuses));
        }

        if (filter.PriorityIds.Count > 0)
        {
            var priorities = filter.PriorityIds.ToArray();
            tickets = tickets.Where(index => index.PriorityId.IsIn(priorities));
        }

        if (filter.TeamIds.Count > 0)
        {
            var teamIds = filter.TeamIds.ToArray();
            tickets = tickets.Where(index => index.TeamId.IsIn(teamIds));
        }

        if (filter.ContentTypes.Count > 0)
        {
            var types = filter.ContentTypes.ToArray();
            tickets = tickets.Where(index => index.ContentType.IsIn(types));
        }

        if (string.Equals(filter.Assignee, TicketAssignees.Unassigned, StringComparison.OrdinalIgnoreCase))
        {
            tickets = tickets.Where(index => index.AssigneeId == null);
        }
        else if (!string.IsNullOrEmpty(filter.Assignee))
        {
            var assignee = string.Equals(filter.Assignee, TicketAssignees.Me, StringComparison.OrdinalIgnoreCase) ? userId : filter.Assignee;
            tickets = tickets.Where(index => index.AssigneeId == assignee);
        }

        if (!string.IsNullOrEmpty(filter.Requester))
        {
            var requester = filter.Requester;
            tickets = tickets.Where(index => index.Requester == requester);
        }

        if (!string.IsNullOrEmpty(filter.AccountId))
        {
            var accountId = filter.AccountId;
            tickets = tickets.Where(index => index.AccountId == accountId);
        }

        if (filter.DueWithin is { } within)
        {
            var dueBy = _clock.UtcNow.Add(within);
            tickets = tickets.Where(index => index.NextDueUtc != null && index.NextDueUtc <= dueBy);
        }

        var total = await tickets.CountAsync(cancellationToken);

        tickets = filter.Sort switch
        {
            TicketSort.Created => tickets.OrderByDescending(index => index.CreatedUtc),
            TicketSort.Due => tickets.OrderBy(index => index.NextDueUtc == null).ThenBy(index => index.NextDueUtc),
            TicketSort.Priority => tickets.OrderByDescending(index => index.PriorityRank).ThenByDescending(index => index.ModifiedUtc),
            _ => tickets.OrderByDescending(index => index.ModifiedUtc),
        };

        var page = await tickets
            .Skip(Math.Max(filter.Skip, 0))
            .Take(Math.Clamp(filter.Take, 1, 500))
            .ListAsync(cancellationToken);

        return ([.. page], total);
    }

    private async Task<List<string>> ApplyAsync(ContentItem ticket, TicketPart part, TicketUpdate update, List<TimelineFieldChange> changes, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var now = _clock.UtcNow;
        var subject = SubjectOf(ticket.ContentItemId);

        if (update.Subject is { } title && !string.IsNullOrWhiteSpace(title) && title.Trim() != ticket.DisplayText)
        {
            changes.Add(new TimelineFieldChange { Field = S["Subject"], From = ticket.DisplayText, To = title.Trim() });
            ticket.DisplayText = title.Trim();
            SetTitle(ticket, ticket.DisplayText);
        }

        if (!string.IsNullOrEmpty(update.DeskId) && update.DeskId != part.DeskId)
        {
            var desk = await _desks.FindByIdAsync(update.DeskId, cancellationToken);

            if (desk is null)
            {
                errors.Add(S["The desk no longer exists."]);
            }
            else
            {
                var from = await _desks.FindByIdAsync(part.DeskId, cancellationToken);
                changes.Add(new TimelineFieldChange { Field = S["Desk"], From = from?.Name, To = desk.Name });
                part.DeskId = desk.ItemId;
            }
        }

        if (!string.IsNullOrEmpty(update.PriorityId) && update.PriorityId != part.PriorityId)
        {
            var priority = await _priorities.FindByIdAsync(update.PriorityId, cancellationToken);

            if (priority is null)
            {
                errors.Add(S["The priority no longer exists."]);
            }
            else
            {
                var from = await _priorities.FindByIdAsync(part.PriorityId, cancellationToken);
                changes.Add(new TimelineFieldChange { Field = S["Priority"], From = from?.Name, To = priority.Name });
                part.PriorityId = priority.ItemId;
                part.PriorityRank = priority.Rank;

                foreach (var timer in await _serviceLevels.GetTimersAsync(subject, activeOnly: true, cancellationToken))
                {
                    await _serviceLevels.RetargetAsync(subject, timer.PolicyId, priority.Name, cancellationToken);

                    break;
                }
            }
        }

        if (update.TeamId is not null && NullIfEmpty(update.TeamId) != part.TeamId)
        {
            changes.Add(new TimelineFieldChange { Field = S["Team"], From = await TeamNameAsync(part.TeamId, cancellationToken), To = await TeamNameAsync(update.TeamId, cancellationToken) });
            part.TeamId = NullIfEmpty(update.TeamId);
        }

        if (update.AssigneeId is not null && NullIfEmpty(update.AssigneeId) != part.AssigneeId)
        {
            changes.Add(new TimelineFieldChange { Field = S["Assignee"], From = await UserNameAsync(part.AssigneeId, cancellationToken), To = await UserNameAsync(update.AssigneeId, cancellationToken) });

            if (!string.IsNullOrEmpty(part.AssigneeId))
            {
                part.ReassignmentCount++;
            }

            part.AssigneeId = NullIfEmpty(update.AssigneeId);
        }

        if (update.EscalationLevel is { } level && level != part.EscalationLevel && level >= 0)
        {
            changes.Add(new TimelineFieldChange { Field = S["Escalation level"], From = part.EscalationLevel.ToString(System.Globalization.CultureInfo.InvariantCulture), To = level.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            part.EscalationLevel = level;
        }

        if (!string.IsNullOrEmpty(update.StatusId) && update.StatusId != part.StatusId)
        {
            var status = await _statuses.FindByIdAsync(update.StatusId, cancellationToken);

            if (status is null)
            {
                errors.Add(S["The status no longer exists."]);
            }
            else if (!TicketLifecycle.Transitions.CanMove(part.StatusCategory, status.Category))
            {
                errors.Add(S["A ticket can't move from {0} to {1}.", part.StatusCategory, status.Category]);
            }
            else
            {
                var from = await _statuses.FindByIdAsync(part.StatusId, cancellationToken);
                changes.Add(new TimelineFieldChange { Field = S["Status"], From = from?.Name, To = status.Name });
                await MoveAsync(ticket, part, from, status, now, cancellationToken);
            }
        }

        return errors;
    }

    private async Task MoveAsync(ContentItem ticket, TicketPart part, TicketStatus from, TicketStatus to, DateTime now, CancellationToken cancellationToken)
    {
        var subject = SubjectOf(ticket.ContentItemId);
        var wasSolved = part.StatusCategory == TicketStatusCategory.Solved;

        part.StatusId = to.ItemId;
        part.StatusCategory = to.Category;
        part.StatusChangedUtc = now;

        switch (to.Category)
        {
            case TicketStatusCategory.Solved:
                part.SolvedUtc = now;
                await _serviceLevels.CompleteAsync(subject, ServiceLevelMetrics.Resolution, now, cancellationToken);
                await _serviceLevels.CancelAsync(subject, ServiceLevelMetrics.NextResponse, cancellationToken);
                break;
            case TicketStatusCategory.Closed:
                part.ClosedUtc = now;
                await _serviceLevels.CancelAsync(subject, cancellationToken: cancellationToken);
                break;
            default:
                if (wasSolved)
                {
                    part.ReopenCount++;
                    await RestartResolutionAsync(ticket, part, now, cancellationToken);
                }

                break;
        }

        if (to.Category is not TicketStatusCategory.Solved and not TicketStatusCategory.Closed)
        {
            if (to.PausesTimers && from?.PausesTimers != true)
            {
                await _serviceLevels.PauseAsync(subject, cancellationToken: cancellationToken);
            }
            else if (!to.PausesTimers && from?.PausesTimers == true)
            {
                await _serviceLevels.ResumeAsync(subject, cancellationToken: cancellationToken);
            }
        }
    }

    // A reopened ticket's resolution counts on from the time it had used; a ticket with no resolution to reopen (it was
    // cancelled, or never had one) starts a new cycle.
    private async Task RestartResolutionAsync(ContentItem ticket, TicketPart part, DateTime now, CancellationToken cancellationToken)
    {
        if (await _serviceLevels.ReopenAsync(SubjectOf(ticket.ContentItemId), ServiceLevelMetrics.Resolution, cancellationToken) is not null)
        {
            return;
        }

        var timers = await _serviceLevels.GetTimersAsync(SubjectOf(ticket.ContentItemId), cancellationToken: cancellationToken);
        var policyId = timers.Select(timer => timer.PolicyId).FirstOrDefault(id => !string.IsNullOrEmpty(id));

        if (string.IsNullOrEmpty(policyId))
        {
            return;
        }

        var priority = await _priorities.FindByIdAsync(part.PriorityId, cancellationToken);

        await _serviceLevels.StartAsync(new ServiceLevelStart
        {
            Subject = SubjectOf(ticket.ContentItemId),
            Metric = ServiceLevelMetrics.Resolution,
            PolicyId = policyId,
            Priority = priority?.Name,
            StartUtc = now,
        }, cancellationToken);
    }

    private async Task StartNextResponseAsync(ContentItem ticket, TicketPart part, TicketDesk desk, DateTime now, CancellationToken cancellationToken)
    {
        var subject = SubjectOf(ticket.ContentItemId);
        var timers = await _serviceLevels.GetTimersAsync(subject, cancellationToken: cancellationToken);

        if (timers.Any(timer => timer.Metric == ServiceLevelMetrics.NextResponse && timer.IsActive))
        {
            return;
        }

        var policyId = timers.Select(timer => timer.PolicyId).FirstOrDefault(id => !string.IsNullOrEmpty(id)) ?? desk?.ServiceLevelPolicyId;

        if (string.IsNullOrEmpty(policyId))
        {
            return;
        }

        var priority = await _priorities.FindByIdAsync(part.PriorityId, cancellationToken);

        await _serviceLevels.StartAsync(new ServiceLevelStart
        {
            Subject = subject,
            Metric = ServiceLevelMetrics.NextResponse,
            PolicyId = policyId,
            Priority = priority?.Name,
            StartUtc = now,
        }, cancellationToken);
    }

    private async Task<bool> AssignToTeamMemberAsync(ContentItem ticket, TicketPart part, TicketPartSettings settings, TicketActor actor, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<ITeamAssignmentService>() is not { } assignment)
        {
            return false;
        }

        var skills = _serviceProvider.GetService<ISkillAssignmentService>();
        var required = settings?.RequiredSkills ?? [];
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var member = await assignment.SelectAsync(new TeamAssignmentRequest
        {
            TeamId = part.TeamId,
            WorkKind = TicketsConstants.RecordKind,
            ExcludedUserIds = string.IsNullOrEmpty(part.AssigneeId) ? [] : [part.AssigneeId],
            CandidateFilter = skills is null || required.Count == 0
                ? null
                : async (candidates, token) =>
                {
                    var qualified = new List<TeamMember>();

                    foreach (var candidate in candidates)
                    {
                        if (!string.IsNullOrEmpty(candidate.UserId)
                            && await skills.HoldsAllAsync(new Skills.Models.SkillSubject(Skills.Models.SkillSubjectKind.User, candidate.UserId), required, today, token))
                        {
                            qualified.Add(candidate);
                        }
                    }

                    return qualified;
                },
        }, cancellationToken);

        if (string.IsNullOrEmpty(member?.UserId))
        {
            return false;
        }

        var changes = new List<TimelineFieldChange>();

        await ApplyAsync(ticket, part, new TicketUpdate { AssigneeId = member.UserId }, changes, cancellationToken);
        await SaveAsync(ticket, part, cancellationToken);
        await AddEventAsync(ticket.ContentItemId, actor, null, changes, cancellationToken);
        await NotifyAsync(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));

        return true;
    }

    private async Task<TicketResult> WithLockAsync(string ticketId, Func<ContentItem, Task<TicketResult>> work, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(ticketId))
        {
            return Refuse(S["The ticket no longer exists."]);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"ticket:{ticketId}", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return Refuse(S["Someone else is changing the ticket; try again."]);
        }

        await using (locker)
        {
            var ticket = await GetAsync(ticketId);

            if (ticket is null)
            {
                return Refuse(S["The ticket no longer exists."]);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return await work(ticket);
        }
    }

    private async Task SaveAsync(ContentItem ticket, TicketPart part, CancellationToken cancellationToken)
    {
        part.NextDueUtc = await GetNextDueAsync(ticket.ContentItemId, cancellationToken);
        ticket.Apply(part);
        await _contentManager.UpdateAsync(ticket);
    }

    private async Task<DateTime?> GetNextDueAsync(string ticketId, CancellationToken cancellationToken)
    {
        var timers = await _serviceLevels.GetTimersAsync(SubjectOf(ticketId), activeOnly: true, cancellationToken);

        return timers
            .Where(timer => timer.State == ServiceLevelTimerState.Running)
            .Select(timer => (DateTime?)timer.DueUtc)
            .DefaultIfEmpty(null)
            .Min();
    }

    private async Task AddEventAsync(string ticketId, TicketActor actor, string body, List<TimelineFieldChange> changes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(body) && changes.Count == 0)
        {
            return;
        }

        await _timeline.AddAsync(new TimelineEntry
        {
            Subject = SubjectOf(ticketId),
            Kind = TicketsConstants.EntryKinds.Event,
            Visibility = TimelineVisibility.Internal,
            Author = actor?.Party ?? default,
            AuthorName = actor?.Name,
            Body = body,
            Changes = changes,
        }, cancellationToken);
    }

    private async Task NotifyAsync(Func<ITicketObserver, Task> notify)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "A ticket observer ({Observer}) failed.", observer.GetType().Name.SanitizeLogValue());
            }
        }
    }

    private async Task<TicketPriority> ResolvePriorityAsync(string priorityId, CancellationToken cancellationToken)
    {
        var priorities = (await _priorities.GetAllAsync(cancellationToken)).ToList();

        return priorities.FirstOrDefault(item => item.ItemId == priorityId)
            ?? priorities.FirstOrDefault(item => string.Equals(item.Name, "Normal", StringComparison.OrdinalIgnoreCase))
            ?? priorities.OrderBy(item => item.Rank).FirstOrDefault();
    }

    private async Task<TicketPartSettings> GetSettingsAsync(string contentType)
    {
        var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        return typeDefinition?.Parts.FirstOrDefault(part => part.PartDefinition.Name == TicketsConstants.PartName)?.GetSettings<TicketPartSettings>() ?? new TicketPartSettings();
    }

    private async Task<string> TeamNameAsync(string teamId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(teamId))
        {
            return null;
        }

        return _serviceProvider.GetService<ITeamManager>() is { } teams && await teams.FindByIdAsync(teamId, cancellationToken) is { } team
            ? team.Name
            : teamId;
    }

    private async Task<string> UserNameAsync(string userId, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(userId)
            ? null
            : await _parties.GetNameAsync(new PartyRef(PartyKinds.User, userId), cancellationToken) ?? userId;

    private static void SetTitle(ContentItem ticket, string subject)
    {
        if (ticket.Content.TitlePart is { } titlePart)
        {
            titlePart.Title = subject;
        }
    }

    private static TicketActor ActorOf(TicketReply reply)
        => reply.Author.IsEmpty ? new TicketActor { Name = reply.AuthorName } : new TicketActor { Party = reply.Author, Name = reply.AuthorName };

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static TicketResult Refuse(string error, ContentItem ticket = null)
        => new() { Ticket = ticket, Errors = [error] };
}
