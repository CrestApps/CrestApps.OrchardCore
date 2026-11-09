using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Drivers;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Moq;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.Reports;

/// <summary>
/// The date range a report starts on: a report declares it on its descriptor, starts on it until a person picks a
/// range, and then takes open-ended ranges as they were picked. A report that declares none still starts on today.
/// </summary>
public sealed class ReportDefaultDateRangeTests
{
    // Wednesday 4 November 2026, 15:30, in a site whose time zone is UTC.
    private static readonly DateTime _now = new(2026, 11, 4, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime _today = new(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _endOfToday = _today.AddDays(1).AddTicks(-1);

    [Fact]
    public void EachPeriod_ResolvesItsBounds_AndNamesItsChoice()
    {
        var now = new DateTime(2026, 11, 4, 15, 30, 0);

        Assert.Equal((now.Date, now.Date.AddDays(1).AddTicks(-1)), ReportDefaultDateRange.Today.Resolve(now));
        Assert.Equal(((DateTime?)null, (DateTime?)null), ReportDefaultDateRange.AllTime.Resolve(now));
        Assert.Equal((new DateTime(2026, 10, 29), now.Date.AddDays(1).AddTicks(-1)), ReportDefaultDateRange.LastDays(7).Resolve(now));
        Assert.Equal((new DateTime(2026, 11, 2), new DateTime(2026, 11, 9).AddTicks(-1)), ReportDefaultDateRange.ThisWeek(DayOfWeek.Monday).Resolve(now));
        Assert.Equal((new DateTime(2026, 11, 1), new DateTime(2026, 11, 8).AddTicks(-1)), ReportDefaultDateRange.ThisWeek(DayOfWeek.Sunday).Resolve(now));

        Assert.Equal("today", ReportDefaultDateRange.Today.Key);
        Assert.Equal(ReportDefaultDateRange.AllTimeKey, ReportDefaultDateRange.AllTime.Key);
        Assert.Equal("last7", ReportDefaultDateRange.LastDays(7).Key);
        Assert.Equal("last90", ReportDefaultDateRange.LastDays(90).Key);
        Assert.Equal(ReportDefaultDateRange.CustomKey, ReportDefaultDateRange.LastDays(14).Key);
        Assert.Equal("thisWeek", ReportDefaultDateRange.ThisWeek().Key);
        Assert.Throws<ArgumentOutOfRangeException>(() => ReportDefaultDateRange.LastDays(0));
    }

    [Fact]
    public async Task AReportWithoutADefault_StillStartsOnToday()
    {
        var range = await Driver(defaultRange: null).NormalizeAsync("plain", null, null, null);

        Assert.Equal(_today, range.FromUtc);
        Assert.Equal(_endOfToday, range.ToUtc);
    }

    [Fact]
    public async Task AReportWithoutADefault_StillFillsAnEmptyBoundWithToday()
    {
        var before = _today.AddDays(-3);

        // "On or before" three days ago gets today's start as its lower bound and is swapped, as it always has been, so
        // a report that declares no default and reads two bounds keeps getting two.
        var range = await Driver(defaultRange: null).NormalizeAsync("plain", null, before, "prior");

        Assert.Equal(before, range.FromUtc);
        Assert.Equal(_today, range.ToUtc);
    }

    [Fact]
    public async Task AnAllTimeReport_StartsWithNeitherBound()
    {
        var range = await Driver(ReportDefaultDateRange.AllTime).NormalizeAsync("declared", null, null, null);

        Assert.Null(range.FromUtc);
        Assert.Null(range.ToUtc);
        Assert.Equal(ReportDefaultDateRange.AllTimeKey, range.Key);
    }

    [Fact]
    public async Task AReportWithADefault_TakesOnOrAfterAndOnOrBefore_AsPicked()
    {
        var day = _today.AddDays(-3);
        var driver = Driver(ReportDefaultDateRange.AllTime);

        var after = await driver.NormalizeAsync("declared", day, null, "after");
        var before = await driver.NormalizeAsync("declared", null, day, "prior");

        Assert.Equal(day, after.FromUtc);
        Assert.Null(after.ToUtc);
        Assert.Null(before.FromUtc);
        Assert.Equal(day, before.ToUtc);
        Assert.Equal("prior", before.Key);
    }

    [Fact]
    public async Task AllTime_ClearsTheBounds_WhenPicked()
    {
        var range = await Driver(ReportDefaultDateRange.LastDays(7)).NormalizeAsync("declared", _today.AddDays(-30), _today, ReportDefaultDateRange.AllTimeKey);

        Assert.Null(range.FromUtc);
        Assert.Null(range.ToUtc);
    }

    [Fact]
    public async Task OnlyAFullRange_IsSwapped_WhenInverted()
    {
        var range = await Driver(ReportDefaultDateRange.AllTime).NormalizeAsync("declared", _today, _today.AddDays(-2), "custom");

        Assert.Equal(_today.AddDays(-2), range.FromUtc);
        Assert.Equal(_today, range.ToUtc);
    }

    [Fact]
    public async Task ALast7DaysReport_StartsSixDaysBeforeToday()
    {
        var range = await Driver(ReportDefaultDateRange.LastDays(7)).NormalizeAsync("declared", null, null, null);

        Assert.Equal(_today.AddDays(-6), range.FromUtc);
        Assert.Equal(_endOfToday, range.ToUtc);
        Assert.Equal("last7", range.Key);
    }

    [Fact]
    public async Task AThisWeekReport_StartsOnMondayAndEndsOnSunday()
    {
        var range = await Driver(ReportDefaultDateRange.ThisWeek(DayOfWeek.Monday)).NormalizeAsync("declared", null, null, null);

        Assert.Equal(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1), range.ToUtc);
    }

    [Fact]
    public void TheDescriptor_CarriesTheDefaultAndTheExportPermission_OnlyWhenDeclared()
    {
        var declared = ReportDescriptor.Create<DeclaringReport>();
        var plain = ReportDescriptor.Create<PlainReport>();

        Assert.Same(DeclaringReport.DefaultDateRange, declared.DefaultDateRange);
        Assert.Same(DeclaringReport.ExportPermission, declared.ExportPermission);
        Assert.Null(plain.DefaultDateRange);
        Assert.Null(plain.ExportPermission);
    }

    private static ReportDateRangeFilterDisplayDriver Driver(ReportDefaultDateRange defaultRange)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        // The site's time zone is UTC: local and UTC times are the same.
        var localClock = new Mock<ILocalClock>();
        localClock.Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value);
        localClock.Setup(value => value.ConvertToUtcAsync(It.IsAny<DateTime>())).ReturnsAsync((DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        var reportManager = new Mock<IReportManager>();
        reportManager.Setup(value => value.GetDescriptor("plain")).Returns(ReportDescriptor.Create<PlainReport>());
        reportManager.Setup(value => value.GetDescriptor("declared")).Returns(new ReportDescriptor(
            "declared",
            PlainReport.DisplayName,
            PlainReport.Description,
            PlainReport.Category,
            PlainReport.Permission,
            typeof(PlainReport),
            _ => new PlainReport())
        {
            DefaultDateRange = defaultRange,
        });

        return new ReportDateRangeFilterDisplayDriver(clock.Object, localClock.Object, reportManager.Object);
    }

    private sealed class PlainReport : IReport
    {
        public static string Name => "plain";

        public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<PlainReport>("Plain");

        public static LocalizationSource Description { get; } = LocalizationSource.Create<PlainReport>("A report that declares nothing.");

        public static string Category => ReportsConstants.Categories.General;

        public static Permission Permission { get; } = new("ViewPlainReport", LocalizationSource.Create<PlainReport>("View plain report"));

        public Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReportDocument());
    }

    private sealed class DeclaringReport : IReport
    {
        public static string Name => "declaring";

        public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<DeclaringReport>("Declaring");

        public static LocalizationSource Description { get; } = LocalizationSource.Create<DeclaringReport>("A report that declares its default and its export permission.");

        public static string Category => ReportsConstants.Categories.General;

        public static Permission Permission { get; } = new("ViewDeclaringReport", LocalizationSource.Create<DeclaringReport>("View declaring report"));

        public static Permission ExportPermission { get; } = new("ExportDeclaringReport", LocalizationSource.Create<DeclaringReport>("Export declaring report"));

        public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

        public Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReportDocument());
    }
}
