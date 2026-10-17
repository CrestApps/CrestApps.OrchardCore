using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The campaign summary splits each campaign's pending records by what the dialer can do with them right now, through the
/// same computation as the dialer queue inventory, so the two reports agree on the same records.
/// </summary>
public sealed class CampaignSummaryDispatchSplitTests
{
    private const string RenewalsCampaign = DialerInventoryFixture.RenewalsCampaign;
    private const string WinBackCampaign = DialerInventoryFixture.WinBackCampaign;

    [Fact]
    public async Task Report_SplitsPendingIntoStatesThatAddUpToIt_AndAgreesWithTheInventory()
    {
        // Arrange
        await using var fixture = await SeedAsync();
        await using var session = fixture.Store.CreateSession();
        var inventoryService = fixture.CreateService(session);

        // Act
        var inventory = await inventoryService.GetInventoryAsync(new DialerQueueInventoryRequest(), TestContext.Current.CancellationToken);
        var document = await CreateProvider(session, inventoryService).RunAsync(new ReportContext(CreateFilter()), TestContext.Current.CancellationToken);
        var inventoryReport = await CreateInventoryProvider(inventoryService).RunAsync(
            new ReportContext(new ReportFilter { ReportName = "contact-center-dialer-queue-inventory" }),
            TestContext.Current.CancellationToken);

        // Assert
        var table = Assert.Single(document.Sections, section => section.Title == "Campaigns");

        foreach (var (campaignName, campaignId) in new[] { ("Renewals", RenewalsCampaign), ("Win-back", WinBackCampaign) })
        {
            var row = Assert.Single(table.Rows, candidate => candidate.Cells[0] == campaignName);
            var expected = Assert.Single(inventory.Campaigns, campaign => campaign.CampaignId == campaignId);

            var pending = Cell(table, row, "Pending");
            var dialable = Cell(table, row, "Dialable now");
            var heldBack = Cell(table, row, "Held back");
            var scheduled = Cell(table, row, "Scheduled for later");
            var notInAnyQueue = Cell(table, row, "Not in any queue");
            var assigned = Cell(table, row, "Assigned to a user");
            var notExplained = Cell(table, row, "Not explained");

            Assert.Equal(pending, dialable + heldBack + scheduled + notInAnyQueue + assigned + notExplained);
            Assert.Equal(expected.PendingCount, pending);
            Assert.Equal(expected.DialableNowCount, dialable);
            Assert.Equal(expected.ScheduledForLaterCount, scheduled);
            Assert.Equal(expected.NotInAnyQueueCount, notInAnyQueue);
            Assert.Equal(expected.AssignedToUserCount, assigned);
            Assert.Equal(expected.HeldBackCount, heldBack + scheduled + notInAnyQueue + assigned);
        }

        var renewals = Assert.Single(table.Rows, candidate => candidate.Cells[0] == "Renewals");
        Assert.Equal((12, 0, 8, 0, 3, 1), Split(table, renewals));
        Assert.Equal("The dialer profile is turned off (8)", Text(table, renewals, "Held back because"));
        Assert.Equal("Renewals power (8)", Text(table, renewals, "Stamped dialer profile"));
        Assert.Equal("0 (12); 2 (1)", Text(table, renewals, "Attempts distribution"));

        var winBack = Assert.Single(table.Rows, candidate => candidate.Cells[0] == "Win-back");
        Assert.Equal((7, 4, 2, 1, 0, 0), Split(table, winBack));
        Assert.Contains("The dialer profile it is stamped with no longer exists (1)", Text(table, winBack, "Held back because"), StringComparison.Ordinal);
        Assert.Contains("Out of attempts (1)", Text(table, winBack, "Held back because"), StringComparison.Ordinal);
        Assert.Equal(DialerInventoryFixture.Now.AddHours(2).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Text(table, winBack, "Next due"));
        Assert.Equal(DialerInventoryFixture.Now.AddHours(-3).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Text(table, winBack, "Last attempt"));

        var total = Assert.Single(table.Rows, candidate => candidate.Kind == ReportRowKind.GrandTotal);
        Assert.Equal((19, 4, 10, 1, 3, 1), Split(table, total));

        // The two reports read the same numbers off the same records.
        var inventorySummary = inventoryReport.Sections[0];
        Assert.Equal(Text(table, total, "Dialable now"), Metric(inventorySummary, "Dialable now"));
        Assert.Equal(Text(table, total, "Pending"), Metric(inventorySummary, "Pending"));
        Assert.Equal(Text(table, total, "Not in any queue"), Metric(inventorySummary, "Not in any queue"));
    }

    [Fact]
    public async Task Report_FilteredToAQueueState_CountsOnlyTheRecordsInIt()
    {
        // Arrange
        await using var fixture = await SeedAsync();
        await using var session = fixture.Store.CreateSession();
        var filter = CreateFilter();
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.QueueState, nameof(DialerQueueState.NotInAnyQueue));

        // Act
        var document = await CreateProvider(session, fixture.CreateService(session)).RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);

        // Assert
        var table = Assert.Single(document.Sections, section => section.Title == "Campaigns");
        var renewals = Assert.Single(table.Rows, candidate => candidate.Cells[0] == "Renewals");
        Assert.Equal(3, Cell(table, renewals, "Total"));
        Assert.Equal((3, 0, 0, 0, 3, 0), Split(table, renewals));
        Assert.DoesNotContain(table.Rows, candidate => candidate.Cells[0] == "Win-back");
    }

    // The dialer dials under the profile stamped on the queue item, so a record whose activity carries another profile is
    // still found by the profile it is dialed under.
    [Fact]
    public async Task Report_FilteredToADialerProfile_FindsTheRecordsStampedWithItToo()
    {
        // Arrange
        await using var fixture = await SeedAsync();
        await using var session = fixture.Store.CreateSession();
        var filter = CreateFilter();
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.DialerProfileId, DialerInventoryFixture.DisabledProfile);

        // Act
        var document = await CreateProvider(session, fixture.CreateService(session)).RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);

        // Assert
        var table = Assert.Single(document.Sections, section => section.Title == "Campaigns");
        var renewals = Assert.Single(table.Rows, candidate => candidate.Cells[0] == "Renewals");
        Assert.Equal(13, Cell(table, renewals, "Total"));
        Assert.Equal(12, Cell(table, renewals, "Pending"));
        Assert.Equal(8, Cell(table, renewals, "Held back"));
        Assert.DoesNotContain(table.Rows, candidate => candidate.Cells[0] == "Win-back");
    }

    [Fact]
    public void FilterActivities_AnInteractionType_KeepsOnlyThatType()
    {
        // Arrange
        OmnichannelActivityIndex[] activities =
        [
            new() { ItemId = "manual", InteractionType = ActivityInteractionType.Manual },
            new() { ItemId = "automated", InteractionType = ActivityInteractionType.Automated },
        ];

        // Act
        var filtered = ContactCenterReportingService.FilterActivities(
            activities,
            new ContactCenterReportCriteria { InteractionType = ActivityInteractionType.Automated });

        // Assert
        Assert.Equal("automated", Assert.Single(filtered).ItemId);
    }

    [Fact]
    public void AttemptsDistribution_GroupsTheHighCountsTogether()
    {
        // Arrange
        OmnichannelActivityIndex[] activities =
        [
            new() { Attempts = 0 },
            new() { Attempts = 1 },
            new() { Attempts = 1 },
            new() { Attempts = 7 },
            new() { Attempts = CampaignSummaryReportProvider.AttemptBuckets },
        ];

        // Act
        var distribution = CampaignSummaryReportProvider.AttemptsDistribution(activities);

        // Assert
        Assert.Equal([("0", 1), ("1", 2), ("5+", 2)], distribution);
    }

    private static async Task<DialerInventoryFixture> SeedAsync()
        => await DialerInventoryFixture.CreateIncidentAsync(fixture =>
        {
            // A renewals record a user works from their own list, and one that is already finished.
            fixture.AddActivity(RenewalsCampaign, DialerInventoryFixture.DisabledProfile, assignedToId: "user-1");
            fixture.AddActivity(RenewalsCampaign, DialerInventoryFixture.DisabledProfile, ActivityStatus.Completed, attempts: 2);

            // A win-back record scheduled for later, and one with no attempts left.
            var scheduled = fixture.AddActivity(WinBackCampaign, DialerInventoryFixture.PowerProfile, scheduledUtc: DialerInventoryFixture.Now.AddHours(2));
            fixture.AddQueueItem(scheduled, DialerInventoryFixture.WinBackQueue, DialerInventoryFixture.PowerProfile);

            var exhausted = fixture.AddActivity(WinBackCampaign, DialerInventoryFixture.PowerProfile, attempts: 6);
            fixture.AddQueueItem(exhausted, DialerInventoryFixture.WinBackQueue, DialerInventoryFixture.PowerProfile);
        });

    private static ReportFilter CreateFilter()
    {
        var filter = new ReportFilter { ReportName = "contact-center-campaign-summary" };
        filter.SetDateRange(new ReportDateRange
        {
            FromUtc = DialerInventoryFixture.Now.AddDays(-30),
            ToUtc = DialerInventoryFixture.Now.AddDays(1),
        });

        return filter;
    }

    private static CampaignSummaryReportProvider CreateProvider(ISession session, IDialerQueueInventoryService inventoryService)
    {
        var reportingService = new ContactCenterReportingService(
            session,
            Mock.Of<IActivityQueueGroupManager>(),
            Mock.Of<IActivityQueueManager>(),
            Mock.Of<IQueueItemManager>(),
            Mock.Of<IAgentProfileManager>(),
            CreateCampaigns().Object,
            CreateCampaignGroups().Object,
            Options.Create(new ContactCenterReportingOptions()),
            Mock.Of<IInteractionEventStore>(),
            Options.Create(new ActivitySourceOptions()));

        return new CampaignSummaryReportProvider(
            reportingService,
            CreateGuard().Object,
            new FormattingLocalizer<CampaignSummaryReportProvider>(),
            inventoryService,
            Mock.Of<ILocalClock>());
    }

    private static DialerQueueInventoryReportProvider CreateInventoryProvider(IDialerQueueInventoryService inventoryService)
        => new(
            Mock.Of<IContactCenterReportingService>(),
            CreateGuard().Object,
            new FormattingLocalizer<DialerQueueInventoryReportProvider>(),
            inventoryService,
            CreateCampaigns().Object,
            Options.Create(new ActivitySourceOptions()),
            Mock.Of<ILocalClock>());

    private static Mock<IContactCenterReportCapabilityGuard> CreateGuard()
    {
        var guard = new Mock<IContactCenterReportCapabilityGuard>();
        guard
            .Setup(value => value.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        return guard;
    }

    private static Mock<ICatalogManager<OmnichannelCampaign>> CreateCampaigns()
    {
        var campaigns = new Mock<ICatalogManager<OmnichannelCampaign>>();
        campaigns
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OmnichannelCampaign { ItemId = RenewalsCampaign, DisplayText = "Renewals" },
                new OmnichannelCampaign { ItemId = WinBackCampaign, DisplayText = "Win-back" },
            ]);

        return campaigns;
    }

    private static Mock<ICatalogManager<OmnichannelCampaignGroup>> CreateCampaignGroups()
    {
        var groups = new Mock<ICatalogManager<OmnichannelCampaignGroup>>();
        groups
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return groups;
    }

    // Pending, then where the pending records stand: dialable now, held back, scheduled for later, not in any queue and
    // assigned to a user.
    private static (int, int, int, int, int, int) Split(ReportSection table, ReportRow row)
        => (
            Cell(table, row, "Pending"),
            Cell(table, row, "Dialable now"),
            Cell(table, row, "Held back"),
            Cell(table, row, "Scheduled for later"),
            Cell(table, row, "Not in any queue"),
            Cell(table, row, "Assigned to a user"));

    private static int Cell(ReportSection table, ReportRow row, string column)
        => int.Parse(Text(table, row, column), NumberStyles.AllowThousands, CultureInfo.CurrentCulture);

    private static string Text(ReportSection table, ReportRow row, string column)
    {
        var index = table.Columns.ToList().FindIndex(candidate => candidate.Label == column);

        Assert.True(index >= 0, $"The table has no '{column}' column.");

        return row.Cells[index];
    }

    private static string Metric(ReportSection section, string label)
        => Assert.Single(section.Metrics, metric => metric.Label == label).Value;

    private sealed class FormattingLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
