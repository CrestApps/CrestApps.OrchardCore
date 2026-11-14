using System.Security.Claims;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Reports;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket reports count only the reader's tickets: a team reader's volume, backlog, response times and service
/// levels come from their teams' tickets, and a reader who may see no ticket gets a report saying so.
/// </summary>
public sealed class TicketReportsTests
{
    [Fact]
    public async Task Volume_CountsTheReadersTickets_AndTheirShareOfTheBacklog()
    {
        // Arrange
        var reader = new ReaderHolder { User = TicketReportDataSourceTests.User("ana") };
        await using var harness = await CreateAsync(reader);
        await TicketReportDataSourceTests.SeedAsync(harness);

        // Act
        var document = await RunAsync<TicketVolumeReport>(harness);

        // Assert
        var metrics = Metrics(document);
        Assert.Equal(ReportFormat.Number(3), metrics["Created"]);
        Assert.Equal(ReportFormat.Number(1), metrics["Solved"]);
        Assert.Equal(ReportFormat.Number(2), metrics["Open now"]);
        Assert.Equal(ReportFormat.Number(1), metrics["Overdue now"]);
        Assert.Equal(ReportFormat.Number(1), metrics["Unassigned now"]);

        var backlog = Assert.Single(document.Sections.Single(section => section.Title == "Backlog at the start of each day").Rows);
        Assert.Equal(["2026-11-02", ReportFormat.Number(2)], backlog.Cells.Take(2));
    }

    [Fact]
    public async Task ResponseTimes_AndServiceLevels_UseTheReadersTickets()
    {
        // Arrange
        var reader = new ReaderHolder { User = TicketReportDataSourceTests.User("ana") };
        await using var harness = await CreateAsync(reader);
        await TicketReportDataSourceTests.SeedAsync(harness);

        // Act
        var times = await RunAsync<TicketResponseTimesReport>(harness);
        var levels = await RunAsync<TicketServiceLevelReport>(harness);

        // Assert
        Assert.Equal(ReportFormat.Duration(90 * 60), Metrics(times)["Median first response"]);
        var levelMetrics = Metrics(levels);
        Assert.Equal(ReportFormat.Number(1), levelMetrics["Targets ended"]);
        Assert.Equal(ReportFormat.Number(1), levelMetrics["Breached"]);
        Assert.Equal(ReportFormat.Percent(0), levelMetrics["Met"]);
    }

    [Fact]
    public async Task AReaderWhoMaySeeNoTicket_GetsAReportSayingSo()
    {
        // Arrange
        var reader = new ReaderHolder { User = TicketReportDataSourceTests.User("stranger") };
        await using var harness = await CreateAsync(reader);
        await TicketReportDataSourceTests.SeedAsync(harness);

        // Act
        var document = await RunAsync<TicketPerformanceReport>(harness);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.Empty(section.Rows);
        Assert.NotNull(section.EmptyMessage);
    }

    private static Task<CalendarTestHarness> CreateAsync(ReaderHolder reader)
        => TicketReportDataSourceTests.CreateAsync(services =>
        {
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(value => value.HttpContext).Returns(() => new DefaultHttpContext { User = reader.User });
            services.AddSingleton(accessor.Object);
            services.AddSingleton(new Mock<IContentDefinitionManager>().Object);
            new ReportsStartup().ConfigureServices(services);
        });

    private static async Task<ReportDocument> RunAsync<TReport>(CalendarTestHarness harness)
        where TReport : class, IReport
    {
        await using var scope = harness.CreateScope();
        var report = ActivatorUtilities.CreateInstance<TReport>(scope.ServiceProvider);

        return await report.RunAsync(new ReportContext(new ReportFilter()), TestContext.Current.CancellationToken);
    }

    private static Dictionary<string, string> Metrics(ReportDocument document)
        => document.Sections.SelectMany(section => section.Metrics).ToDictionary(metric => metric.Label, metric => metric.Value, StringComparer.Ordinal);

    private sealed class ReaderHolder
    {
        public ClaimsPrincipal User { get; set; }
    }
}
