using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;

/// <summary>
/// The conversion goals report needs an AI profile, lists only that profile's scored sessions, and gives each goal
/// its own column.
/// </summary>
public sealed class AIChatConversionGoalsReportTests
{
    private static readonly DateTime _day = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryAIChatSessionEventReader _reader = new();

    [Fact]
    public async Task WithoutAProfile_TheReportAsksForOne_AndReadsNothing()
    {
        var document = await RunAsync(Filter(profileId: null));

        Assert.Equal("Choose an AI profile to see how its sessions scored.", Assert.Single(document.Sections).Description);
        Assert.Null(_reader.LastQuery);
    }

    [Fact]
    public async Task WithoutScoredSessions_TheDocumentIsEmpty()
    {
        _reader.Events.Add(new AIChatSessionEvent { SessionId = "s1", ProfileId = "profile-1", SessionStartedUtc = _day.AddHours(9) });

        var document = await RunAsync(Filter("profile-1"));

        Assert.False(document.HasContent);
    }

    [Fact]
    public async Task ScoredSessionsOfTheChosenProfile_HaveATotalAndAColumnPerGoal()
    {
        var scored = new AIChatSessionEvent { SessionId = "s1", ProfileId = "profile-1", SessionStartedUtc = _day.AddHours(9), ConversionScore = 7, ConversionMaxScore = 10 };
        scored.ConversionGoalResults.Add(new() { Name = "Booked", Score = 5, MaxScore = 5 });
        scored.ConversionGoalResults.Add(new() { Name = "Email", Score = 2, MaxScore = 5 });

        var otherProfile = new AIChatSessionEvent { SessionId = "s2", ProfileId = "profile-2", SessionStartedUtc = _day.AddHours(10), ConversionScore = 1, ConversionMaxScore = 10 };

        _reader.Events.AddRange([scored, otherProfile]);

        var document = await RunAsync(Filter("profile-1"));

        var table = document.Sections.Single(section => section.Rows.Count > 0);
        Assert.Equal(["Session started (UTC)", "Session", "Total points", "Booked", "Email"], table.Columns.Select(column => column.Label));
        var row = Assert.Single(table.Rows);
        Assert.Equal(["s1", "7 / 10", "5 / 5", "2 / 5"], row.Cells.Skip(1));
        Assert.Equal("profile-1", _reader.LastQuery.ProfileId);
    }

    [Fact]
    public async Task TheExport_IsOneFlatTable_WithTheColumnsOfTheOldCsv_AndAColumnPerGoal()
    {
        var scored = new AIChatSessionEvent { SessionId = "s1", ProfileId = "profile-1", SessionStartedUtc = _day.AddHours(9), ConversionScore = 7, ConversionMaxScore = 10 };
        scored.ConversionGoalResults.Add(new() { Name = "Email", Score = 2, MaxScore = 5 });
        scored.ConversionGoalResults.Add(new() { Name = "Booked", Score = 5, MaxScore = 5 });
        var unscored = new AIChatSessionEvent { SessionId = "s2", ProfileId = "profile-1", SessionStartedUtc = _day.AddHours(10) };
        _reader.Events.AddRange([scored, unscored]);

        var document = await RunAsync(Filter("profile-1"), ReportRunMode.Export);

        Assert.Single(document.Sections);
        Assert.Equal(
        [
            "SessionStartedUtc,SessionId,TotalPoints,Booked,Email",
            "2026-11-02T09:00:00.0000000Z,s1,7 / 10,5 / 5,2 / 5",
        ], AIChatSessionAnalyticsReportTests.CsvLines(document));
    }

    [Fact]
    public async Task TheExport_WithoutScoredSessions_KeepsItsHeader()
    {
        var document = await RunAsync(Filter("profile-1"), ReportRunMode.Export);

        Assert.Equal(["SessionStartedUtc,SessionId,TotalPoints"], AIChatSessionAnalyticsReportTests.CsvLines(document));
    }

    [Fact]
    public void TheReport_StartsOnAllTime_AndExportsWithTheExportPermission()
    {
        var descriptor = ReportDescriptor.Create<AIChatConversionGoalsReport>();

        Assert.Equal(ReportDateRangePeriod.AllTime, descriptor.DefaultDateRange.Period);
        Assert.Equal("ExportChatAnalytics", descriptor.ExportPermission.Name);
    }

    private Task<ReportDocument> RunAsync(ReportFilter filter, ReportRunMode mode = ReportRunMode.Display)
        => new AIChatConversionGoalsReport(_reader, new PassThroughStringLocalizer<AIChatConversionGoalsReport>())
            .RunAsync(new ReportContext(filter, mode), TestContext.Current.CancellationToken);

    private static ReportFilter Filter(string profileId)
    {
        var filter = new ReportFilter { ReportName = AIChatConversionGoalsReport.Name };
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, ToUtc = _day.AddDays(1).AddTicks(-1), Key = "custom" });
        filter.Set(AIChatReportFilters.ProfileId, profileId);

        return filter;
    }
}
