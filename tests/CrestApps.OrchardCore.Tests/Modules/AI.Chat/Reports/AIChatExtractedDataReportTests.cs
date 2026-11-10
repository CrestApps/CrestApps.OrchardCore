using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;

/// <summary>
/// The extracted data report needs an AI profile, reads only that profile's period, and gives each extracted field
/// its own column.
/// </summary>
public sealed class AIChatExtractedDataReportTests
{
    private static readonly DateTime _day = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAIChatSessionExtractedDataStore> _store = new();

    [Fact]
    public async Task WithoutAProfile_TheReportAsksForOne_AndReadsNothing()
    {
        var document = await RunAsync(Filter(profileId: null));

        var section = Assert.Single(document.Sections);
        Assert.Equal("Choose an AI profile to see the data it extracted.", section.Description);
        _store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WithAProfileAndNoRecords_TheDocumentIsEmpty()
    {
        Setup("profile-1");

        var document = await RunAsync(Filter("profile-1"));

        Assert.False(document.HasContent);
    }

    [Fact]
    public async Task WithRecords_EachFieldIsAColumn_AndTheFilterReachesTheStore()
    {
        Setup("profile-1",
            Record("s1", _day.AddHours(9), ("Email", ["ana@example.com"]), ("Topics", ["Billing", "Refund"])),
            Record("s2", _day.AddHours(10), ("Email", ["bob@example.com"])));

        var document = await RunAsync(Filter("profile-1"));

        var table = document.Sections.Single(section => section.Rows.Count > 0);
        Assert.Equal(["Session started", "Session", "Email", "Topics"], table.Columns.Select(column => column.Label));
        Assert.Equal("2026-11-02 05:00", table.Rows[0].Cells[0]);
        Assert.Equal("s2", table.Rows[0].Cells[1]);
        Assert.Equal("Billing, Refund", table.Rows[1].Cells[3]);
        _store.Verify(store => store.GetAsync("profile-1", _day, _day.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()));
    }

    private void Setup(string profileId, params AIChatSessionExtractedDataRecord[] records)
        => _store
            .Setup(store => store.GetAsync(profileId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

    [Fact]
    public async Task TheExport_IsOneFlatTable_WithTheColumnsOfTheOldCsv_AndAColumnPerField()
    {
        Setup("profile-1",
            Record("s1", _day.AddHours(9), ("Email", ["ana@example.com"]), ("Topics", ["Billing", "Refund"])),
            Record("s2", _day.AddHours(10), ("Email", ["bob@example.com"])));

        var document = await RunAsync(Filter("profile-1"), ReportRunMode.Export);

        Assert.Single(document.Sections);
        Assert.Equal(
        [
            "SessionStartedUtc,SessionId,Email,Topics",
            "2026-11-02T10:00:00.0000000Z,s2,bob@example.com,",
            "2026-11-02T09:00:00.0000000Z,s1,ana@example.com,\"Billing, Refund\"",
        ], AIChatSessionAnalyticsReportTests.CsvLines(document));
    }

    [Fact]
    public async Task TheExport_WithoutRecords_KeepsItsHeader()
    {
        Setup("profile-1");

        var document = await RunAsync(Filter("profile-1"), ReportRunMode.Export);

        Assert.Equal(["SessionStartedUtc,SessionId"], AIChatSessionAnalyticsReportTests.CsvLines(document));
    }

    [Fact]
    public async Task AnAllTimeRange_ReachesTheStore_WithoutBounds()
    {
        Setup("profile-1");

        var filter = new ReportFilter { ReportName = AIChatExtractedDataReport.Name };
        filter.SetDateRange(new ReportDateRange { Key = ReportDefaultDateRange.AllTimeKey });
        filter.Set(AIChatReportFilters.ProfileId, "profile-1");

        await RunAsync(filter);

        _store.Verify(store => store.GetAsync("profile-1", null, null, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void TheReport_StartsOnAllTime_AndExportsWithTheExportPermission()
    {
        var descriptor = ReportDescriptor.Create<AIChatExtractedDataReport>();

        Assert.Equal(ReportDateRangePeriod.AllTime, descriptor.DefaultDateRange.Period);
        Assert.Equal("ExportChatAnalytics", descriptor.ExportPermission.Name);
    }

    private Task<ReportDocument> RunAsync(ReportFilter filter, ReportRunMode mode = ReportRunMode.Display)
        => new AIChatExtractedDataReport(_store.Object, ReportTestClocks.LocalClock(TimeSpan.FromHours(-5)), new PassThroughStringLocalizer<AIChatExtractedDataReport>())
            .RunAsync(new ReportContext(filter, mode), TestContext.Current.CancellationToken);

    private static ReportFilter Filter(string profileId)
    {
        var filter = new ReportFilter { ReportName = AIChatExtractedDataReport.Name };
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, ToUtc = _day.AddDays(1).AddTicks(-1), Key = "custom" });
        filter.Set(AIChatReportFilters.ProfileId, profileId);

        return filter;
    }

    private static AIChatSessionExtractedDataRecord Record(string sessionId, DateTime startedUtc, params (string Name, string[] Values)[] values)
    {
        var record = new AIChatSessionExtractedDataRecord
        {
            SessionId = sessionId,
            ProfileId = "profile-1",
            SessionStartedUtc = startedUtc,
        };

        foreach (var (name, fieldValues) in values)
        {
            record.Values[name] = [.. fieldValues];
        }

        return record;
    }
}
