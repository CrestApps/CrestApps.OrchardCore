using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The agent's leg of a call no agent talked on measured the voicemail greeting or silence, and the report counted it
/// against the agent. These pin that only calls an agent answered are counted on the agent's side, and that a stored
/// provider leg is read the way the provider is rated now.
/// </summary>
public sealed class CallQualityReportConversationTests
{
    private static readonly DateTime _observed = new(2026, 9, 24, 23, 45, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunAsync_AgentLegsOfCallsNoAgentTalkedOn_AreNotCountedAgainstTheAgent()
    {
        // Arrange
        var answered = new Interaction
        {
            ItemId = "answered",
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            AgentId = "a1",
            CreatedUtc = _observed.AddMinutes(-3),
            AnsweredUtc = _observed.AddMinutes(-2),
        };

        var voicemail = new Interaction
        {
            ItemId = "voicemail",
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,

            // The agent the call was offered to, who did not take it.
            AgentId = "a1",
            CreatedUtc = _observed.AddMinutes(-3),
            AnsweredUtc = _observed.AddMinutes(-2),
        };
        voicemail.TechnicalMetadata[ContactCenterConstants.Voicemail.ProjectionMetadataKey] = true;

        var document = await RunAsync(
            [answered, voicemail],
            Record("r1", "answered", "leg-1", CallQualitySource.Browser, CallQualityRating.Poor, mos: 2.72),
            Record("r2", "voicemail", "leg-2", CallQualitySource.Provider, CallQualityRating.Poor, mos: 4.5));

        // Assert
        var metrics = document.Sections.SelectMany(section => section.Metrics).ToDictionary(metric => metric.Label, metric => metric.Value);

        Assert.Equal("1", metrics["Calls measured"]);
        Assert.Equal("1", metrics["Poor"]);
        Assert.Equal("1", metrics["Agent legs with no conversation (not counted)"]);
    }

    [Fact]
    public async Task RunAsync_AStoredProviderLegRatedOnSkippedPackets_IsReadAsItRatesNow()
    {
        // Arrange: an answered call whose provider agent leg was stored as poor with 100% "loss": the provider received
        // nothing from the soft phone and scored the leg 4.5.
        var answered = new Interaction
        {
            ItemId = "answered",
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            AgentId = "a1",
            CreatedUtc = _observed.AddMinutes(-3),
            AnsweredUtc = _observed.AddMinutes(-2),
        };

        var record = Record("r1", "answered", "leg-1", CallQualitySource.Provider, CallQualityRating.Poor, mos: 4.5);
        record.LossPercent = 100;
        record.Provider = new ProviderCallQualityStats
        {
            InboundMos = 4.5,
            InboundPacketCount = 0,
            InboundSkipPacketCount = 596,
            OutboundPacketCount = 572,
        };

        // Act
        var document = await RunAsync([answered], record);

        // Assert
        var metrics = document.Sections.SelectMany(section => section.Metrics).ToDictionary(metric => metric.Label, metric => metric.Value);

        Assert.Equal("1", metrics["Calls measured"]);
        Assert.Equal("0", metrics["Poor"]);
    }

    private static async Task<ReportDocument> RunAsync(Interaction[] interactions, params CallQualityRecord[] stored)
    {
        var records = new Mock<ICallQualityRecordStore>();
        records
            .Setup(store => store.GetObservedBetweenAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var interactionStore = new Mock<IInteractionStore>();
        interactionStore
            .Setup(store => store.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<string> ids, CancellationToken _) =>
                ValueTask.FromResult<IReadOnlyCollection<Interaction>>(interactions.Where(interaction => ids.Contains(interaction.ItemId)).ToArray()));

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.GetByAggregateWindowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var profiles = new Mock<IAgentProfileStore>();
        profiles
            .Setup(store => store.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentProfile { ItemId = "a1", UserId = "u1", UserName = "mike" }]);

        var userManager = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);

        var guard = new Mock<IContactCenterReportCapabilityGuard>();
        guard
            .Setup(value => value.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var localizer = new Mock<IStringLocalizer<CallQualityReportProvider>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns<string>(name => new LocalizedString(name, name));
        localizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns<string, object[]>((name, _) => new LocalizedString(name, name));

        var report = new CallQualityReportProvider(
            new Mock<IContactCenterReportingService>().Object,
            guard.Object,
            records.Object,
            interactionStore.Object,
            eventStore.Object,
            profiles.Object,
            userManager.Object,
            localizer.Object);

        var filter = new ReportFilter();
        filter.SetDateRange(new ReportDateRange { FromUtc = _observed.AddHours(-1), ToUtc = _observed.AddHours(1) });

        return await report.RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);
    }

    private static CallQualityRecord Record(
        string itemId,
        string interactionId,
        string callControlId,
        CallQualitySource source,
        CallQualityRating rating,
        double mos)
        => new()
        {
            ItemId = itemId,
            InteractionId = interactionId,
            AgentId = "a1",
            Source = source,
            LegRole = CallPartyRole.Agent,
            Rating = rating,
            ProviderCallControlId = callControlId,
            Mos = mos,
            ObservedUtc = _observed,
        };
}
