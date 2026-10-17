using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The dialer queue inventory: one row per campaign queue and stamped dialer profile, read from the indexes, with each
/// pending record classified the way the dispatcher decides it.
/// </summary>
public sealed class DialerQueueInventoryTests
{
    private const string RenewalsCampaign = DialerInventoryFixture.RenewalsCampaign;
    private const string WinBackCampaign = DialerInventoryFixture.WinBackCampaign;
    private const string DisabledProfile = DialerInventoryFixture.DisabledProfile;
    private const string PowerProfile = DialerInventoryFixture.PowerProfile;
    private const string DeletedProfile = DialerInventoryFixture.DeletedProfile;

    private static readonly string _renewalsQueue = DialerInventoryFixture.RenewalsQueue;
    private static readonly string _winBackQueue = DialerInventoryFixture.WinBackQueue;

    // The incident, scaled down: a campaign queue whose records are stamped with a turned-off profile, pending records of
    // the same campaign in no queue at all, and a second queue correctly stamped with an enabled Power profile that is
    // still not dialing because the item at its head is stamped with a profile that was deleted.
    [Fact]
    public async Task GetInventoryAsync_TheIncident_ShowsEveryCauseOnItsOwnRow()
    {
        // Arrange
        await using var fixture = await SeedIncidentAsync();
        await using var session = fixture.Store.CreateSession();

        // Act
        var inventory = await fixture.CreateService(session).GetInventoryAsync(new DialerQueueInventoryRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, inventory.Rows.Count);

        var disabled = Assert.Single(inventory.Rows, row => row.QueueId == _renewalsQueue);
        Assert.Equal(RenewalsCampaign, disabled.CampaignId);
        Assert.Equal(DisabledProfile, disabled.StampedProfileId);
        Assert.Equal("Renewals power", disabled.StampedProfileName);
        Assert.Equal(nameof(DialerMode.Power), disabled.StampedProfileMode);
        Assert.False(disabled.StampedProfileEnabled);
        Assert.Equal(8, disabled.WaitingCount);
        Assert.Equal(DialerInventoryFixture.Now.AddHours(-6), disabled.OldestWaitingUtc);
        Assert.Equal(8, disabled.PendingCount);
        Assert.Equal(0, disabled.DialableNowCount);
        Assert.Equal(8, disabled.HeldBackCount);
        var disabledReason = Assert.Single(disabled.HeldBackReasons);
        Assert.Equal(DispatchBlocker.ProfileDisabled, disabledReason.Blocker);
        Assert.Equal(8, disabledReason.Count);
        Assert.Equal(DispatchPacing.ProfileDisabled, disabled.Pacing);
        Assert.Equal(2, disabled.ProfileMismatchCount);
        Assert.Equal(0, disabled.NotInAnyQueueCount);
        Assert.Null(disabled.LastDialedUtc);
        Assert.Equal(
            [(DisabledProfile, 6), (PowerProfile, 2)],
            disabled.ActivityProfiles.Select(profile => (profile.ProfileId, profile.Count)));
        var mix = Assert.Single(disabled.ActivityMix);
        Assert.Equal((ActivitySources.PowerDial, nameof(ActivityInteractionType.Manual), "Phone", 8), (mix.Source, mix.InteractionType, mix.Channel, mix.Count));

        var notQueued = Assert.Single(inventory.Rows, row => row.CampaignId == RenewalsCampaign && row.QueueId is null);
        Assert.Equal(3, notQueued.PendingCount);
        Assert.Equal(3, notQueued.NotInAnyQueueCount);
        Assert.Equal(3, notQueued.HeldBackCount);
        Assert.Equal(DispatchBlocker.NotQueued, Assert.Single(notQueued.HeldBackReasons).Blocker);
        Assert.Null(notQueued.Pacing);

        var enabled = Assert.Single(inventory.Rows, row => row.QueueId == _winBackQueue && row.StampedProfileId == PowerProfile);
        Assert.True(enabled.StampedProfileEnabled);
        Assert.Equal(4, enabled.WaitingCount);
        Assert.Equal(1, enabled.ReservedCount);
        Assert.Equal(4, enabled.PendingCount);
        Assert.Equal(4, enabled.DialableNowCount);
        Assert.Equal(0, enabled.HeldBackCount);
        Assert.Equal(0, enabled.ProfileMismatchCount);
        Assert.Equal(DispatchPacing.ProfileMissing, enabled.Pacing);
        Assert.Equal(DialerInventoryFixture.Now.AddHours(-3), enabled.LastDialedUtc);

        var deleted = Assert.Single(inventory.Rows, row => row.QueueId == _winBackQueue && row.StampedProfileId == DeletedProfile);
        Assert.True(deleted.StampedProfileMissing);
        Assert.Null(deleted.StampedProfileEnabled);
        Assert.Equal(1, deleted.WaitingCount);
        Assert.Equal(DispatchBlocker.ProfileMissing, Assert.Single(deleted.HeldBackReasons).Blocker);
        Assert.Equal(1, deleted.ProfileMismatchCount);
        Assert.Equal(DispatchPacing.ProfileMissing, deleted.Pacing);

        var renewals = Assert.Single(inventory.Campaigns, campaign => campaign.CampaignId == RenewalsCampaign);
        Assert.Equal((11, 0, 11, 3, 0), (renewals.PendingCount, renewals.DialableNowCount, renewals.HeldBackCount, renewals.NotInAnyQueueCount, renewals.NotEvaluatedCount));

        var winBack = Assert.Single(inventory.Campaigns, campaign => campaign.CampaignId == WinBackCampaign);
        Assert.Equal((5, 4, 1), (winBack.PendingCount, winBack.DialableNowCount, winBack.HeldBackCount));
    }

    // The acceptance of the issue: a supervisor reads the incident from the report, on one screen, without logs or queries.
    [Fact]
    public async Task Report_TheIncident_IsVisibleOnOneScreen()
    {
        // Arrange
        await using var fixture = await SeedIncidentAsync();
        await using var session = fixture.Store.CreateSession();
        var provider = CreateProvider(fixture.CreateService(session));

        // Act
        var document = await provider.RunAsync(new ReportContext(new ReportFilter { ReportName = DialerQueueInventoryReportProvider.Name }), TestContext.Current.CancellationToken);

        // Assert
        var summary = document.Sections[0];
        Assert.Equal("16", Metric(summary, "Pending"));
        Assert.Equal("12", Metric(summary, "Held back"));
        Assert.Equal("4", Metric(summary, "Dialable now"));
        Assert.Equal("3", Metric(summary, "Not in any queue"));

        var table = Assert.Single(document.Sections, section => section.Title == "Campaign queues by stamped dialer profile");
        var flagsColumn = IndexOf(table, "Flags");
        var pacingColumn = IndexOf(table, "Queue pacing");
        var reasonColumn = IndexOf(table, "Held back because");
        var stampColumn = IndexOf(table, "Stamped dialer profile");
        var onColumn = IndexOf(table, "Profile on");

        var renewals = Assert.Single(table.Rows, row => row.Cells[stampColumn] == "Renewals power");
        Assert.Equal("No", renewals.Cells[onColumn]);
        Assert.Contains("The stamped dialer profile is turned off", renewals.Cells[flagsColumn], StringComparison.Ordinal);
        Assert.Contains("2 stamped differently from their activity", renewals.Cells[flagsColumn], StringComparison.Ordinal);
        Assert.Contains("The dialer profile is turned off (8)", renewals.Cells[reasonColumn], StringComparison.Ordinal);
        Assert.Equal("Not paced: the dialer profile at the head of the queue is turned off", renewals.Cells[pacingColumn]);

        var notQueued = Assert.Single(table.Rows, row => row.Cells[IndexOf(table, "Queue")] == "Not in the campaign queue");
        Assert.Contains("3 pending activities in no queue", notQueued.Cells[flagsColumn], StringComparison.Ordinal);

        var power = Assert.Single(table.Rows, row => row.Cells[stampColumn] == "Win-back power");
        Assert.Equal("Yes", power.Cells[onColumn]);
        Assert.Equal("4", power.Cells[IndexOf(table, "Dialable now")]);
        Assert.Equal(
            "Not paced: the item at the head of the queue has no dialer profile, or its profile no longer exists",
            power.Cells[pacingColumn]);

        var deleted = Assert.Single(table.Rows, row => row.Cells[stampColumn] == "(No longer exists)");
        Assert.Contains("The stamped dialer profile no longer exists", deleted.Cells[flagsColumn], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetInventoryAsync_ForOneCampaign_LeavesTheOthersOut()
    {
        // Arrange
        await using var fixture = await SeedIncidentAsync();
        await using var session = fixture.Store.CreateSession();

        // Act
        var inventory = await fixture.CreateService(session).GetInventoryAsync(
            new DialerQueueInventoryRequest { CampaignIds = [WinBackCampaign] },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.All(inventory.Rows, row => Assert.Equal(WinBackCampaign, row.CampaignId));
        Assert.Equal(WinBackCampaign, Assert.Single(inventory.Campaigns).CampaignId);
    }

    [Fact]
    public void ResolveCampaignIds_ACampaignGroup_NarrowsToItsCampaigns()
    {
        // Arrange
        OmnichannelCampaign[] campaigns =
        [
            new() { ItemId = "a", CampaignGroupId = "group-1" },
            new() { ItemId = "b", CampaignGroupId = "group-2" },
            new() { ItemId = "c", CampaignGroupId = "group-1" },
        ];

        // Act
        var all = DialerQueueInventoryReportProvider.ResolveCampaignIds(null, null, campaigns);
        var group = DialerQueueInventoryReportProvider.ResolveCampaignIds(null, "group-1", campaigns);
        var one = DialerQueueInventoryReportProvider.ResolveCampaignIds("b", null, campaigns);

        // Assert
        Assert.Null(all);
        Assert.Equal(["a", "c"], group);
        Assert.Equal(["b"], one);
    }

    private static Task<DialerInventoryFixture> SeedIncidentAsync()
        => DialerInventoryFixture.CreateIncidentAsync();

    private static DialerQueueInventoryReportProvider CreateProvider(IDialerQueueInventoryService service)
    {
        var guard = new Mock<IContactCenterReportCapabilityGuard>();
        guard
            .Setup(value => value.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var campaigns = new Mock<ICatalogManager<OmnichannelCampaign>>();
        campaigns
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OmnichannelCampaign { ItemId = RenewalsCampaign, DisplayText = "Renewals" },
                new OmnichannelCampaign { ItemId = WinBackCampaign, DisplayText = "Win-back" },
            ]);

        return new DialerQueueInventoryReportProvider(
            Mock.Of<IContactCenterReportingService>(),
            guard.Object,
            new FormattingLocalizer<DialerQueueInventoryReportProvider>(),
            service,
            campaigns.Object,
            Options.Create(new ActivitySourceOptions()),
            Mock.Of<ILocalClock>());
    }

    private static string Metric(ReportSection section, string label)
        => Assert.Single(section.Metrics, metric => metric.Label == label).Value;

    private static int IndexOf(ReportSection table, string label)
    {
        var index = table.Columns.ToList().FindIndex(column => column.Label == label);

        Assert.True(index >= 0, $"The table has no '{label}' column.");

        return index;
    }

    private sealed class FormattingLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
