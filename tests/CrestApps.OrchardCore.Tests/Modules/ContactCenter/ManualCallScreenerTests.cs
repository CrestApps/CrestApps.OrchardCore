using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ManualCallScreenerTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ScreenAsync_WhenContactOptedOut_DeniesAndRecordsAudit()
    {
        // Arrange
        var harness = new Harness();
        harness.ContactLookup
            .Setup(lookup => lookup.FindContactItemIdsAsync("+14255551212", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["contact1"]);
        var contact = new ContentItem();
        contact.Apply(new OmnichannelContactPart { DoNotCall = true });
        harness.ContentManager
            .Setup(manager => manager.GetAsync("contact1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(contact);

        // Act
        var result = await harness.ScreenAsync("+14255551212");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(DialerSuppressionReason.DoNotCall.ToString(), result.Reason);
        Assert.Single(harness.PublishedEvents);
        Assert.Equal(ContactCenterConstants.Events.ManualDialSuppressed, harness.PublishedEvents[0].EventType);
    }

    [Fact]
    public async Task ScreenAsync_WhenOnNationalRegistry_Denies()
    {
        // Arrange
        var harness = new Harness();
        var registry = new Mock<INationalDoNotCallRegistry>();
        registry
            .Setup(r => r.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<PhoneNumber>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PhoneNumber.FromE164("+14255551212")]);
        harness.Registries.Add(registry.Object);

        // Act
        var result = await harness.ScreenAsync("+14255551212");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(DialerSuppressionReason.NationalDoNotCallRegistry.ToString(), result.Reason);
    }

    [Fact]
    public async Task ScreenAsync_WhenDestinationCannotBeCanonicalized_DeniesFailClosed()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await harness.ScreenAsync("not-a-number");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(DialerSuppressionReason.NoDestination.ToString(), result.Reason);
    }

    [Fact]
    public async Task ScreenAsync_WhenOutsideCallingWindow_Denies()
    {
        // Arrange
        var harness = new Harness();
        harness.Options.EnforceCallingWindow = true;
        harness.Options.CallingCalendarId = "manual-calendar";
        harness.BusinessHoursService
            .Setup(service => service.EvaluateAsync(
                "manual-calendar",
                _now,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await harness.ScreenAsync("+14255551212");

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(DialerSuppressionReason.OutsideCallingWindow.ToString(), result.Reason);
    }

    [Fact]
    public async Task ScreenAsync_WhenNumberIsCleanAndWindowOpen_Allows()
    {
        // Arrange
        var harness = new Harness();
        harness.Options.EnforceCallingWindow = true;
        harness.Options.CallingCalendarId = "manual-calendar";
        harness.BusinessHoursService
            .Setup(service => service.EvaluateAsync(
                "manual-calendar",
                _now,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await harness.ScreenAsync("+14255551212");

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Empty(harness.PublishedEvents);
    }

    [Fact]
    public async Task ScreenAsync_WhenAllComplianceChecksDisabled_AllowsWithoutScreening()
    {
        // Arrange
        var harness = new Harness();
        harness.Options.RespectDoNotCall = false;
        harness.Options.EnforceCallingWindow = false;
        var registry = new Mock<INationalDoNotCallRegistry>(MockBehavior.Strict);
        harness.Registries.Add(registry.Object);

        // Act
        var result = await harness.ScreenAsync("+14255551212");

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ScreenAsync_WhenDestinationIsExtension_AllowsWithoutScreening()
    {
        // Arrange: an unparseable internal extension that would otherwise fail closed.
        var harness = new Harness();
        harness.Options.EnforceCallingWindow = true;
        harness.Options.CallingCalendarId = "manual-calendar";

        // Act
        var result = await harness.ScreenAsync("1001", isExtension: true);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Empty(harness.PublishedEvents);
        harness.BusinessHoursService.Verify(
            service => service.EvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DialThroughTelephonyService_WhenComplianceScreenerRegistered_BlocksADoNotCallDestinationAndRecordsAudit()
    {
        // Arrange: the real compliance screener, aggregator, and telephony service composed exactly as the
        // dial path wires them, so a do-not-call destination proves it cannot reach the provider.
        var harness = new Harness();
        harness.ContactLookup
            .Setup(lookup => lookup.FindContactItemIdsAsync("+14255551212", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["contact1"]);
        var contact = new ContentItem();
        contact.Apply(new OmnichannelContactPart { DoNotCall = true });
        harness.ContentManager
            .Setup(manager => manager.GetAsync("contact1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(contact);

        var provider = new RecordingCallControlProvider();
        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver
            .Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(provider);

        var screeningService = new DefaultOutboundCallScreeningService([harness.BuildScreener()]);
        var telephonyService = new DefaultTelephonyService(
            resolver.Object,
            screeningService,
            new PassThroughStringLocalizer<DefaultTelephonyService>());

        // Act
        var result = await telephonyService.DialAsync(new DialRequest { To = "+14255551212" }, CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(provider.DialWasCalled);
        Assert.Single(harness.PublishedEvents);
        Assert.Equal(ContactCenterConstants.Events.ManualDialSuppressed, harness.PublishedEvents[0].EventType);
    }

    private sealed class RecordingCallControlProvider : ITelephonyProvider, ITelephonyCallControlProvider
    {
        public bool DialWasCalled { get; private set; }

        public Microsoft.Extensions.Localization.LocalizedString Name => new("Recording", "Recording");

        public TelephonyCapabilities Capabilities => TelephonyCapabilities.Dial;

        public Task<TelephonyResult> DialAsync(DialRequest request, CancellationToken cancellationToken = default)
        {
            DialWasCalled = true;

            return Task.FromResult(TelephonyResult.Success());
        }

        public Task<TelephonyResult> HangupAsync(CallReference call, CancellationToken cancellationToken = default)
            => Task.FromResult(TelephonyResult.Success());
    }

    private sealed class Harness
    {
        public ManualDialingComplianceOptions Options { get; } = new();

        public IPhoneNumberService PhoneNumberService { get; } = new DefaultPhoneNumberService();

        public List<INationalDoNotCallRegistry> Registries { get; } = [];

        public Mock<IInboundContactLookup> ContactLookup { get; } = new();

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<IBusinessHoursService> BusinessHoursService { get; } = new();

        public List<InteractionEvent> PublishedEvents { get; } = [];

        public Harness()
        {
            ContactLookup
                .Setup(lookup => lookup.FindContactItemIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        public Task<OutboundCallScreeningResult> ScreenAsync(string destination, bool isExtension = false)
        {
            var screener = BuildScreener();

            return screener.ScreenAsync(
                new OutboundCallScreeningContext
                {
                    Request = new DialRequest { To = destination, IsExtension = isExtension },
                    Origin = OutboundCallOrigin.SoftPhone,
                },
                CancellationToken.None);
        }

        public ContactCenterManualCallScreener BuildScreener()
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(_now);

            var publisher = new Mock<IContactCenterEventPublisher>();
            publisher
                .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => PublishedEvents.Add(interactionEvent))
                .Returns(Task.CompletedTask);

            return new ContactCenterManualCallScreener(
                Microsoft.Extensions.Options.Options.Create(Options),
                PhoneNumberService,
                Registries,
                ContactLookup.Object,
                ContentManager.Object,
                BusinessHoursService.Object,
                publisher.Object,
                clock.Object,
                new PassThroughStringLocalizer<ContactCenterManualCallScreener>(),
                NullLogger<ContactCenterManualCallScreener>.Instance);
        }
    }
}
