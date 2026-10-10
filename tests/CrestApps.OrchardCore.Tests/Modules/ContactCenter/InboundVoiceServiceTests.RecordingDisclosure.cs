using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Whether an inbound caller on an entry point is told the call is recorded. The disclosure is said through the entry
/// point's announcement, ahead of its welcome or closed message, so a line with no welcome message still tells its
/// callers; a caller who is not put through to anyone is not told.
/// </summary>
public sealed partial class InboundVoiceServiceTests
{
    private const string Disclosure = "This call may be recorded for quality assurance and training purposes.";

    [Fact]
    public async Task AnOpenEntryPointWithNoWelcome_OwesTheDisclosure_ThenItsMenu()
    {
        // Arrange
        var (harness, interaction) = AnnouncementHarness(isOpen: true, withMenu: true);
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("entry_point_welcome", result.ReasonCode);
        AssertOwed(interaction, EntryPointAnnouncement.Welcome, EntryPointAnnouncement.NextMenu);
        Assert.True(EntryPointAnnouncement.Read(interaction).IncludesDisclosure);
        AssertNotRoutedYet(harness);
    }

    [Fact]
    public async Task AnOpenEntryPointWithNoWelcomeOrMenu_OwesTheDisclosure_ThenItsQueue()
    {
        // Arrange
        // Queueing the caller here would put them through to an agent before they had been told.
        var (harness, interaction) = AnnouncementHarness(isOpen: true);
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));
        var service = harness.CreateService();

        // Act
        await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        AssertOwed(interaction, EntryPointAnnouncement.Welcome, EntryPointAnnouncement.NextTarget);
        Assert.True(EntryPointAnnouncement.Read(interaction).IncludesDisclosure);
        AssertNotRoutedYet(harness);
    }

    [Fact]
    public async Task AnOpenEntryPointWithAWelcome_OwesTheDisclosureWithIt()
    {
        // Arrange
        var (harness, interaction) = AnnouncementHarness(isOpen: true, welcomeMessage: "Thanks for calling.", withMenu: true);
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));
        var service = harness.CreateService();

        // Act
        await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        AssertOwed(interaction, EntryPointAnnouncement.Welcome, EntryPointAnnouncement.NextMenu);
        Assert.True(EntryPointAnnouncement.Read(interaction).IncludesDisclosure);
    }

    [Theory]
    [InlineData(EntryPointClosedAction.HoldInQueue)]
    [InlineData(EntryPointClosedAction.Overflow)]
    public async Task AClosedEntryPointThatKeepsCallersWithNoClosedMessage_OwesTheDisclosure_ThenTheQueue(EntryPointClosedAction closedAction)
    {
        // Arrange
        var (harness, interaction) = AnnouncementHarness(isOpen: false, closedAction: closedAction);
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));
        var service = harness.CreateService();

        // Act
        await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        AssertOwed(interaction, EntryPointAnnouncement.Closed, EntryPointAnnouncement.NextQueue);
        Assert.True(EntryPointAnnouncement.Read(interaction).IncludesDisclosure);
    }

    [Theory]
    [InlineData(EntryPointClosedAction.Voicemail, EntryPointAnnouncement.NextVoicemail)]
    [InlineData(EntryPointClosedAction.Reject, EntryPointAnnouncement.NextReject)]
    public async Task AClosedEntryPointThatTurnsCallersAway_SaysItsClosedMessageWithoutTheDisclosure(EntryPointClosedAction closedAction, string next)
    {
        // Arrange
        // Nobody is put through to anyone, so there is no call to record.
        var (harness, interaction) = AnnouncementHarness(isOpen: false, closedMessage: "We are closed.", closedAction: closedAction);
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));
        var service = harness.CreateService();

        // Act
        await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        AssertOwed(interaction, EntryPointAnnouncement.Closed, next);
        Assert.False(EntryPointAnnouncement.Read(interaction).IncludesDisclosure);
    }

    [Fact]
    public async Task AClosedEntryPointThatSendsCallersToVoicemailWithNoClosedMessage_AppliesItsClosedActionAsBefore()
    {
        // Arrange
        var (harness, interaction) = AnnouncementHarness(isOpen: false);
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("entry_point_closed_voicemail", result.ReasonCode);
        Assert.Null(EntryPointAnnouncement.Read(interaction));
    }

    [Fact]
    public async Task ATenantThatDoesNotDiscloseOnInboundCalls_QueuesTheCallerAsBefore()
    {
        // Arrange
        // Recording is on (a provider is registered) but inbound callers are not told, so it answers with nothing.
        var (harness, interaction) = AnnouncementHarness(isOpen: true, withMenu: true);
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(null));
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ivr_menu", result.ReasonCode);
        Assert.Null(EntryPointAnnouncement.Read(interaction));
    }

    private sealed class FixedDisclosureProvider : IRecordingDisclosureProvider
    {
        private readonly string _inboundDisclosure;

        public FixedDisclosureProvider(string inboundDisclosure)
        {
            _inboundDisclosure = inboundDisclosure;
        }

        public Task<string> GetDisclosureAsync(RecordingDisclosureCallType callType, CancellationToken cancellationToken = default)
            => Task.FromResult(callType == RecordingDisclosureCallType.Inbound ? _inboundDisclosure : null);
    }
}
