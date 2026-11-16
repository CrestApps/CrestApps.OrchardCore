using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Reports;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using YesSql;
using ISession = YesSql.ISession;
using TicketsReportsStartup = CrestApps.OrchardCore.Tickets.ReportsStartup;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket time reports read the time logged on the reader's tickets: the period clips each entry, the reader's
/// ticket scope hides other desks' time, and a solved ticket's work time (the union of its entries) differs from its
/// effort (every person's minutes added up).
/// </summary>
public sealed class TicketTimeReportsTests
{
    private static readonly DateTime _day = CalendarTestHarness.Now.Date;

    [Fact]
    public async Task TicketTime_AddsUpTheReadersTicketTime_ByPersonAndAccount_ClippedToThePeriod()
    {
        // Arrange
        await using var harness = await CreateAsync("ana");
        await SeedAsync(harness);

        // Act
        var document = await RunAsync<TicketTimeReport>(harness);

        // Assert
        var metrics = Metrics(document);
        Assert.Equal(ReportFormat.Duration(180 * 60), metrics["Time on tickets"]);
        Assert.Equal(ReportFormat.Duration(120 * 60), metrics["Billable"]);
        Assert.Equal(ReportFormat.Duration(60 * 60), metrics["Not billed"]);
        Assert.Equal(ReportFormat.Number(2), metrics["Tickets worked on"]);
        Assert.Equal(ReportFormat.Number(2), metrics["People"]);

        var people = Rows(document, "By person");
        Assert.Equal(["Ana", "Ben"], people.Select(row => row[0]).Order());
        Assert.All(people, row => Assert.Equal(ReportFormat.Duration(90 * 60), row[1]));

        var accounts = Rows(document, "By account");
        Assert.Equal(["contoso", ReportFormat.Duration(120 * 60)], accounts[0].Take(2));
        Assert.Equal(["fabrikam", ReportFormat.Duration(60 * 60)], accounts[1].Take(2));

        var tickets = Rows(document, "Tickets with the most time");
        Assert.Equal(["a1", "a2"], tickets.Select(row => row[0]));
    }

    [Fact]
    public async Task ResolutionAndWorkTime_ComparesHowLongASolvedTicketTookToTheTimeWorkedOnIt()
    {
        // Arrange
        await using var harness = await CreateAsync("ana");
        await SeedAsync(harness);

        // Act
        var document = await RunAsync<TicketResolutionWorkReport>(harness);

        // Assert
        var metrics = Metrics(document);
        Assert.Equal(ReportFormat.Number(1), metrics["Solved"]);
        Assert.Equal(ReportFormat.Percent(1), metrics["Solved with time logged"]);
        Assert.Equal(ReportFormat.Duration(8 * 3600), metrics["Median resolution time"]);
        Assert.Equal(ReportFormat.Duration(90 * 60), metrics["Median work time"]);
        Assert.Equal(ReportFormat.Duration(120 * 60), metrics["Median effort"]);
        Assert.Equal("2", metrics["People per ticket"]);
        Assert.Equal(ReportFormat.Percent(90.0 / 480), metrics["Touch ratio"]);

        var ticket = Assert.Single(Rows(document, "Tickets that took the most effort"));
        Assert.Equal(["a1", ReportFormat.Duration(8 * 3600), ReportFormat.Duration(90 * 60), ReportFormat.Duration(120 * 60), "2"], ticket.Take(5));
    }

    [Fact]
    public async Task WorkLoggedBeforeTheTicketWasOpened_CountsInWorkTime_ButTheTouchRatioOnlyCountsItsLife()
    {
        // Arrange (Ana also worked on a1 from 1:00 to 1:30, before it was opened at 2:00)
        await using var harness = await CreateAsync("ana");
        await SeedAsync(harness, earlyWork: true);

        // Act
        var document = await RunAsync<TicketResolutionWorkReport>(harness);

        // Assert
        var metrics = Metrics(document);
        Assert.Equal(ReportFormat.Duration(120 * 60), metrics["Median work time"]);
        Assert.Equal(ReportFormat.Duration(150 * 60), metrics["Median effort"]);
        Assert.Equal(ReportFormat.Percent(90.0 / 480), metrics["Touch ratio"]);
    }

    [Fact]
    public async Task AReaderWhoMaySeeNoTicket_GetsTimeReportsSayingSo()
    {
        // Arrange
        await using var harness = await CreateAsync("stranger");
        await SeedAsync(harness);

        // Act
        var time = await RunAsync<TicketTimeReport>(harness);
        var work = await RunAsync<TicketResolutionWorkReport>(harness);

        // Assert
        Assert.All([time, work], document =>
        {
            var section = Assert.Single(document.Sections);
            Assert.Empty(section.Rows);
            Assert.NotNull(section.EmptyMessage);
        });
    }

    private static Task<CalendarTestHarness> CreateAsync(string readerId)
        => TicketReportDataSourceTests.CreateAsync(
            services =>
            {
                var accessor = new Mock<IHttpContextAccessor>();
                accessor.SetupGet(value => value.HttpContext).Returns(new DefaultHttpContext { User = TicketReportDataSourceTests.User(readerId) });
                services.AddSingleton(accessor.Object);
                services.AddSingleton(new Mock<IContentDefinitionManager>().Object);
                services.AddTimeTrackerCore();
                services.AddSingleton(new Mock<IExternalTimeClock>().Object);
                new TicketsReportsStartup().ConfigureServices(services);
                new TicketTimeReportsStartup().ConfigureServices(services);
            },
            TimeTrackerIndexSchema.CreateAsync,
            [new TimeTrackerIndexProvider(), new TimeCategoryIndexProvider(), new TimeStageIndexProvider()],
            [TimeTrackerConstants.CollectionName]);

    // a1 (support, team A, Contoso) was opened at 2:00 and solved at 10:00: Ana worked on it 2:00-3:00 (billed) and Ben
    // 2:30-3:30 (not billed). a2 (support, team A, Fabrikam) is open: Ana 8:00-8:30, and Ben from 23:30 the day before
    // to 0:30. a3 is on the HR desk, which Ana can't see.
    private static async Task SeedAsync(CalendarTestHarness harness, bool earlyWork = false)
    {
        var (billed, notBilled) = await harness.RunAsync<(string, string)>(async services =>
        {
            var stages = services.GetRequiredService<TimeStageManager>();
            var first = await stages.NewAsync();
            first.Name = "Tickets";
            await stages.CreateAsync(first);
            var second = await stages.NewAsync();
            second.Name = "Tickets, not billed";
            await stages.CreateAsync(second);

            var desks = services.GetRequiredService<ICatalog<TicketDesk>>();
            var support = (await desks.GetAllAsync()).Single(desk => desk.ItemId == "support");
            support.Put(new TicketDeskTimeSettings { StageId = first.ItemId, NotBilledStageId = second.ItemId });

            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Ticket("a1", "support", "contoso", "ana", _day.AddHours(2), solvedUtc: _day.AddHours(10)));
            await session.SaveAsync(Ticket("a2", "support", "fabrikam", "ben", _day.AddHours(5)));
            await session.SaveAsync(Ticket("a3", "hr", "contoso", "ana", _day.AddHours(1)));

            return (first.ItemId, second.ItemId);
        });

        await harness.RunAsync(async services =>
        {
            var tracker = services.GetRequiredService<ITimeTrackerService>();

            var entries = new List<(string User, string Ticket, string Stage, double From, double To)>
            {
                ("ana", "a1", billed, 2.0, 3.0),
                ("ben", "a1", notBilled, 2.5, 3.5),
                ("ana", "a2", billed, 8.0, 8.5),
                ("ben", "a2", billed, -0.5, 0.5),
                ("ana", "a3", billed, 9.0, 10.0),
            };

            if (earlyWork)
            {
                entries.Add(("ana", "a1", billed, 1.0, 1.5));
            }

            foreach (var (user, ticket, stage, from, to) in entries)
            {
                var result = await tracker.AddAsync(
                    new TimeEntry
                    {
                        UserId = user,
                        UserName = user == "ana" ? "Ana" : "Ben",
                        StageId = stage,
                        StartUtc = _day.AddHours(from),
                        EndUtc = _day.AddHours(to),
                        Source = TimeTrackerConstants.Sources.Ticket,
                        SourceRef = TicketTime.SourceRefOf(ticket),
                    },
                    user,
                    null);

                Assert.True(result.Succeeded, string.Join(" ", result.Errors));
            }

            return true;
        });
    }

    private static ContentItem Ticket(string id, string deskId, string accountId, string assigneeId, DateTime createdUtc, DateTime? solvedUtc = null)
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
            TeamId = "teamA",
            AccountId = accountId,
            AssigneeId = assigneeId,
            StatusId = "open",
            StatusCategory = solvedUtc is null ? TicketStatusCategory.Open : TicketStatusCategory.Solved,
            PriorityId = "normal",
            SolvedUtc = solvedUtc,
        });

        return item;
    }

    private static async Task<ReportDocument> RunAsync<TReport>(CalendarTestHarness harness)
        where TReport : class, IReport
    {
        await using var scope = harness.CreateScope();
        var report = ActivatorUtilities.CreateInstance<TReport>(scope.ServiceProvider);
        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, ToUtc = _day.AddDays(1).AddTicks(-1), Key = "custom" });

        return await report.RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);
    }

    private static Dictionary<string, string> Metrics(ReportDocument document)
        => document.Sections.SelectMany(section => section.Metrics).ToDictionary(metric => metric.Label, metric => metric.Value, StringComparer.Ordinal);

    private static List<IReadOnlyList<string>> Rows(ReportDocument document, string title)
        => [.. document.Sections.Single(section => section.Title == title).Rows.Select(row => (IReadOnlyList<string>)[.. row.Cells])];
}
