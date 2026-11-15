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
using OrchardCore.Locking;
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

    // What each ticket changed under this service's lock was when it was loaded, so a save can tell observers that it
    // was assigned or its status changed, whichever path made the change.
    private readonly Dictionary<string, (string AssigneeId, string StatusId)> _snapshots = new(StringComparer.Ordinal);

    // What observers are told, in order, once the change is committed and the ticket's lock released: a workflow or
    // any other observer then reads the saved ticket and can change it under the lock itself.
    private readonly List<Func<ITicketObserver, Task>> _pending = [];

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
        var contentType = request.Ticket?.ContentType
            ?? (string.IsNullOrWhiteSpace(request.ContentType) ? TicketsConstants.DefaultContentType : request.ContentType.Trim());
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

        var subject = FirstNonEmpty(request.Subject, request.Ticket?.DisplayText)?.Trim();

        if (string.IsNullOrEmpty(subject))
        {
            errors.Add(S["Write a subject."]);
        }

        if (request.Body?.Trim().Length > TimelineConstants.MaxBodyLength)
        {
            errors.Add(S["The message is longer than {0} characters.", TimelineConstants.MaxBodyLength]);
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
        var ticket = request.Ticket ?? await _contentManager.NewAsync(contentType);
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
        // An address only typed in (a guest's) never files the ticket under an account: anyone could type a customer's.
        part.RequesterUnverified = request.RequesterUnverified;
        part.AccountId = !string.IsNullOrEmpty(request.AccountId) ? request.AccountId : request.RequesterUnverified ? null : await AccountByEmailAsync(part, cancellationToken);
        part.TeamId = FirstNonEmpty(request.TeamId, settings.DefaultTeamId, desk.DefaultTeamId);
        part.AssigneeId = string.IsNullOrEmpty(request.AssigneeId) ? null : request.AssigneeId;
        part.AssignedUtc = part.AssigneeId is null ? null : now;
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
            var added = await _timeline.AddAsync(new TimelineEntry
            {
                ItemId = string.IsNullOrEmpty(request.EntryId) ? null : request.EntryId,
                Subject = SubjectOf(ticket.ContentItemId),
                Kind = TicketsConstants.EntryKinds.Reply,
                Visibility = TimelineVisibility.Public,
                // The first message is the requester's: a known party, a guest kept by name, or the agent who wrote it
                // for nobody in particular.
                Author = !request.Requester.IsEmpty ? request.Requester : request.RequesterContact is { HasAnyValue: true } ? default : request.CreatedBy,
                AuthorName = request.RequesterContact?.Name ?? request.RequesterContact?.Email,
                Body = request.Body?.Trim(),
                AttachmentIds = [.. request.AttachmentIds],
                Channel = request.Channel,
                CreatedUtc = now,
            }, cancellationToken);

            // The ticket isn't kept without its first message; nothing of it reaches the database.
            if (!added.Succeeded)
            {
                await _contentManager.RemoveAsync(ticket);
                _pending.Clear();

                return new TicketResult { Errors = added.Errors };
            }

            part.ReplyCount = 1;
        }

        if (!string.IsNullOrEmpty(request.FollowUpOfId))
        {
            await AddEventAsync(ticket.ContentItemId, TicketActor.Site, S["Follow-up of a closed ticket."], [], cancellationToken);
        }

        // The account's entitlement first, then the ticket type's, then the desk's.
        var policyId = FirstNonEmpty(await AccountPolicyAsync(part.AccountId), settings.ServiceLevelPolicyId, desk.ServiceLevelPolicyId);

        if (!string.IsNullOrEmpty(policyId))
        {
            foreach (var metric in new[] { ServiceLevelMetrics.FirstResponse, ServiceLevelMetrics.Resolution })
            {
                await _serviceLevels.StartAsync(new ServiceLevelStart
                {
                    Subject = SubjectOf(ticket.ContentItemId),
                    Metric = metric,
                    PolicyId = policyId,
                    Priority = priority.ItemId,
                    StartUtc = now,
                    Paused = status.PausesTimers,
                }, cancellationToken);
            }

            part.NextDueUtc = await GetNextDueAsync(ticket.ContentItemId, cancellationToken);
        }

        ticket.Apply(part);
        await _contentManager.UpdateAsync(ticket);

        // The ticket starts in its first status, unassigned as far as observers are concerned.
        _snapshots[ticket.ContentItemId] = (null, part.StatusId);

        Enqueue(observer => observer.CreatedAsync(ticket, cancellationToken));

        if (!string.IsNullOrEmpty(part.AssigneeId))
        {
            await RaiseChangesAsync(ticket, part, cancellationToken);
        }

        // A desk without a strategy leaves new tickets for an agent to take.
        if (string.IsNullOrEmpty(part.AssigneeId) && !string.IsNullOrEmpty(part.TeamId) && !string.IsNullOrEmpty(desk.AssignmentStrategy))
        {
            await AssignToTeamMemberAsync(ticket, part, settings, desk, desk.AssignmentStrategy, TicketActor.Site, cancellationToken);
        }

        // An import tells nobody: 5,000 past tickets must not e-mail 5,000 requesters.
        if (request.IsImport)
        {
            _pending.Clear();
        }

        // Committed before anyone is told, so a Ticket Created workflow reads the saved ticket and its changes aren't
        // overwritten by this method.
        await _session.SaveChangesAsync(cancellationToken);
        await RaisePendingAsync(request.CreatedBy.IsEmpty ? TicketActor.Site : new TicketActor { Party = request.CreatedBy });

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

        return WithLockAsync(ticketId, actor, async ticket =>
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
            await AddEventAsync(ticket.ContentItemId, actor ?? TicketActor.Site, NullIfEmpty(update.Reason?.Trim()), changes, cancellationToken);
            Enqueue(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));

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
        var result = await WithLockAsync(ticketId, ActorOf(reply), async ticket =>
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

            if (reply.Body?.Trim().Length > TimelineConstants.MaxBodyLength)
            {
                return Refuse(S["The message is longer than {0} characters.", TimelineConstants.MaxBodyLength], ticket);
            }

            // Everything that can refuse the reply is checked before anything is written, so a refused reply leaves no
            // entry, no completed timer and no changed counter behind.
            var automated = reply.Actor?.FromWorkflow == true;
            var agentReply = !reply.IsInternal && !reply.FromRequester && !automated;
            var statuses = await _statuses.GetAllAsync(cancellationToken);
            var statusId = reply.SetStatusId;
            var awaitingApproval = part.Approval?.State == TicketApprovalState.Pending;

            // A ticket that waits for approval keeps its status: only the approval moves it.
            if (awaitingApproval && !string.IsNullOrEmpty(statusId) && statusId != part.StatusId)
            {
                return Refuse(S["The ticket waits for approval, so its status can't change until it is decided or canceled."], ticket);
            }

            if (statusId is null && !reply.IsInternal && !awaitingApproval)
            {
                if (reply.FromRequester && part.StatusCategory is TicketStatusCategory.Solved or TicketStatusCategory.Pending)
                {
                    statusId = TicketLifecycle.FirstOf(statuses, TicketStatusCategory.Open)?.ItemId;
                }
                else if (agentReply && part.StatusCategory == TicketStatusCategory.New)
                {
                    statusId = TicketLifecycle.FirstOf(statuses, TicketStatusCategory.Open)?.ItemId;
                }
            }

            if (!string.IsNullOrEmpty(statusId) && statusId != part.StatusId)
            {
                var target = statuses.FirstOrDefault(status => status.ItemId == statusId);

                if (target is null)
                {
                    return Refuse(S["The status no longer exists."], ticket);
                }

                if (!TicketLifecycle.Transitions.CanMove(part.StatusCategory, target.Category))
                {
                    return Refuse(S["A ticket can't move from {0} to {1}.", part.StatusCategory, target.Category], ticket);
                }
            }

            var entry = new TimelineEntry
            {
                ItemId = string.IsNullOrEmpty(reply.EntryId) ? null : reply.EntryId,
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
                }
                else if (agentReply)
                {
                    // Only a person's answer counts as a response; an automated acknowledgement doesn't.
                    part.LastAgentActivityUtc = now;
                    part.AgentReplyCount++;

                    if (part.FirstResponseUtc is null)
                    {
                        part.FirstResponseUtc = now;
                        await _serviceLevels.CompleteAsync(subject, ServiceLevelMetrics.FirstResponse, now, cancellationToken);
                    }

                    await _serviceLevels.CompleteAsync(subject, ServiceLevelMetrics.NextResponse, now, cancellationToken);
                }
            }

            if (!string.IsNullOrEmpty(statusId) && statusId != part.StatusId)
            {
                // Checked above, so it can't be refused now.
                await ApplyAsync(ticket, part, new TicketUpdate { StatusId = statusId }, changes, cancellationToken);
            }

            await SaveAsync(ticket, part, cancellationToken);

            if (changes.Count > 0)
            {
                await AddEventAsync(ticket.ContentItemId, ActorOf(reply), null, changes, cancellationToken);
            }

            Enqueue(observer => observer.RepliedAsync(ticket, added.Entry, reply.FromRequester, cancellationToken));

            if (changes.Count > 0)
            {
                Enqueue(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));
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
            EntryId = reply.EntryId,
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
        => WithLockAsync(ticketId, actor, async ticket =>
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
            var desk = await _desks.FindByIdAsync(part.DeskId, cancellationToken);
            var strategy = string.IsNullOrEmpty(desk?.AssignmentStrategy) ? TeamAssignmentStrategies.LeastLoaded : desk.AssignmentStrategy;

            return await AssignToTeamMemberAsync(ticket, part, settings, desk, strategy, actor ?? TicketActor.Site, cancellationToken)
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

        // Both tickets are locked, always in the same order so two merges of the same pair can't wait on each other, and
        // checked again inside the locks: A into B and B into A can't both succeed, and a lock that can't be had leaves
        // both tickets as they were.
        return await WithLocksAsync([ticketId, intoTicketId], actor, async tickets =>
        {
            var ticket = tickets[ticketId];
            var target = tickets[intoTicketId];
            var part = ticket.GetOrCreate<TicketPart>();
            var targetPart = target.GetOrCreate<TicketPart>();

            if (TicketLifecycle.IsReadOnly(part))
            {
                return Refuse(S["A closed or merged ticket can't change."], ticket);
            }

            if (TicketLifecycle.IsReadOnly(targetPart))
            {
                return Refuse(S["The ticket to merge into is closed or gone."], ticket);
            }

            var closed = TicketLifecycle.FirstOf(await _statuses.GetAllAsync(cancellationToken), TicketStatusCategory.Closed);

            if (closed is null)
            {
                return Refuse(S["Add a ticket status in the Closed category first."], ticket);
            }

            var now = _clock.UtcNow;

            part.MergedIntoId = intoTicketId;

            // Links sent for the merged ticket stop working; the conversation continues on the other one.
            part.AccessVersion++;
            part.StatusId = closed.ItemId;
            part.StatusCategory = closed.Category;
            part.StatusChangedUtc = now;
            part.ClosedUtc = now;
            part.NextDueUtc = null;

            foreach (var party in new[] { part.Requester }.Concat(part.Participants))
            {
                if (!party.IsEmpty && party != targetPart.Requester && !targetPart.Participants.Contains(party))
                {
                    targetPart.Participants.Add(party);
                }
            }

            await _serviceLevels.CancelAsync(SubjectOf(ticket.ContentItemId), cancellationToken: cancellationToken);
            await SaveAsync(ticket, part, cancellationToken);
            await SaveAsync(target, targetPart, cancellationToken);
            await AddEventAsync(ticket.ContentItemId, actor, S["Merged into {0}.", targetPart.Number], [], cancellationToken);
            await AddEventAsync(target.ContentItemId, actor, S["{0} was merged into this ticket.", part.Number], [], cancellationToken);
            Enqueue(observer => observer.MergedAsync(ticket, target, cancellationToken));

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

        return WithLockAsync(ticketId, actor, async ticket =>
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
                changes.Add(new TimelineFieldChange { Field = S["Link"], Key = TicketChangeKeys.Link, From = record.ToString(), FromValue = record.ToString() });
            }
            else if (existing is null)
            {
                part.Links.Add(new TicketLink { Record = record, Kind = kind });
                changes.Add(new TimelineFieldChange { Field = S["Link"], Key = TicketChangeKeys.Link, To = $"{kind}: {record}", ToValue = record.ToString() });
            }
            else if (existing.Kind != kind)
            {
                changes.Add(new TimelineFieldChange { Field = S["Link"], Key = TicketChangeKeys.Link, From = $"{existing.Kind}: {record}", To = $"{kind}: {record}", FromValue = record.ToString(), ToValue = record.ToString() });
                existing.Kind = kind;
            }
            else
            {
                return new TicketResult { Ticket = ticket };
            }

            await SaveAsync(ticket, part, cancellationToken);
            await AddEventAsync(ticket.ContentItemId, actor ?? TicketActor.Site, null, changes, cancellationToken);
            Enqueue(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));

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
        var userId = string.IsNullOrEmpty(query.UserId) ? "\u0000" : query.UserId;

        var tickets = _session.Query<ContentItem, TicketIndex>(TicketScopeFilter.ToPredicate(query));

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

        if (filter.SolvedBeforeUtc is { } solvedBefore)
        {
            tickets = tickets.Where(index => index.SolvedUtc != null && index.SolvedUtc < solvedBefore);
        }

        if (filter.DueWithin is { } within)
        {
            var dueBy = _clock.UtcNow.Add(within);
            tickets = tickets.Where(index => index.NextDueUtc != null && index.NextDueUtc <= dueBy);
        }

        var total = await tickets.CountAsync(cancellationToken);

        // Every sort ends on the index row's id, so rows with equal values keep one order and a page never repeats or skips
        // a ticket. Due time sorts on a column that is never null (tickets with nothing due last), as SQL Server can't order
        // by a test for null.
        tickets = filter.Sort switch
        {
            TicketSort.Created => tickets.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id),
            TicketSort.Due => tickets.OrderBy(index => index.DueSortUtc).ThenBy(index => index.Id),
            TicketSort.Priority => tickets.OrderByDescending(index => index.PriorityRank).ThenByDescending(index => index.ModifiedUtc).ThenByDescending(index => index.Id),
            _ => tickets.OrderByDescending(index => index.ModifiedUtc).ThenByDescending(index => index.Id),
        };

        var page = await tickets
            .Skip(Math.Max(filter.Skip, 0))
            .Take(Math.Clamp(filter.Take, 1, 500))
            .ListAsync(cancellationToken);

        return ([.. page], total);
    }

    private Task<List<string>> ApplyAsync(ContentItem ticket, TicketPart part, TicketUpdate update, List<TimelineFieldChange> changes, CancellationToken cancellationToken)
        => ApplyAsync(ticket, part, update, changes, byApproval: false, cancellationToken);

    // A ticket that waits for approval changes status only through the approval (byApproval), whoever asks: the panel,
    // a bulk action, the API or a workflow.
    private async Task<List<string>> ApplyAsync(ContentItem ticket, TicketPart part, TicketUpdate update, List<TimelineFieldChange> changes, bool byApproval, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (!byApproval && part.Approval?.State == TicketApprovalState.Pending && !string.IsNullOrEmpty(update.StatusId) && update.StatusId != part.StatusId)
        {
            errors.Add(S["The ticket waits for approval, so its status can't change until it is decided or canceled."]);

            return errors;
        }
        var now = _clock.UtcNow;
        var subject = SubjectOf(ticket.ContentItemId);

        if (update.Subject is { } title && !string.IsNullOrWhiteSpace(title) && title.Trim() != ticket.DisplayText)
        {
            changes.Add(new TimelineFieldChange { Field = S["Subject"], Key = TicketChangeKeys.Subject, From = ticket.DisplayText, To = title.Trim() });
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
            else if (desk.TicketTypes.Count > 0 && !desk.TicketTypes.Contains(ticket.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(S["The {0} desk does not take this kind of ticket.", desk.Name]);
            }
            else
            {
                var from = await _desks.FindByIdAsync(part.DeskId, cancellationToken);

                // The ticket's targets follow the new desk's policy, unless its type names one of its own.
                var settings = await GetSettingsAsync(ticket.ContentType);

                if (string.IsNullOrEmpty(settings.ServiceLevelPolicyId) && !string.IsNullOrEmpty(desk.ServiceLevelPolicyId) && desk.ServiceLevelPolicyId != from?.ServiceLevelPolicyId)
                {
                    var current = await _priorities.FindByIdAsync(part.PriorityId, cancellationToken);
                    await _serviceLevels.RetargetAsync(subject, desk.ServiceLevelPolicyId, current?.ItemId, cancellationToken);
                }

                changes.Add(new TimelineFieldChange { Field = S["Desk"], Key = TicketChangeKeys.Desk, From = from?.Name, To = desk.Name, FromValue = part.DeskId, ToValue = desk.ItemId });
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
                changes.Add(new TimelineFieldChange { Field = S["Priority"], Key = TicketChangeKeys.Priority, From = from?.Name, To = priority.Name, FromValue = part.PriorityId, ToValue = priority.ItemId });
                part.PriorityId = priority.ItemId;
                part.PriorityRank = priority.Rank;

                foreach (var timer in await _serviceLevels.GetTimersAsync(subject, activeOnly: true, cancellationToken))
                {
                    await _serviceLevels.RetargetAsync(subject, timer.PolicyId, priority.ItemId, cancellationToken);

                    break;
                }
            }
        }

        if (update.TeamId is not null && NullIfEmpty(update.TeamId) is { } newTeam && newTeam != part.TeamId
            && _serviceProvider.GetService<ITeamManager>() is { } teamManager && await teamManager.FindByIdAsync(newTeam, cancellationToken) is null)
        {
            errors.Add(S["The team no longer exists."]);
        }
        else if (update.TeamId is not null && NullIfEmpty(update.TeamId) != part.TeamId)
        {
            changes.Add(new TimelineFieldChange { Field = S["Team"], Key = TicketChangeKeys.Team, From = await TeamNameAsync(part.TeamId, cancellationToken), To = await TeamNameAsync(update.TeamId, cancellationToken), FromValue = part.TeamId, ToValue = NullIfEmpty(update.TeamId) });
            part.TeamId = NullIfEmpty(update.TeamId);
        }

        if (update.AssigneeId is not null && NullIfEmpty(update.AssigneeId) is { } newAssignee && newAssignee != part.AssigneeId
            && _serviceProvider.GetService<ITicketAccessService>() is { } access
            && !await access.CanBeAssignedAsync(newAssignee, update.DeskId ?? part.DeskId, cancellationToken))
        {
            errors.Add(S["That person can't see tickets on this desk, so they can't be given this ticket."]);
        }
        else if (update.AssigneeId is not null && NullIfEmpty(update.AssigneeId) != part.AssigneeId)
        {
            changes.Add(new TimelineFieldChange { Field = S["Assignee"], Key = TicketChangeKeys.Assignee, From = await UserNameAsync(part.AssigneeId, cancellationToken), To = await UserNameAsync(update.AssigneeId, cancellationToken), FromValue = part.AssigneeId, ToValue = NullIfEmpty(update.AssigneeId) });

            if (!string.IsNullOrEmpty(part.AssigneeId))
            {
                part.ReassignmentCount++;
            }

            part.AssigneeId = NullIfEmpty(update.AssigneeId);
            part.AssignedUtc = part.AssigneeId is null ? null : _clock.UtcNow;
        }

        if ((update.RaiseEscalation ? part.EscalationLevel + 1 : update.EscalationLevel) is { } level && level != part.EscalationLevel && level >= 0)
        {
            changes.Add(new TimelineFieldChange { Field = S["Escalation level"], Key = TicketChangeKeys.EscalationLevel, From = part.EscalationLevel.ToString(System.Globalization.CultureInfo.InvariantCulture), To = level.ToString(System.Globalization.CultureInfo.InvariantCulture), FromValue = part.EscalationLevel.ToString(System.Globalization.CultureInfo.InvariantCulture), ToValue = level.ToString(System.Globalization.CultureInfo.InvariantCulture) });
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
                changes.Add(new TimelineFieldChange { Field = S["Status"], Key = TicketChangeKeys.Status, From = from?.Name, To = status.Name, FromValue = part.StatusId, ToValue = status.ItemId });
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

                // A ticket solved without an answer has no response left to wait for: its response targets end here, so
                // the monitor doesn't breach a solved ticket.
                await _serviceLevels.CancelAsync(subject, ServiceLevelMetrics.FirstResponse, cancellationToken);
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

        // The timers follow the status the ticket is in now, not the step it came from: a ticket that waited (paused),
        // was solved and is reopened counts again. Pausing and resuming leave timers already in that state alone.
        if (to.Category is not TicketStatusCategory.Solved and not TicketStatusCategory.Closed)
        {
            if (to.PausesTimers)
            {
                await _serviceLevels.PauseAsync(subject, cancellationToken: cancellationToken);
            }
            else
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
            Priority = priority?.ItemId,
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
            Priority = priority?.ItemId,
            StartUtc = now,
        }, cancellationToken);
    }

    private async Task<bool> AssignToTeamMemberAsync(ContentItem ticket, TicketPart part, TicketPartSettings settings, TicketDesk desk, string strategy, TicketActor actor, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<ITeamAssignmentService>() is not { } assignment)
        {
            return false;
        }

        var skills = _serviceProvider.GetService<ISkillAssignmentService>();
        var required = settings?.RequiredSkills ?? [];
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var picked = await assignment.PickAsync(new TeamAssignmentRequest
        {
            TeamId = part.TeamId,
            WorkKind = TicketsConstants.RecordKind,
            Strategy = strategy,
            AvailabilitySources = HonoredSources(desk),
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

        var member = picked.Member;

        if (string.IsNullOrEmpty(member?.UserId))
        {
            // Left unassigned on purpose, and the ticket says why, so nobody wonders whether assignment ran.
            string reason = picked.Outcome switch
            {
                TeamAssignmentOutcome.NoMembers => S["Not assigned: the team has no members who can sign in."],
                TeamAssignmentOutcome.NoneQualified => S["Not assigned: no available team member holds the skills this ticket type needs."],
                TeamAssignmentOutcome.AllAtCapacity => S["Not assigned: every available team member has as many open tickets as allowed."],
                TeamAssignmentOutcome.NoTeam => null,
                _ => S["Not assigned: no one on the team is available now (off shift, out of office or signed out)."],
            };

            if (reason is not null)
            {
                await AddEventAsync(ticket.ContentItemId, actor, reason, [], cancellationToken);
            }

            return false;
        }

        var changes = new List<TimelineFieldChange>();

        if ((await ApplyAsync(ticket, part, new TicketUpdate { AssigneeId = member.UserId }, changes, cancellationToken)).Count > 0)
        {
            return false;
        }

        await SaveAsync(ticket, part, cancellationToken);
        await AddEventAsync(ticket.ContentItemId, actor, null, changes, cancellationToken);
        Enqueue(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));

        return true;
    }

    /// <inheritdoc/>
    public Task<TicketResult> RateAsync(string ticketId, int score, string comment, CancellationToken cancellationToken = default)
        => WithLockAsync(ticketId, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();

            if (part.StatusCategory is not (TicketStatusCategory.Solved or TicketStatusCategory.Closed))
            {
                return Refuse(S["A request can be rated once it is solved."], ticket);
            }

            if (score is < TicketSatisfaction.MinScore or > TicketSatisfaction.MaxScore)
            {
                return Refuse(S["Choose a rating from {0} to {1}.", TicketSatisfaction.MinScore, TicketSatisfaction.MaxScore], ticket);
            }

            comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

            if (comment?.Length > TicketSatisfaction.MaxCommentLength)
            {
                comment = comment[..TicketSatisfaction.MaxCommentLength];
            }

            part.Satisfaction = new TicketSatisfaction { Score = score, Comment = comment, RatedUtc = _clock.UtcNow };
            await SaveAsync(ticket, part, cancellationToken);

            var actor = new TicketActor { Party = part.Requester, Name = part.RequesterContact?.Name };
            var body = S["Rated {0} out of {1}.", score, TicketSatisfaction.MaxScore].Value + (comment is null ? null : "\n\n" + comment);
            await AddEventAsync(ticket.ContentItemId, actor, body, [], cancellationToken);

            var satisfaction = part.Satisfaction;
            Enqueue(observer => observer.RatedAsync(ticket, satisfaction, cancellationToken));

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<TicketResult> RecordRatingRequestAsync(string ticketId, CancellationToken cancellationToken = default)
        => WithLockAsync(ticketId, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();
            part.RatingRequestedUtc = _clock.UtcNow;
            await SaveAsync(ticket, part, cancellationToken);

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<TicketResult> StartApprovalAsync(string ticketId, IReadOnlyList<TicketApprovalStep> steps, string waitingStatusId = null, TicketActor actor = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        return WithLockAsync(ticketId, actor, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();

            if (TicketLifecycle.IsReadOnly(part))
            {
                return Refuse(S["A closed or merged ticket can't change."], ticket);
            }

            if (part.Approval?.State == TicketApprovalState.Pending)
            {
                return Refuse(S["The ticket is already waiting for approval."], ticket);
            }

            // A step left with no approver (a requester without a manager, say) is skipped, and the timeline says so.
            var skipped = steps.Where(step => step is not null && step.ApproverIds?.Any(id => !string.IsNullOrWhiteSpace(id)) != true).Select(step => step.Name).ToList();
            var kept = steps
                .Where(step => step?.ApproverIds?.Any(id => !string.IsNullOrWhiteSpace(id)) == true)
                .Select(step => new TicketApprovalStep
                {
                    Name = NullIfEmpty(step.Name?.Trim()),
                    ApproverIds = [.. step.ApproverIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)],
                })
                .ToList();

            if (kept.Count == 0)
            {
                return Refuse(S["An approval needs a step with an approver."], ticket);
            }

            part.Approval = new TicketApproval { State = TicketApprovalState.Pending, Steps = kept, HeldStatusId = part.StatusId };

            // Waiting holds the ticket in the type's waiting status, or the first waiting one, which pauses its timers
            // when that status does.
            var changes = new List<TimelineFieldChange>();
            var statuses = await _statuses.GetAllAsync(cancellationToken);
            var waiting = statuses.FirstOrDefault(status => status.ItemId == waitingStatusId && status.Category is not (TicketStatusCategory.Solved or TicketStatusCategory.Closed))
                ?? TicketLifecycle.FirstOf(statuses, TicketStatusCategory.Pending);

            if (waiting is not null && waiting.ItemId != part.StatusId)
            {
                var errors = await ApplyAsync(ticket, part, new TicketUpdate { StatusId = waiting.ItemId }, changes, byApproval: true, cancellationToken);

                if (errors.Count > 0)
                {
                    return new TicketResult { Ticket = ticket, Errors = errors };
                }
            }

            await SaveAsync(ticket, part, cancellationToken);

            var first = kept[0];
            var waitingFor = S["Waiting for approval: {0}.", first.Name ?? S["step {0}", 1].Value].Value;

            if (skipped.Count > 0)
            {
                waitingFor += " " + S["Skipped for want of an approver: {0}.", string.Join(", ", skipped.Select(name => name ?? "?"))].Value;
            }

            await AddEventAsync(ticket.ContentItemId, actor ?? TicketActor.Site, waitingFor, changes, cancellationToken);

            if (changes.Count > 0)
            {
                Enqueue(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));
            }

            Enqueue(observer => observer.ApprovalRequestedAsync(ticket, first, cancellationToken));

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TicketResult> DecideApprovalAsync(string ticketId, string approverId, string approverName, bool approve, string comment, CancellationToken cancellationToken = default)
    {
        var actor = TicketActor.User(approverId, approverName);

        return WithLockAsync(ticketId, actor, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();

            if (part.Approval?.Current is not { } step)
            {
                return Refuse(S["The ticket isn't waiting for approval."], ticket);
            }

            if (string.IsNullOrEmpty(approverId) || !step.ApproverIds.Contains(approverId, StringComparer.Ordinal))
            {
                return Refuse(S["You aren't an approver of this step."], ticket);
            }

            comment = NullIfEmpty(comment?.Trim());

            if (!approve && comment is null)
            {
                return Refuse(S["Say why the request isn't approved; the requester reads it."], ticket);
            }

            var now = _clock.UtcNow;
            var approval = part.Approval;
            var stepName = step.Name ?? S["step {0}", approval.CurrentStep + 1].Value;

            step.Decision = approve ? TicketApprovalDecision.Approved : TicketApprovalDecision.Rejected;
            step.DecidedById = approverId;
            step.DecidedByName = approverName;
            step.DecidedUtc = now;
            step.Comment = comment;

            var changes = new List<TimelineFieldChange>();
            var statuses = await _statuses.GetAllAsync(cancellationToken);
            TicketApprovalStep next = null;
            string body;
            string targetStatusId = null;

            if (!approve)
            {
                approval.State = TicketApprovalState.Rejected;
                body = S["Not approved ({0}): {1}", stepName, comment];
                targetStatusId = TicketLifecycle.FirstOf(statuses, TicketStatusCategory.Solved)?.ItemId;
            }
            else if (approval.CurrentStep + 1 < approval.Steps.Count)
            {
                approval.CurrentStep++;
                next = approval.Steps[approval.CurrentStep];
                body = S["Approved ({0}). Waiting for approval: {1}.", stepName, next.Name ?? S["step {0}", approval.CurrentStep + 1].Value];
            }
            else
            {
                approval.State = TicketApprovalState.Approved;
                body = S["Approved ({0}). The request can go ahead.", stepName];

                // Back to the status it had, when the ticket may go there from waiting; otherwise to work.
                var held = statuses.FirstOrDefault(status => status.ItemId == approval.HeldStatusId);
                targetStatusId = held is not null && TicketLifecycle.Transitions.CanMove(part.StatusCategory, held.Category)
                    ? held.ItemId
                    : TicketLifecycle.FirstOf(statuses, TicketStatusCategory.Open)?.ItemId;
            }

            if (comment is not null && approve)
            {
                body += "\n\n" + comment;
            }

            if (!string.IsNullOrEmpty(targetStatusId) && targetStatusId != part.StatusId)
            {
                var errors = await ApplyAsync(ticket, part, new TicketUpdate { StatusId = targetStatusId }, changes, byApproval: true, cancellationToken);

                if (errors.Count > 0)
                {
                    return new TicketResult { Ticket = ticket, Errors = errors };
                }
            }

            await SaveAsync(ticket, part, cancellationToken);
            await AddEventAsync(ticket.ContentItemId, actor, body, changes, cancellationToken);

            if (!approve)
            {
                // The requester reads why, as a reply, and is e-mailed it like any reply.
                var added = await _timeline.AddAsync(new TimelineEntry
                {
                    Subject = SubjectOf(ticket.ContentItemId),
                    Kind = TicketsConstants.EntryKinds.Reply,
                    Visibility = TimelineVisibility.Public,
                    Author = actor.Party,
                    AuthorName = approverName,
                    Body = S["Your request wasn't approved: {0}", comment],
                }, cancellationToken);

                if (added.Entry is { } entry && added.Errors.Count == 0)
                {
                    Enqueue(observer => observer.RepliedAsync(ticket, entry, false, cancellationToken));
                }
            }

            if (changes.Count > 0)
            {
                Enqueue(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));
            }

            if (next is not null)
            {
                Enqueue(observer => observer.ApprovalRequestedAsync(ticket, next, cancellationToken));
            }

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TicketResult> CancelApprovalAsync(string ticketId, string reason, TicketActor actor, CancellationToken cancellationToken = default)
        => WithLockAsync(ticketId, actor, async ticket =>
        {
            var part = ticket.GetOrCreate<TicketPart>();

            if (part.Approval?.State != TicketApprovalState.Pending)
            {
                return Refuse(S["The ticket isn't waiting for approval."], ticket);
            }

            reason = NullIfEmpty(reason?.Trim());

            if (reason is null)
            {
                return Refuse(S["Say why the approval is canceled."], ticket);
            }

            var statuses = await _statuses.GetAllAsync(cancellationToken);
            var held = statuses.FirstOrDefault(status => status.ItemId == part.Approval.HeldStatusId);
            var targetStatusId = held is not null && TicketLifecycle.Transitions.CanMove(part.StatusCategory, held.Category)
                ? held.ItemId
                : TicketLifecycle.FirstOf(statuses, TicketStatusCategory.Open)?.ItemId;

            part.Approval.State = TicketApprovalState.Canceled;

            var changes = new List<TimelineFieldChange>();

            if (!string.IsNullOrEmpty(targetStatusId) && targetStatusId != part.StatusId)
            {
                var errors = await ApplyAsync(ticket, part, new TicketUpdate { StatusId = targetStatusId }, changes, byApproval: true, cancellationToken);

                if (errors.Count > 0)
                {
                    return new TicketResult { Ticket = ticket, Errors = errors };
                }
            }

            await SaveAsync(ticket, part, cancellationToken);
            await AddEventAsync(ticket.ContentItemId, actor ?? TicketActor.Site, S["Approval canceled: {0}", reason], changes, cancellationToken);

            if (changes.Count > 0)
            {
                Enqueue(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));
            }

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);

    // With Account E-mail Domains on, a ticket without an account is filed under the account whose domain the
    // requester's e-mail is on.
    private async Task<string> AccountByEmailAsync(TicketPart part, CancellationToken cancellationToken)
        => _serviceProvider.GetService<Accounts.Services.IAccountEmailDomains>() is { } domains && !string.IsNullOrWhiteSpace(part.RequesterContact?.Email)
            ? await domains.FindAccountIdAsync(part.RequesterContact.Email, cancellationToken)
            : null;

    // The service-level policy the account is entitled to, if it names one.
    private async Task<string> AccountPolicyAsync(string accountId)
        => string.IsNullOrEmpty(accountId)
            ? null
            : (await _contentManager.GetAsync(accountId, VersionOptions.Published))?.TryGet<TicketAccountPart>(out var entitlement) == true
                ? entitlement.ServiceLevelPolicyId
                : null;

    // The sources the desk honors; a desk that never chose honors the sources that are on by default.
    private List<string> HonoredSources(TicketDesk desk)
        => desk?.AvailabilitySources
            ?? [.. _serviceProvider.GetServices<ITeamMemberAvailabilitySource>().Where(source => source.HonoredByDefault).Select(source => source.Name)];

    /// <inheritdoc/>
    public Task<TicketResult> DeleteAsync(string ticketId, CancellationToken cancellationToken = default)
        => WithLockAsync(ticketId, async ticket =>
        {
            // The ticket's timeline and timers go with it (TicketContentCleanup), whichever way the item is removed.
            await _contentManager.RemoveAsync(ticket);
            Enqueue(observer => observer.DeletedAsync(ticket, cancellationToken));

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);

    /// <inheritdoc/>
    public Task<TicketResult> EditAsync(string ticketId, Func<ContentItem, Task<bool>> edit, TicketActor actor = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edit);

        return WithLockAsync(ticketId, actor, async ticket =>
        {
            if (TicketLifecycle.IsReadOnly(ticket.GetOrCreate<TicketPart>()))
            {
                return Refuse(S["A closed or merged ticket can't change."], ticket);
            }

            var subjectBefore = ticket.DisplayText;

            if (!await edit(ticket))
            {
                return Refuse(S["Correct the fields and try again."], ticket);
            }

            var changes = new List<TimelineFieldChange>();

            if (!string.Equals(subjectBefore, ticket.DisplayText, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(ticket.DisplayText))
                {
                    return Refuse(S["Write a subject."], ticket);
                }

                changes.Add(new TimelineFieldChange { Field = S["Subject"], Key = TicketChangeKeys.Subject, From = subjectBefore, To = ticket.DisplayText });
            }

            // The editor changes the type's fields; the ticket's own values are read back from the edited item.
            await SaveAsync(ticket, ticket.GetOrCreate<TicketPart>(), cancellationToken);
            await AddEventAsync(ticket.ContentItemId, actor ?? TicketActor.Site, S["Changed the ticket's fields."], changes, cancellationToken);
            Enqueue(observer => observer.UpdatedAsync(ticket, changes, cancellationToken));

            return new TicketResult { Ticket = ticket };
        }, cancellationToken);
    }

    private Task<TicketResult> WithLockAsync(string ticketId, Func<ContentItem, Task<TicketResult>> work, CancellationToken cancellationToken)
        => WithLockAsync(ticketId, null, work, cancellationToken);

    private async Task<TicketResult> WithLockAsync(string ticketId, TicketActor actor, Func<ContentItem, Task<TicketResult>> work, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(ticketId))
        {
            return Refuse(S["The ticket no longer exists."]);
        }

        return await WithLocksAsync([ticketId], actor, tickets => work(tickets[ticketId]), cancellationToken);
    }

    // Locks the tickets (in id order), loads each as saved, does the work and, if it succeeded, commits it while the locks
    // are still held: the next change of a ticket reads what this one saved. Only then are the locks released and the
    // observers told, so a workflow sees the saved ticket and can lock it itself. A change that is refused writes nothing
    // and tells nobody.
    private async Task<TicketResult> WithLocksAsync(IReadOnlyList<string> ticketIds, TicketActor actor, Func<IReadOnlyDictionary<string, ContentItem>, Task<TicketResult>> work, CancellationToken cancellationToken)
    {
        var lockers = new List<ILocker>();
        TicketResult result;

        try
        {
            foreach (var id in ticketIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"ticket:{id}", _lockTimeout, _lockExpiration);

                if (!locked)
                {
                    return Refuse(S["Someone else is changing the ticket; try again."]);
                }

                lockers.Add(locker);
            }

            var tickets = new Dictionary<string, ContentItem>(StringComparer.Ordinal);

            foreach (var id in ticketIds)
            {
                var ticket = await LoadSavedAsync(id);

                if (ticket is null)
                {
                    return Refuse(S["The ticket no longer exists."]);
                }

                cancellationToken.ThrowIfCancellationRequested();

                var loaded = ticket.GetOrCreate<TicketPart>();
                _snapshots[ticket.ContentItemId] = (loaded.AssigneeId, loaded.StatusId);
                tickets[id] = ticket;
            }

            result = await work(tickets);

            if (!result.Succeeded)
            {
                _pending.Clear();

                return result;
            }

            await _session.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            for (var i = lockers.Count - 1; i >= 0; i--)
            {
                await lockers[i].DisposeAsync();
            }
        }

        await RaisePendingAsync(actor);

        return result;
    }

    // Reads the ticket as it was saved, not a copy this request read before it held the lock.
    private async Task<ContentItem> LoadSavedAsync(string ticketId)
    {
        if (await GetAsync(ticketId) is not { } cached)
        {
            return null;
        }

        _session.Detach(cached);
        _serviceProvider.GetService<IContentManagerSession>()?.Clear();

        return await GetAsync(ticketId);
    }

    private void Enqueue(Func<ITicketObserver, Task> notify)
        => _pending.Add(notify);

    // Tells the observers what was queued, in order. An observer that changes a ticket (a workflow task) queues and raises
    // its own events through its own call.
    private async Task RaisePendingAsync(TicketActor actor)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var pending = _pending.ToArray();
        _pending.Clear();

        using var scope = TicketEventActor.Use(actor ?? TicketActor.Site);

        foreach (var notify in pending)
        {
            await NotifyAsync(notify);
        }
    }

    private async Task SaveAsync(ContentItem ticket, TicketPart part, CancellationToken cancellationToken)
    {
        part.NextDueUtc = await GetNextDueAsync(ticket.ContentItemId, cancellationToken);
        ticket.Apply(part);
        await _contentManager.UpdateAsync(ticket);
        await RaiseChangesAsync(ticket, part, cancellationToken);
    }

    // Tells observers a ticket was assigned or moved to another status since it was loaded (or created).
    private async Task RaiseChangesAsync(ContentItem ticket, TicketPart part, CancellationToken cancellationToken)
    {
        if (!_snapshots.TryGetValue(ticket.ContentItemId, out var before))
        {
            return;
        }

        _snapshots[ticket.ContentItemId] = (part.AssigneeId, part.StatusId);

        if (!string.Equals(before.AssigneeId, part.AssigneeId, StringComparison.Ordinal))
        {
            Enqueue(observer => observer.AssignedAsync(ticket, before.AssigneeId, cancellationToken));
        }

        if (!string.Equals(before.StatusId, part.StatusId, StringComparison.Ordinal))
        {
            var previous = string.IsNullOrEmpty(before.StatusId) ? null : await _statuses.FindByIdAsync(before.StatusId, cancellationToken);
            var current = await _statuses.FindByIdAsync(part.StatusId, cancellationToken);

            Enqueue(observer => observer.StatusChangedAsync(ticket, previous, current, cancellationToken));
        }
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
        => reply.Actor ?? (reply.Author.IsEmpty ? new TicketActor { Name = reply.AuthorName } : new TicketActor { Party = reply.Author, Name = reply.AuthorName });

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static TicketResult Refuse(string error, ContentItem ticket = null)
        => new() { Ticket = ticket, Errors = [error] };
}
