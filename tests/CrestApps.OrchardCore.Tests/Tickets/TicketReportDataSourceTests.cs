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
using OrchardCore.DataSources;
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
        Assert.Equal(["s1"], ana.Select(row => (string)row["TimerId"]));
        Assert.Equal(["s1", "s3", "s9"], manager.Select(row => (string)row["TimerId"]).Order());
    }

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddTicketsCore();
                services.AddSingleton(LocalClock("Pacific/Auckland"));
                services.AddSingleton(Access());
                services.AddSingleton(Catalog(new TicketDesk { ItemId = "support", Name = "Support" }, new TicketDesk { ItemId = "hr", Name = "HR" }));
                services.AddSingleton(Catalog(new TicketStatus { ItemId = "open", Name = "Open", Category = TicketStatusCategory.Open }));
                services.AddSingleton(Catalog(new TicketPriority { ItemId = "normal", Name = "Normal" }));
                new TicketsDataSourcesStartup().ConfigureServices(services);
                new ServiceLevelsDataSourcesStartup().ConfigureServices(services);
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

    private static Task SeedAsync(CalendarTestHarness harness)
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
            await session.SaveAsync(Ticket("t5", "support", "teamA", "ana", TicketStatusCategory.Solved, now.AddDays(-2)));

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

    private static ClaimsPrincipal User(string userId)
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
