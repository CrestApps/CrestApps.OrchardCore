using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket service: a new ticket is numbered and filed with its first reply and its service-level timers; the first
/// agent reply completes the first response and opens the ticket; a requester's reply reopens a solved ticket inside the
/// window and opens a linked follow-up after it; solving completes the resolution and closing ends the timers; a closed
/// ticket can't change; a merge moves the requester to the other ticket and closes the merged one.
/// </summary>
public sealed class TicketServiceTests
{
    private static readonly DateTime _now = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);
    private static readonly PartyRef _requester = new(PartyKinds.Customer, "c1");
    private static readonly PartyRef _agent = new(PartyKinds.User, "agent-1");

    private readonly Dictionary<string, ContentItem> _items = [];
    private readonly InMemoryTimelineService _timeline = new();
    private readonly FakeTracker _tracker = new();
    private readonly List<string> _observed = [];
    private DateTime _clockNow = _now;
    private long _number;

    private static readonly TicketStatus[] _statuses =
    [
        new() { ItemId = "new", Name = "New", Category = TicketStatusCategory.New },
        new() { ItemId = "open", Name = "Open", Category = TicketStatusCategory.Open },
        new() { ItemId = "pending", Name = "Waiting for you", Category = TicketStatusCategory.Pending, PausesTimers = true },
        new() { ItemId = "solved", Name = "Solved", Category = TicketStatusCategory.Solved },
        new() { ItemId = "closed", Name = "Closed", Category = TicketStatusCategory.Closed },
    ];

    private static readonly TicketPriority[] _priorities =
    [
        new() { ItemId = "low", Name = "Low", Rank = 1 },
        new() { ItemId = "normal", Name = "Normal", Rank = 2 },
        new() { ItemId = "urgent", Name = "Urgent", Rank = 4 },
    ];

    private static readonly TicketDesk _desk = new() { ItemId = "support", Name = "Support", ServiceLevelPolicyId = "sla", ReopenWindowDays = 7 };

    [Fact]
    public async Task ANewTicket_IsNumberedAndFiled_WithItsFirstReplyAndTimers()
    {
        // Arrange
        var service = Create();

        // Act
        var result = await service.CreateAsync(new TicketCreateRequest { Subject = "  Printer jams  ", Body = "It jams on every page.", Requester = _requester }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var part = result.Ticket.GetOrCreate<TicketPart>();
        Assert.Equal("T-1", part.Number);
        Assert.Equal("Printer jams", result.Ticket.DisplayText);
        Assert.Equal("support", part.DeskId);
        Assert.Equal("new", part.StatusId);
        Assert.Equal("normal", part.PriorityId);
        Assert.Equal(2, part.PriorityRank);
        Assert.Equal(1, part.ReplyCount);
        var reply = Assert.Single(_timeline.Of(Subject(result.Ticket)));
        Assert.Equal(TicketsConstants.EntryKinds.Reply, reply.Kind);
        Assert.Equal(TimelineVisibility.Public, reply.Visibility);
        Assert.Equal(_requester, reply.Author);
        Assert.Equal([ServiceLevelMetrics.FirstResponse, ServiceLevelMetrics.Resolution], _tracker.Timers.Select(timer => timer.Metric));
        Assert.All(_tracker.Timers, timer => Assert.Equal("Normal", timer.Priority));
        Assert.Equal(_now.AddHours(1), part.NextDueUtc);
        Assert.Contains("created", _observed);
    }

    [Fact]
    public async Task ATicket_NeedsATicketTypeAndASubject()
    {
        // Arrange
        var service = Create();

        // Act
        var notATicket = await service.CreateAsync(new TicketCreateRequest { ContentType = "Article", Subject = "Hi" }, TestContext.Current.CancellationToken);
        var noSubject = await service.CreateAsync(new TicketCreateRequest { Subject = " " }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(notATicket.Succeeded);
        Assert.False(noSubject.Succeeded);
        Assert.Empty(_items);
    }

    [Fact]
    public async Task TheFirstAgentReply_CompletesTheFirstResponse_AndOpensTheTicket()
    {
        // Arrange
        var service = Create();
        var ticket = (await service.CreateAsync(new TicketCreateRequest { Subject = "Printer jams", Body = "Help", Requester = _requester }, TestContext.Current.CancellationToken)).Ticket;
        _clockNow = _now.AddMinutes(20);

        // Act
        var result = await service.ReplyAsync(ticket.ContentItemId, new TicketReply { Body = "Try the rear tray.", Author = _agent }, TestContext.Current.CancellationToken);
        var note = await service.ReplyAsync(ticket.ContentItemId, new TicketReply { Body = "Known issue.", Author = _agent, IsInternal = true }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(note.Succeeded);
        var part = result.Ticket.GetOrCreate<TicketPart>();
        Assert.Equal(_now.AddMinutes(20), part.FirstResponseUtc);
        Assert.Equal("open", part.StatusId);
        Assert.Equal(2, part.ReplyCount);
        Assert.Equal(ServiceLevelTimerState.Completed, _tracker.Find(ServiceLevelMetrics.FirstResponse).State);
        Assert.Contains(_timeline.Of(Subject(ticket)), entry => entry.Kind == TicketsConstants.EntryKinds.Note && entry.Visibility == TimelineVisibility.Internal);
    }

    [Fact]
    public async Task SolvingCompletesTheResolution_AndAReplyInsideTheWindowReopens()
    {
        // Arrange
        var service = Create();
        var ticket = (await service.CreateAsync(new TicketCreateRequest { Subject = "Printer jams", Body = "Help", Requester = _requester }, TestContext.Current.CancellationToken)).Ticket;
        await service.ReplyAsync(ticket.ContentItemId, new TicketReply { Body = "Fixed.", Author = _agent, SetStatusId = "solved" }, TestContext.Current.CancellationToken);
        _clockNow = _now.AddDays(2);

        // Act
        var reopened = await service.ReplyAsync(ticket.ContentItemId, new TicketReply { Body = "It jams again.", Author = _requester, FromRequester = true }, TestContext.Current.CancellationToken);

        // Assert
        var part = reopened.Ticket.GetOrCreate<TicketPart>();
        Assert.Equal("open", part.StatusId);
        Assert.Equal(1, part.ReopenCount);
        Assert.Equal(_now, part.SolvedUtc);
        // The reopened ticket keeps counting its one resolution against the same target instead of starting a new cycle.
        var resolution = Assert.Single(_tracker.Timers, timer => timer.Metric == ServiceLevelMetrics.Resolution);
        Assert.Equal(ServiceLevelTimerState.Running, resolution.State);
        Assert.Contains(_tracker.Timers, timer => timer.Metric == ServiceLevelMetrics.NextResponse && timer.State == ServiceLevelTimerState.Running);
    }

    [Fact]
    public async Task AReplyAfterTheWindow_OpensALinkedFollowUp()
    {
        // Arrange
        var service = Create();
        var ticket = (await service.CreateAsync(new TicketCreateRequest { Subject = "Printer jams", Body = "Help", Requester = _requester }, TestContext.Current.CancellationToken)).Ticket;
        await service.UpdateAsync(ticket.ContentItemId, new TicketUpdate { StatusId = "solved" }, cancellationToken: TestContext.Current.CancellationToken);
        _clockNow = _now.AddDays(30);

        // Act
        var result = await service.ReplyAsync(ticket.ContentItemId, new TicketReply { Body = "Again!", Author = _requester, FromRequester = true }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotEqual(ticket.ContentItemId, result.Ticket.ContentItemId);
        var followUp = result.Ticket.GetOrCreate<TicketPart>();
        Assert.Equal(ticket.ContentItemId, followUp.FollowUpOfId);
        Assert.Equal(_requester, followUp.Requester);
        Assert.Contains(followUp.Links, link => link.Record == Subject(ticket) && link.Kind == TicketLinkKinds.FollowUpOf);
    }

    [Fact]
    public async Task AClosedTicket_EndsItsTimers_AndCantChange()
    {
        // Arrange
        var service = Create();
        var ticket = (await service.CreateAsync(new TicketCreateRequest { Subject = "Printer jams", Body = "Help", Requester = _requester }, TestContext.Current.CancellationToken)).Ticket;

        // Act
        var closed = await service.UpdateAsync(ticket.ContentItemId, new TicketUpdate { StatusId = "closed" }, cancellationToken: TestContext.Current.CancellationToken);
        var changed = await service.UpdateAsync(ticket.ContentItemId, new TicketUpdate { PriorityId = "urgent" }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(closed.Succeeded);
        Assert.All(_tracker.Timers, timer => Assert.Equal(ServiceLevelTimerState.Cancelled, timer.State));
        Assert.False(changed.Succeeded);
        Assert.Contains(_timeline.Of(Subject(ticket)), entry => entry.Kind == TicketsConstants.EntryKinds.Event && entry.Changes.Any(change => change.To == "Closed"));
    }

    [Fact]
    public async Task APendingStatus_PausesTheTimers()
    {
        // Arrange
        var service = Create();
        var ticket = (await service.CreateAsync(new TicketCreateRequest { Subject = "Printer jams", Body = "Help", Requester = _requester }, TestContext.Current.CancellationToken)).Ticket;

        // Act
        await service.UpdateAsync(ticket.ContentItemId, new TicketUpdate { StatusId = "pending" }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.All(_tracker.Timers, timer => Assert.Equal(ServiceLevelTimerState.Paused, timer.State));
        Assert.Null(_items[ticket.ContentItemId].GetOrCreate<TicketPart>().NextDueUtc);
    }

    [Fact]
    public async Task AMerge_MovesTheRequesterAcross_AndClosesTheMergedTicket()
    {
        // Arrange
        var service = Create();
        var first = (await service.CreateAsync(new TicketCreateRequest { Subject = "Printer jams", Body = "Help", Requester = _requester }, TestContext.Current.CancellationToken)).Ticket;
        var other = new PartyRef(PartyKinds.Customer, "c2");
        var duplicate = (await service.CreateAsync(new TicketCreateRequest { Subject = "Printer broken", Body = "Same", Requester = other }, TestContext.Current.CancellationToken)).Ticket;

        // Act
        var result = await service.MergeAsync(duplicate.ContentItemId, first.ContentItemId, TicketActor.User("agent-1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains(other, _items[first.ContentItemId].GetOrCreate<TicketPart>().Participants);
        var merged = _items[duplicate.ContentItemId].GetOrCreate<TicketPart>();
        Assert.Equal(first.ContentItemId, merged.MergedIntoId);
        Assert.Equal(TicketStatusCategory.Closed, merged.StatusCategory);
        Assert.Contains("merged", _observed);
    }

    private static RecordRef Subject(ContentItem ticket)
        => new(TicketsConstants.RecordKind, ticket.ContentItemId);

    private DefaultTicketService Create()
    {
        var contentManager = new Mock<IContentManager>();

        contentManager.Setup(value => value.NewAsync(It.IsAny<string>()))
            .ReturnsAsync((string type) => new ContentItem { ContentItemId = $"ticket-{_items.Count + 1}", ContentType = type, CreatedUtc = _clockNow, Latest = true, Published = true });
        contentManager.Setup(value => value.CreateAsync(It.IsAny<ContentItem>(), It.IsAny<VersionOptions>()))
            .Callback((ContentItem item, VersionOptions _) => _items[item.ContentItemId] = item)
            .ReturnsAsync(true);
        contentManager.Setup(value => value.UpdateAsync(It.IsAny<ContentItem>()))
            .Callback((ContentItem item) => _items[item.ContentItemId] = item)
            .Returns(Task.CompletedTask);
        contentManager.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions _) => _items.GetValueOrDefault(id));

        var settings = new JsonObject { [nameof(TicketPartSettings)] = new JsonObject() };
        var ticketType = new ContentTypeDefinition("Ticket", "Ticket", [new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName), settings)], new JsonObject());
        var definitions = new Mock<IContentDefinitionManager>();

        definitions.Setup(value => value.GetTypeDefinitionAsync("Ticket")).ReturnsAsync(ticketType);
        definitions.Setup(value => value.GetTypeDefinitionAsync("Article")).ReturnsAsync(new ContentTypeDefinition("Article", "Article"));

        var numbers = new Mock<INumberSequence>();

        numbers.Setup(value => value.NextAsync(It.IsAny<string>(), It.IsAny<NumberSequenceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => ++_number);

        var locks = new Mock<IDistributedLock>();

        locks.Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((Mock.Of<ILocker>(), true));

        var clock = new Mock<IClock>();

        clock.SetupGet(value => value.UtcNow).Returns(() => _clockNow);

        var observer = new Mock<ITicketObserver>();

        observer.Setup(value => value.CreatedAsync(It.IsAny<ContentItem>(), It.IsAny<CancellationToken>())).Callback(() => _observed.Add("created")).Returns(Task.CompletedTask);
        observer.Setup(value => value.MergedAsync(It.IsAny<ContentItem>(), It.IsAny<ContentItem>(), It.IsAny<CancellationToken>())).Callback(() => _observed.Add("merged")).Returns(Task.CompletedTask);

        return new DefaultTicketService(
            contentManager.Object,
            definitions.Object,
            Mock.Of<ISession>(),
            Catalog(_statuses),
            Catalog(_priorities),
            Catalog([_desk]),
            _timeline,
            _tracker,
            Mock.Of<IPartyDirectory>(),
            numbers.Object,
            locks.Object,
            clock.Object,
            new ServiceCollection().BuildServiceProvider(),
            [observer.Object],
            NullLogger<DefaultTicketService>.Instance,
            new PassThroughLocalizer<DefaultTicketService>());
    }

    private static ICatalog<T> Catalog<T>(IReadOnlyCollection<T> items)
        where T : ITicketSettingsRecord
    {
        var catalog = new Mock<ICatalog<T>>();

        catalog.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([.. items]);
        catalog.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => items.FirstOrDefault(item => ((CrestApps.Core.Models.CatalogItem)(object)item).ItemId == id));

        return catalog.Object;
    }

    // Each metric's target is an hour; enough to see what the service starts, pauses, completes and cancels.
    private sealed class FakeTracker : IServiceLevelTracker
    {
        public List<ServiceLevelTimer> Timers { get; } = [];

        public ServiceLevelTimer Find(string metric)
            => Timers.Last(timer => timer.Metric == metric);

        public Task<ServiceLevelTimer> StartAsync(ServiceLevelStart start, CancellationToken cancellationToken = default)
        {
            var timer = new ServiceLevelTimer
            {
                Subject = start.Subject,
                Metric = start.Metric,
                PolicyId = start.PolicyId,
                Priority = start.Priority,
                State = start.Paused ? ServiceLevelTimerState.Paused : ServiceLevelTimerState.Running,
                StartedUtc = start.StartUtc ?? DateTime.MinValue,
                DueUtc = (start.StartUtc ?? DateTime.MinValue).AddHours(1),
            };

            Timers.Add(timer);

            return Task.FromResult(timer);
        }

        public Task<IReadOnlyList<ServiceLevelTimer>> PauseAsync(RecordRef subject, string metric = null, CancellationToken cancellationToken = default)
            => Change(subject, metric, ServiceLevelTimerState.Running, ServiceLevelTimerState.Paused);

        public Task<IReadOnlyList<ServiceLevelTimer>> ResumeAsync(RecordRef subject, string metric = null, CancellationToken cancellationToken = default)
            => Change(subject, metric, ServiceLevelTimerState.Paused, ServiceLevelTimerState.Running);

        public async Task<ServiceLevelTimer> CompleteAsync(RecordRef subject, string metric, DateTime? atUtc = null, CancellationToken cancellationToken = default)
        {
            var completed = await Change(subject, metric, null, ServiceLevelTimerState.Completed);

            return completed.Count > 0 ? completed[0] : null;
        }

        public Task<IReadOnlyList<ServiceLevelTimer>> CancelAsync(RecordRef subject, string metric = null, CancellationToken cancellationToken = default)
            => Change(subject, metric, null, ServiceLevelTimerState.Cancelled);

        public Task<ServiceLevelTimer> ReopenAsync(RecordRef subject, string metric, CancellationToken cancellationToken = default)
        {
            var timer = Timers.LastOrDefault(candidate => candidate.Subject == subject && candidate.Metric == metric);

            if (timer is { State: ServiceLevelTimerState.Completed })
            {
                timer.State = ServiceLevelTimerState.Running;
            }

            return Task.FromResult(timer is { IsActive: true } ? timer : null);
        }

        public Task<IReadOnlyList<ServiceLevelTimer>> RetargetAsync(RecordRef subject, string policyId, string priority, CancellationToken cancellationToken = default)
        {
            foreach (var timer in Timers.Where(timer => timer.Subject == subject && timer.IsActive))
            {
                timer.Priority = priority;
            }

            return Task.FromResult<IReadOnlyList<ServiceLevelTimer>>([]);
        }

        public Task<IReadOnlyList<ServiceLevelTimer>> GetTimersAsync(RecordRef subject, bool activeOnly = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelTimer>>([.. Timers.Where(timer => timer.Subject == subject && (!activeOnly || timer.IsActive))]);

        private Task<IReadOnlyList<ServiceLevelTimer>> Change(RecordRef subject, string metric, ServiceLevelTimerState? from, ServiceLevelTimerState to)
        {
            var changed = Timers
                .Where(timer => timer.Subject == subject && timer.IsActive && (metric is null || timer.Metric == metric) && (from is null || timer.State == from))
                .ToList();

            foreach (var timer in changed)
            {
                timer.State = to;
            }

            return Task.FromResult<IReadOnlyList<ServiceLevelTimer>>(changed);
        }
    }

    private sealed class PassThroughLocalizer<T> : Microsoft.Extensions.Localization.IStringLocalizer<T>
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name] => new(name, name);

        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
