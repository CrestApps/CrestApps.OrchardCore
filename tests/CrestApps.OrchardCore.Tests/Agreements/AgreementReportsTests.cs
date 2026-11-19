using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Agreements;

/// <summary>
/// The agreement reports: each active agreement with covered hours as of the report's last day, with its hours used,
/// left and past the cover and when they ran out or are expected to at the pace so far; and the burn-down of its hours
/// each day against an even pace.
/// </summary>
public sealed class AgreementReportsTests
{
    private const string NewYork = "America/New_York";

    [Fact]
    public void TheBurnDown_ListsEachDaysHoursLeft_AgainstAnEvenPace()
    {
        // Arrange (10 hours from 1 to 10 November: 2 hours used on the 2nd, 1 on the 4th)
        var agreement = new Agreement { TimeZoneId = NewYork, StartDate = new DateOnly(2026, 11, 1) };
        var balance = Balance(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 10), 600, (new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc), 120), (new DateTime(2026, 11, 4, 17, 0, 0, DateTimeKind.Utc), 60));

        // Act
        var days = AgreementBurnDown.Days(agreement, balance, new DateOnly(2026, 11, 5));
        var withoutEnd = AgreementBurnDown.Days(agreement, Balance(new DateOnly(2026, 11, 1), null, 600), new DateOnly(2026, 11, 2));
        var before = AgreementBurnDown.Days(agreement, balance, new DateOnly(2026, 10, 31));

        // Assert
        Assert.Equal([0d, 120, 120, 180, 180], days.Select(day => day.UsedMinutes));
        Assert.Equal([600d, 480, 480, 420, 420], days.Select(day => day.LeftMinutes));
        Assert.Equal([540d, 480, 420, 360, 300], days.Select(day => Math.Round(day.EvenLeftMinutes.Value, 6)));
        Assert.All(withoutEnd, day => Assert.Null(day.EvenLeftMinutes));
        Assert.Empty(before);
    }

    [Fact]
    public void TheRunOut_FollowsThePaceSoFar_OrIsTheDayTheHoursRanOut()
    {
        // Arrange (10 hours from 1 to 30 November)
        var agreement = new Agreement { TimeZoneId = NewYork, StartDate = new DateOnly(2026, 11, 1) };
        var start = new DateOnly(2026, 11, 1);
        var end = new DateOnly(2026, 11, 30);

        // Act (4 hours in 4 days: 10 hours by the 10th; 1 hour in 10 days lasts the period; 11 hours ran out on the 3rd)
        var fast = AgreementBurnDown.ProjectRunOut(agreement, Balance(start, end, 600, (new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc), 240)), new DateOnly(2026, 11, 4));
        var slow = AgreementBurnDown.ProjectRunOut(agreement, Balance(start, end, 600, (new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc), 60)), new DateOnly(2026, 11, 10));
        var over = AgreementBurnDown.ProjectRunOut(agreement, Balance(start, end, 600, (new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc), 300), (new DateTime(2026, 11, 3, 17, 0, 0, DateTimeKind.Utc), 360)), new DateOnly(2026, 11, 4));
        var unused = AgreementBurnDown.ProjectRunOut(agreement, Balance(start, end, 600), new DateOnly(2026, 11, 4));

        // Assert
        Assert.Equal(new AgreementRunOut(new DateOnly(2026, 11, 10), false), fast);
        Assert.Equal(new AgreementRunOut(null, false), slow);
        Assert.Equal(new AgreementRunOut(new DateOnly(2026, 11, 3), true), over);
        Assert.Equal(new AgreementRunOut(null, false), unused);
    }

    [Fact]
    public async Task AgreementUsage_ListsTheActiveAgreementsWithHours_MostUsedFirst()
    {
        // Arrange
        await using var kit = await CreateKitAsync();
        var (gold, silver) = await SeedAsync(kit);

        // Act
        var document = await RunAsync<AgreementUsageReport>(kit);

        // Assert
        var metrics = document.Sections.SelectMany(section => section.Metrics).ToDictionary(metric => metric.Label, metric => metric.Value, StringComparer.Ordinal);
        Assert.Equal("2", metrics["Agreements"]);
        Assert.Equal(AgreementUsageReport.Time(30), metrics["Overage"]);
        Assert.Equal("0", metrics["Past 80%"]);
        Assert.Equal("1", metrics["Used up"]);

        var rows = Rows(document, "Agreements");
        Assert.Equal(2, rows.Count);
        Assert.Equal(
            [silver.Number + " Silver", "Contoso", "2026-11-01 to 2026-11-30", AgreementUsageReport.Time(120), AgreementUsageReport.Time(150), AgreementUsageReport.Time(0), AgreementUsageReport.Time(30), ReportFormat.Percent(1.25), "Ran out on 2026-11-03"],
            rows[0]);
        Assert.Equal(
            [gold.Number + " Gold", "Contoso", "2026-11-01 to 2026-11-30", AgreementUsageReport.Time(600), AgreementUsageReport.Time(420), AgreementUsageReport.Time(180), AgreementUsageReport.Time(0), ReportFormat.Percent(0.7), "Expected on 2026-11-23"],
            rows[1]);
        Assert.Equal([["Test", AgreementUsageReport.Time(570), ReportFormat.Percent(1), "0"]], Rows(document, "Where the hours went"));
    }

    [Fact]
    public async Task AgreementBurnDown_DrawsTheChosenAgreementsDays_AgainstAnEvenPace()
    {
        // Arrange
        await using var kit = await CreateKitAsync();
        var (gold, _) = await SeedAsync(kit);

        // Act
        var document = await RunAsync<AgreementBurnDownReport>(kit, gold.ItemId);

        // Assert (1 to 16 November; 10 hours, 8 left after the 2nd, 3 after the 10th; an even pace leaves 4.67 on the 16th)
        var chart = Assert.Single(document.Sections, section => section.Chart is not null).Chart;
        Assert.Equal(16, chart.Labels.Count);
        Assert.Equal("2026-11-01", chart.Labels[0]);
        Assert.Equal(["Hours left", "Even pace"], chart.Datasets.Select(dataset => dataset.Label));
        Assert.Equal([10d, 8, 8, 8, 8, 8, 8, 8, 8, 3, 3, 3, 3, 3, 3, 3], chart.Datasets[0].Values);
        Assert.Equal(4.67, chart.Datasets[1].Values[^1]);
        Assert.Equal(16, Rows(document, "Days").Count);
    }

    private static Task<AgreementsTests.Kit> CreateKitAsync()
    {
        var account = new ContentItem { ContentItemId = "contoso", ContentType = "Account", DisplayText = "Contoso" };
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((IEnumerable<string> ids, VersionOptions _) => ids.Contains("contoso") ? [account] : []);

        return AgreementsTests.Kit.CreateAsync(services => services.AddSingleton(contentManager.Object));
    }

    // Gold: 10 hours from 1 to 30 November, 2 hours used on the 2nd and 5 on the 10th. Silver: an allowance of 2 hours a
    // month, 2.5 used on 3 November. A draft and an agreement without hours aren't reported.
    private static async Task<(Agreement Gold, Agreement Silver)> SeedAsync(AgreementsTests.Kit kit)
    {
        var gold = await kit.CreateAsync(item => Configure(item, "Gold", AgreementStatus.Active, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 10 }));
        var silver = await kit.CreateAsync(item => Configure(item, "Silver", AgreementStatus.Active, new DateOnly(2026, 10, 1), null, new AgreementHours { Kind = AgreementHoursKind.MonthlyAllowance, Hours = 2 }));
        var draft = await kit.CreateAsync(item => Configure(item, "Draft", AgreementStatus.Draft, new DateOnly(2026, 11, 1), null, new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 5 }));
        await kit.CreateAsync(item => Configure(item, "Visits only", AgreementStatus.Active, new DateOnly(2026, 11, 1), null, new AgreementHours { Kind = AgreementHoursKind.None }));

        var noon = AgreementsTests.Noon;
        kit.Usage.Add((gold.ItemId, noon.AddDays(-14), 120));
        kit.Usage.Add((gold.ItemId, noon.AddDays(-6), 300));
        kit.Usage.Add((silver.ItemId, noon.AddDays(-13), 150));
        kit.Usage.Add((draft.ItemId, noon.AddDays(-1), 60));

        return (gold, silver);
    }

    private static void Configure(Agreement agreement, string name, AgreementStatus status, DateOnly start, DateOnly? end, AgreementHours hours)
    {
        agreement.Name = name;
        agreement.Status = status;
        agreement.AccountId = "contoso";
        agreement.StartDate = start;
        agreement.EndDate = end;
        agreement.TimeZoneId = NewYork;
        agreement.Hours = hours;
    }

    private static Task<ReportDocument> RunAsync<TReport>(AgreementsTests.Kit kit, string agreementId = null)
        where TReport : class, IReport
        => kit.Harness.RunAsync(
            services =>
            {
                var data = ActivatorUtilities.CreateInstance<AgreementReportData>(services);
                var report = ActivatorUtilities.CreateInstance<TReport>(services, data);
                var filter = new ReportFilter();
                var noon = AgreementsTests.Noon;
                filter.SetDateRange(new ReportDateRange { FromUtc = noon.AddHours(-12), ToUtc = noon.AddHours(11), Key = "custom" });
                filter.Set(AgreementReportData.AgreementFilterKey, agreementId);

                return report.RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);
            },
            commit: false);

    private static AgreementBalance Balance(DateOnly start, DateOnly? end, double coveredMinutes, params (DateTime StartUtc, double Minutes)[] entries)
        => new()
        {
            PeriodStart = start,
            PeriodEnd = end,
            CoveredMinutes = coveredMinutes,
            UsedMinutes = entries.Sum(entry => entry.Minutes),
            Entries = [.. entries.Select(entry => (new AgreementUsage { StartUtc = entry.StartUtc, Minutes = entry.Minutes }, entry.Minutes))],
        };

    private static List<IReadOnlyList<string>> Rows(ReportDocument document, string title)
        => [.. document.Sections.Single(section => section.Title == title).Rows.Select(row => (IReadOnlyList<string>)[.. row.Cells])];
}
