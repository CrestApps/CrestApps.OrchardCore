using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Reports;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Reports.DataSources;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DataSources;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using ServiceLevelsDataSourcesStartup = CrestApps.OrchardCore.ServiceLevels.DataSourcesStartup;
using TicketsDataSourcesStartup = CrestApps.OrchardCore.Tickets.DataSourcesStartup;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The Tickets data sets, read from SQLite, show each reader the tickets of their scope only: a reader who sees their
/// teams' tickets on one desk gets those tickets, their replies and notes, their changes, links and backlog rows, and
/// nothing of another team or desk; a reader who sees every ticket gets them all; a reader with no scope gets no data
/// set. The daily backlog snapshot counts the open tickets of each desk once a day, on the site's day. The service-level
/// timers of tickets follow the same scope through the ticket timer access provider.
/// </summary>
public sealed class TicketReportDataSourceTests
{
    [Fact]
    public async Task ATeamReader_SeesTheirTeamsTickets_AndEveryoneReaderSeesAll_AndAStrangerNothing()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);

        // Act
        var ana = await ReadAsync(harness, TicketReportDataSets.Tickets, User("ana"));
        var boss = await ReadAsync(harness, TicketReportDataSets.Tickets, User("boss"));
        var strangerSets = await RunAsync(harness, source => source.GetDataSetsAsync(new DataSourceContext { User = User("stranger") }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["t1", "t2", "t5"], ana.Select(row => (string)row[TicketReportDataSets.TicketIdField]).Order());
        Assert.Equal(["t1", "t2", "t3", "t4", "t5"], boss.Select(row => (string)row[TicketReportDataSets.TicketIdField]).Order());
        Assert.Empty(strangerSets);

        var t1 = ana.Single(row => (string)row[TicketReportDataSets.TicketIdField] == "t1");
        Assert.Equal("Support", t1["Desk"]);
        Assert.Equal("Open", t1["Status"]);
        Assert.Equal(90d, Convert.ToDouble(t1["FirstResponseMinutes"]));
    }

    [Fact]
    public async Task EntriesChangesLinksAndBacklog_FollowTheSameScope()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);
        var ana = User("ana");

        // Act
        var entries = await ReadAsync(harness, TicketReportDataSets.TicketEntries, ana);
        var changes = await ReadAsync(harness, TicketReportDataSets.TicketChanges, ana);
        var links = await ReadAsync(harness, TicketReportDataSets.TicketLinks, ana);
        var backlog = await ReadAsync(harness, TicketReportDataSets.TicketBacklog, ana);
        var allEntries = await ReadAsync(harness, TicketReportDataSets.TicketEntries, User("boss"));

        // Assert
        var entry = Assert.Single(entries);
        Assert.Equal("t1", entry[TicketReportDataSets.TicketIdField]);
        Assert.Equal(TicketsConstants.EntryKinds.Reply, entry["Kind"]);
        Assert.Equal(["e1", "e3", "e4"], allEntries.Select(row => (string)row["EntryId"]).Order());

        var change = Assert.Single(changes);
        Assert.Equal("t2", change[TicketReportDataSets.TicketIdField]);
        Assert.Equal("Status", change["Field"]);
        Assert.Equal("Pending", change["To"]);

        var link = Assert.Single(links);
        Assert.Equal("Order", link["RecordKind"]);

        Assert.Equal(2, backlog.Count);
        Assert.All(backlog, row => Assert.Equal("support", row["DeskId"]));
        Assert.DoesNotContain(backlog, row => (string)row["AssigneeId"] == "ben");
    }

    [Fact]
    public async Task TheBacklogSnapshot_CountsOpenTicketsOnTheSitesDay_OncePerDeskAndDay()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<TicketBacklogRecorder>().RecordAsync(TestContext.Current.CancellationToken));
        var again = await harness.RunAsync(services => services.GetRequiredService<TicketBacklogRecorder>().RecordAsync(TestContext.Current.CancellationToken));
        var days = await harness.RunAsync(
            async services => (await services.GetRequiredService<ISession>().Query<TicketBacklogDay, TicketBacklogIndex>().ListAsync(TestContext.Current.CancellationToken)).ToList(),
            commit: false);

        // Assert
        Assert.Equal(2, first);
        Assert.Equal(0, again);

        // 12:00 UTC on 2 November is 1:00 on 3 November in Auckland.
        var support = days.Single(day => day.DeskId == "support" && day.Day == "2026-11-03");
        Assert.Equal(3, support.Groups.Sum(group => group.Count));
        var anas = support.Groups.Single(group => group.AssigneeId == "ana");
        Assert.Equal(1, anas.Overdue);
        Assert.Equal(TicketStatusCategory.Open, anas.StatusCategory);
        Assert.DoesNotContain(support.Groups, group => group.StatusCategory == TicketStatusCategory.Solved);
    }

    [Fact]
    public async Task ServiceLevelTimers_OfTicketsFollowTheScope_AndAPolicyManagerSeesEveryTimer()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);

        // Act
        var ana = await ReadTimersAsync(harness, User("ana"));
        var manager = await ReadTimersAsync(harness, CalendarTestHarness.User("lead", ServiceLevelsPermissions.ManageServiceLevelPolicies.Name));

        // Assert
        Assert.Equal(["s1", "s2"], ana.Select(row => (string)row["TimerId"]).Order());
        Assert.Equal(["s1", "s2", "s3", "s9"], manager.Select(row => (string)row["TimerId"]).Order());
    }

    [Fact]
    public async Task TwoNodesRecordingAtOnce_RecordADesksDayOnce()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);

        // Act
        var results = await Task.WhenAll(
            harness.RunAsync(services => services.GetRequiredService<TicketBacklogRecorder>().RecordAsync(TestContext.Current.CancellationToken)),
            harness.RunAsync(services => services.GetRequiredService<TicketBacklogRecorder>().RecordAsync(TestContext.Current.CancellationToken)));
        var rows = await harness.RunAsync(
            services => services.GetRequiredService<ISession>().QueryIndex<TicketBacklogIndex>(index => index.Day == "2026-11-03").CountAsync(TestContext.Current.CancellationToken),
            commit: false);

        // Assert
        Assert.Equal(2, results.Sum());
        Assert.Equal(2, rows);
    }

    [Fact]
    public async Task ARunLateInTheDay_StillRecordsThatDay()
    {
        // Arrange: 12:00 UTC on 2 November is 23:00 in Noumea, the last hour of that day there.
        await using var harness = await CreateAsync(services => services.AddSingleton(LocalClock("Pacific/Noumea")));
        await SeedAsync(harness);

        // Act
        var recorded = await harness.RunAsync(services => services.GetRequiredService<TicketBacklogRecorder>().RecordAsync(TestContext.Current.CancellationToken));
        var days = await harness.RunAsync(
            async services => (await services.GetRequiredService<ISession>().QueryIndex<TicketBacklogIndex>(index => index.Day == "2026-11-02").ListAsync(TestContext.Current.CancellationToken)).Select(index => index.DeskId).Order().ToList(),
            commit: false);

        // Assert: HR's day is recorded at 23:00; Support's, already recorded earlier that day, isn't recorded twice.
        Assert.Equal(1, recorded);
        Assert.Equal(["hr", "support"], days);
    }

    [Fact]
    public async Task ARedactedEntrysText_IsEmptyInTheEntriesDataSet()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SeedAsync(harness);
        await harness.RunAsync(async services =>
        {
            var redacted = Entry("e9", "t1", TicketsConstants.EntryKinds.Reply, CalendarTestHarness.Now.AddMinutes(-5));
            redacted.Body = "My card number is 4111";
            redacted.RedactedUtc = CalendarTestHarness.Now;
            await services.GetRequiredService<ISession>().SaveAsync(redacted, false, TimelineConstants.CollectionName);
        });

        // Act
        var entries = await ReadAsync(harness, TicketReportDataSets.TicketEntries, User("boss"));

        // Assert
        var row = Assert.Single(entries, row => (string)row["EntryId"] == "e9");
        Assert.Null(row["Body"]);
        Assert.Equal(true, row["Redacted"]);
        Assert.DoesNotContain(entries, row => row["Body"] is string text && text.Contains("4111", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AReaderWhoLosesAccess_GetsNothingOnTheNextRun()
    {
        // Arrange: a pipeline runs as its publisher, whose scope is read again on every run.
        var scopes = new Dictionary<string, TicketScopedQuery>(StringComparer.Ordinal)
        {
            ["pat"] = new TicketScopedQuery { AllDeskIds = ["support"], UserId = "pat" },
        };
        await using var harness = await CreateAsync(services =>
        {
            var access = new Mock<ITicketAccessService>();
            access.Setup(service => service.ApplyScopeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<TicketQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ClaimsPrincipal user, TicketQuery query, CancellationToken _) => scopes.TryGetValue(user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, out var scope) ? scope : null);
            services.AddSingleton(access.Object);
        });
        await SeedAsync(harness);
        var before = await ReadAsync(harness, TicketReportDataSets.Tickets, User("pat"));

        // Act
        scopes.Remove("pat");
        var after = await ReadAsync(harness, TicketReportDataSets.Tickets, User("pat"));

        // Assert
        Assert.NotEmpty(before);
        Assert.Empty(after);
    }

    [Fact]
    public async Task TimelineAccess_AnswersABatchFromTheIndex_ScopeForAgents_AndOwnTicketsForTheRequester()
    {
        // Arrange
        await using var harness = await CreateAsync(services => services.AddScoped(provider => new TicketTimelineAccess(Mock.Of<IContentManager>(), provider.GetRequiredService<ITicketAccessService>(), provider.GetRequiredService<ISession>())));
        await SeedAsync(harness);
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var own = await session.Query<ContentItem, TicketIndex>(index => index.ContentItemId == "t4").FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            own.Alter<TicketPart>(part => part.Requester = new PartyRef(PartyKinds.User, "dana"));
            await session.SaveAsync(own);
        });

        // Act
        var visible = await harness.RunAsync(
            async services =>
            {
                var access = services.GetRequiredService<TicketTimelineAccess>();
                var ids = new[] { "t1", "t2", "t3", "t4", "t5" };

                return new
                {
                    Ana = await access.GetVisibleAsync(User("ana"), ids, TimelineVisibility.Internal, TestContext.Current.CancellationToken),
                    DanaPublic = await access.GetVisibleAsync(User("dana"), ids, TimelineVisibility.Public, TestContext.Current.CancellationToken),
                    DanaInternal = await access.GetVisibleAsync(User("dana"), ids, TimelineVisibility.Internal, TestContext.Current.CancellationToken),
                };
            },
            commit: false);

        // Assert
        Assert.Equal(["t1", "t2", "t5"], visible.Ana.Order());
        Assert.Equal(["t4"], visible.DanaPublic);
        Assert.Empty(visible.DanaInternal);
    }

    private sealed class TestLock : IDistributedLock
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            await _semaphore.WaitAsync();

            return new Locker(_semaphore);
        }

        public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
            => await _semaphore.WaitAsync(timeout) ? (new Locker(_semaphore), true) : (null, false);

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(_semaphore.CurrentCount == 0);

        private sealed class Locker : ILocker
        {
            private readonly SemaphoreSlim _semaphore;

            public Locker(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
                => _semaphore.Release();

            public ValueTask DisposeAsync()
            {
                _semaphore.Release();

                return ValueTask.CompletedTask;
            }
        }
    }

    internal static Task<CalendarTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddTicketsCore();
                services.AddSingleton(LocalClock("Pacific/Auckland"));
                services.AddSingleton(Access());
                services.AddSingleton(Catalog(new TicketDesk { ItemId = "support", Name = "Support" }, new TicketDesk { ItemId = "hr", Name = "HR" }));
                services.AddSingleton(Catalog(new TicketStatus { ItemId = "open", Name = "Open", Category = TicketStatusCategory.Open }));
                services.AddSingleton(Catalog(new TicketPriority { ItemId = "normal", Name = "Normal" }));
                services.AddSingleton<IDistributedLock, TestLock>();

                // The category names of the data set read the ticket types' taxonomies; these sites have none.
                services.AddSingleton(Mock.Of<IContentManager>());
                services.AddSingleton(new Mock<IContentDefinitionManager>().Object);
                new TicketsDataSourcesStartup().ConfigureServices(services);
                new ServiceLevelsDataSourcesStartup().ConfigureServices(services);
                configure?.Invoke(services);
            },
            async schema =>
            {
                await TicketIndexSchema.CreateAsync(schema);
                await TicketIndexSchema.CreateBacklogIndexAsync(schema);
                await TimelineIndexSchema.CreateAsync(schema);
                await ServiceLevelsIndexSchema.CreateAsync(schema);
            },
            [new TicketIndexProvider(), new TicketBacklogIndexProvider(), new TimelineEntryIndexProvider(), new ServiceLevelTimerIndexProvider()],
            [TimelineConstants.CollectionName, ServiceLevelsConstants.CollectionName]);

    internal static Task SeedAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var now = CalendarTestHarness.Now;
            var session = services.GetRequiredService<ISession>();

            var t1 = Ticket("t1", "support", "teamA", null, TicketStatusCategory.Open, now.AddHours(-3));
            t1.Alter<TicketPart>(part =>
            {
                part.FirstResponseUtc = now.AddHours(-1.5);
                part.Links.Add(new TicketLink { Record = new RecordRef("Order", "o1"), Kind = "Related" });
            });
            await session.SaveAsync(t1);
            await session.SaveAsync(Ticket("t2", "support", "teamB", "ana", TicketStatusCategory.Open, now.AddHours(-10), dueUtc: now.AddHours(-1)));
            await session.SaveAsync(Ticket("t3", "support", "teamB", "ben", TicketStatusCategory.Pending, now.AddHours(-2)));
            await session.SaveAsync(Ticket("t4", "hr", "teamA", null, TicketStatusCategory.Open, now.AddHours(-4)));
            var t5 = Ticket("t5", "support", "teamA", "ana", TicketStatusCategory.Solved, now.AddDays(-2));
            t5.Alter<TicketPart>(part => part.SolvedUtc = now.AddDays(-1));
            await session.SaveAsync(t5);

            await session.SaveAsync(Entry("e1", "t1", TicketsConstants.EntryKinds.Reply, now.AddHours(-1.5)), false, TimelineConstants.CollectionName);
            var e2 = Entry("e2", "t2", TicketsConstants.EntryKinds.Event, now.AddHours(-1));
            e2.Changes.Add(new TimelineFieldChange { Field = "Status", From = "Open", To = "Pending" });
            await session.SaveAsync(e2, false, TimelineConstants.CollectionName);
            await session.SaveAsync(Entry("e3", "t3", TicketsConstants.EntryKinds.Note, now.AddHours(-1)), false, TimelineConstants.CollectionName);
            await session.SaveAsync(Entry("e4", "t4", TicketsConstants.EntryKinds.Reply, now.AddHours(-1)), false, TimelineConstants.CollectionName);

            await session.SaveAsync(new TicketBacklogDay
            {
                Day = "2026-11-02",
                DeskId = "support",
                TakenUtc = now.AddHours(-11),
                Groups =
                [
                    new TicketBacklogGroup { TeamId = "teamA", Count = 1, StatusId = "open", StatusCategory = TicketStatusCategory.Open },
                    new TicketBacklogGroup { TeamId = "teamB", AssigneeId = "ana", Count = 1, StatusId = "open", StatusCategory = TicketStatusCategory.Open },
                    new TicketBacklogGroup { TeamId = "teamB", AssigneeId = "ben", Count = 1, StatusId = "open", StatusCategory = TicketStatusCategory.Open },
                ],
            });

            await session.SaveAsync(Timer("s1", TicketsConstants.RecordKind, "t1", now.AddHours(-3)), false, ServiceLevelsConstants.CollectionName);
            var s2 = Timer("s2", TicketsConstants.RecordKind, "t2", now.AddHours(-10));
            s2.State = ServiceLevelTimerState.Completed;
            s2.EndedUtc = now.AddHours(-1);
            s2.BreachedUtc = now.AddHours(-9);
            s2.ElapsedSeconds = 7200;
            await session.SaveAsync(s2, false, ServiceLevelsConstants.CollectionName);
            await session.SaveAsync(Timer("s3", TicketsConstants.RecordKind, "t3", now.AddHours(-2)), false, ServiceLevelsConstants.CollectionName);
            await session.SaveAsync(Timer("s9", "WorkOrder", "w1", now.AddHours(-2)), false, ServiceLevelsConstants.CollectionName);
        });

    private static ContentItem Ticket(string id, string deskId, string teamId, string assigneeId, TicketStatusCategory category, DateTime createdUtc, DateTime? dueUtc = null)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentType = TicketsConstants.DefaultContentType,
            Latest = true,
            Published = true,
            DisplayText = id,
            CreatedUtc = createdUtc,
            ModifiedUtc = createdUtc,
        };
        item.Apply(new TicketPart
        {
            Number = id,
            DeskId = deskId,
            TeamId = teamId,
            AssigneeId = assigneeId,
            StatusId = "open",
            StatusCategory = category,
            PriorityId = "normal",
            NextDueUtc = dueUtc,
        });

        return item;
    }

    private static TimelineEntry Entry(string id, string ticketId, string kind, DateTime createdUtc)
        => new()
        {
            ItemId = id,
            Subject = new RecordRef(TicketsConstants.RecordKind, ticketId),
            Kind = kind,
            Visibility = kind == TicketsConstants.EntryKinds.Note ? TimelineVisibility.Internal : TimelineVisibility.Public,
            Author = new PartyRef(PartyKinds.User, "agent"),
            AuthorName = "Agent",
            Body = "Text",
            CreatedUtc = createdUtc,
        };

    private static ServiceLevelTimer Timer(string id, string kind, string subjectId, DateTime startedUtc)
        => new()
        {
            ItemId = id,
            Subject = new RecordRef(kind, subjectId),
            Metric = "FirstResponse",
            TargetSeconds = 3600,
            State = ServiceLevelTimerState.Running,
            StartedUtc = startedUtc,
            DueUtc = startedUtc.AddHours(1),
            WarnAtUtc = startedUtc.AddMinutes(48),
        };

    private static ILocalClock LocalClock(string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var clock = new Mock<ILocalClock>();
        clock.Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, zone));

        return clock.Object;
    }

    internal static ClaimsPrincipal User(string userId)
        => CalendarTestHarness.User(userId);

    private static ITicketAccessService Access()
    {
        var access = new Mock<ITicketAccessService>();
        access.Setup(service => service.ApplyScopeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<TicketQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal user, TicketQuery query, CancellationToken _) => user?.FindFirstValue(ClaimTypes.NameIdentifier) switch
            {
                "ana" => new TicketScopedQuery { Query = query, TeamDeskIds = ["support"], TeamIds = ["teamA"], UserId = "ana" },
                "boss" => new TicketScopedQuery { Query = query, AllDeskIds = ["support", "hr"], UserId = "boss" },
                _ => null,
            });

        return access.Object;
    }

    private static ICatalog<T> Catalog<T>(params T[] records)
        where T : class
    {
        var catalog = new Mock<ICatalog<T>>();
        catalog.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(records);

        return catalog.Object;
    }

    private static async Task<T> RunAsync<T>(CalendarTestHarness harness, Func<TicketReportDataSource, Task<T>> work)
    {
        await using var scope = harness.CreateScope();

        return await work(scope.ServiceProvider.GetRequiredService<TicketReportDataSource>());
    }

    private static async Task<List<Dictionary<string, object>>> ReadAsync(CalendarTestHarness harness, string dataSet, ClaimsPrincipal user)
        => await RunAsync(harness, async source => Rows(await ReportDesignerTestServices.ReadAsync(source, Query(dataSet, user), TestContext.Current.CancellationToken)));

    private static async Task<List<Dictionary<string, object>>> ReadTimersAsync(CalendarTestHarness harness, ClaimsPrincipal user)
    {
        await using var scope = harness.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<ServiceLevelReportDataSource>();

        return Rows(await ReportDesignerTestServices.ReadAsync(source, Query("ServiceLevelTimers", user), TestContext.Current.CancellationToken));
    }

    private static DataSourceQuery Query(string dataSet, ClaimsPrincipal user)
        => new()
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = user },
        };

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
        => table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
}
