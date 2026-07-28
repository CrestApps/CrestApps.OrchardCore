using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerEligibilityServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EvaluateAsync_WhenNoDestination_SuppressesNoDestination()
    {
        // Arrange
        var harness = new Harness();
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = null };

        // Act
        var result = await harness.EvaluateAsync(Profile(), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.NoDestination, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenMaxAttemptsReached_SuppressesMaxAttempts()
    {
        // Arrange
        var harness = new Harness();
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+14255551212", Attempts = 3 };

        // Act
        var result = await harness.EvaluateAsync(Profile(maxAttempts: 3), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.MaxAttemptsReached, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenWithinRetryCoolDown_SuppressesCoolDown()
    {
        // Arrange
        var harness = new Harness();
        harness.InteractionManager
            .Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "int1", EndedUtc = _now.AddMinutes(-30) });

        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+14255551212" };

        // Act
        var result = await harness.EvaluateAsync(Profile(retryDelayMinutes: 60), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.RetryCoolDown, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenContactOptedOut_SuppressesDoNotCall()
    {
        // Arrange
        var harness = new Harness();
        var contact = new ContentItem();
        contact.Apply(new OmnichannelContactPart { DoNotCall = true });

        harness.ContentManager
            .Setup(m => m.GetAsync("contact1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(contact);

        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+14255551212",
            ContactContentItemId = "contact1",
        };

        // Act
        var result = await harness.EvaluateAsync(Profile(), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.DoNotCall, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenOutsideCallingWindow_SuppressesWindow()
    {
        // Arrange
        var harness = new Harness();
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+14255551212" };
        var profile = Profile();
        profile.EnforceCallingWindow = true;
        profile.CallingCalendarId = "calendar-default";
        harness.BusinessHoursService
            .Setup(service => service.EvaluateAsync(
                "calendar-default",
                _now,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await harness.EvaluateAsync(profile, activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.OutsideCallingWindow, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenRequiredCallingCalendarIsUnavailable_SuppressesWindow()
    {
        // Arrange
        var harness = new Harness();
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+14255551212" };
        var profile = Profile();
        profile.EnforceCallingWindow = true;
        profile.CallingCalendarId = "calendar-default";
        harness.BusinessHoursService
            .Setup(service => service.EvaluateAsync(
                "calendar-default",
                _now,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool?)null);

        // Act
        var result = await harness.EvaluateAsync(profile, activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.OutsideCallingWindow, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenRegionalCalendarIsConfigured_UsesDestinationRegionAndContactTimeZone()
    {
        // Arrange
        var harness = new Harness();
        var contact = new ContentItem();
        contact.Apply(new OmnichannelContactPart { TimeZoneId = "America/Los_Angeles" });
        harness.ContentManager
            .Setup(manager => manager.GetAsync("contact1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(contact);
        harness.BusinessHoursService
            .Setup(service => service.EvaluateAsync(
                "calendar-us",
                _now,
                "America/Los_Angeles",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+14255551212",
            ContactContentItemId = "contact1",
        };
        var profile = Profile();
        profile.EnforceCallingWindow = true;
        profile.CallingCalendarId = "calendar-default";
        profile.RegionalCallingCalendarIds["US"] = "calendar-us";

        // Act
        var result = await harness.EvaluateAsync(profile, activity);

        // Assert
        Assert.True(result.IsEligible);
        harness.BusinessHoursService.VerifyAll();
    }

    [Fact]
    public async Task EvaluateAsync_WhenOnNationalRegistry_SuppressesRegistry()
    {
        // Arrange
        var harness = new Harness();

        var registry = new Mock<INationalDoNotCallRegistry>();
        registry
            .Setup(r => r.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<PhoneNumber>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([PhoneNumber.FromE164("+14255551212")]);

        harness.Registries.Add(registry.Object);

        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+14255551212" };

        // Act
        var result = await harness.EvaluateAsync(Profile(), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.NationalDoNotCallRegistry, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAbandonmentCapExceeded_SuppressesAbandonment()
    {
        // Arrange
        var harness = new Harness();
        harness.AbandonmentPolicyService
            .Setup(service => service.EvaluateAsync(It.IsAny<DialerProfile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerAbandonmentEvaluation.Suppressed(true, 5, 100, "The rolling abandonment rate of 5% exceeds the 3% cap."));

        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+14255551212" };

        // Act
        var result = await harness.EvaluateAsync(Profile(), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.AbandonmentRateExceeded, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAllChecksPass_ReturnsEligible()
    {
        // Arrange
        var harness = new Harness();
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+14255551212",
            Attempts = 1,
        };

        // Act
        var result = await harness.EvaluateAsync(Profile(retryDelayMinutes: 0), activity);

        // Assert
        Assert.True(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.None, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheDestinationIsInNationalForm_StillReachesTheRegistryAsACanonicalNumber()
    {
        // Arrange
        // This is the regression. The destination is written the way a person or an imported file writes it,
        // the profile says which country that national form belongs to, and the number is on a registry.
        // Before the destination was canonicalized once at the top, this path handed the registry a
        // digits-only string, the registry could not match it, and the call was placed.
        var harness = new Harness();

        var seen = new List<PhoneNumber>();
        var registry = new Mock<INationalDoNotCallRegistry>();
        registry
            .Setup(r => r.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<PhoneNumber>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<PhoneNumber>, CancellationToken>((numbers, _) => seen.AddRange(numbers))
            .ReturnsAsync([PhoneNumber.FromE164("+14255551212")]);

        harness.Registries.Add(registry.Object);

        var profile = Profile();
        profile.DefaultRegionCode = "US";

        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "(425) 555-1212" };

        // Act
        var result = await harness.EvaluateAsync(profile, activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.NationalDoNotCallRegistry, result.Reason);
        Assert.Equal([PhoneNumber.FromE164("+14255551212")], seen);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheDestinationCannotBeCanonicalized_SuppressesRatherThanDialing()
    {
        // Arrange
        // No region is configured, so a national-format destination cannot be resolved to a real number. The
        // question "is this number on a do-not-call registry?" has no answer, and the only safe response to
        // an unanswerable compliance question is not to place the call.
        var harness = new Harness();

        var registry = new Mock<INationalDoNotCallRegistry>();
        registry
            .Setup(r => r.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<PhoneNumber>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        harness.Registries.Add(registry.Object);

        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "(425) 555-1212" };

        // Act
        var result = await harness.EvaluateAsync(Profile(), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.NoDestination, result.Reason);
        registry.Verify(
            r => r.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<PhoneNumber>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_WhenARegistryCannotAnswer_SuppressesWithoutCancellingTheActivity()
    {
        // Arrange
        // The registry did not report that the destination is unlisted; it reported nothing at all. Treating
        // that silence as a clean result is what let a call be placed to a number nobody had screened.
        var harness = new Harness();

        var registry = new Mock<INationalDoNotCallRegistry>();
        registry
            .Setup(r => r.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<PhoneNumber>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DoNotCallScreeningException("usa-ftc", "The registry could not be reached."));

        harness.Registries.Add(registry.Object);

        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+14255551212" };

        // Act
        var result = await harness.EvaluateAsync(Profile(), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.ComplianceScreeningUnavailable, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenOneRegistryCannotAnswer_DoesNotFallBackToTheOthers()
    {
        // Arrange
        // Every configured registry was asked because each covers numbers the others do not. An answer from
        // one says nothing about the jurisdiction another one covers.
        var harness = new Harness();

        var failing = new Mock<INationalDoNotCallRegistry>();
        failing
            .Setup(r => r.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<PhoneNumber>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DoNotCallScreeningException("usa-ftc", "The registry could not be reached."));

        var clearing = new Mock<INationalDoNotCallRegistry>();
        clearing
            .Setup(r => r.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<PhoneNumber>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        harness.Registries.Add(failing.Object);
        harness.Registries.Add(clearing.Object);

        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+14255551212" };

        // Act
        var result = await harness.EvaluateAsync(Profile(), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.ComplianceScreeningUnavailable, result.Reason);
    }

    private static DialerProfile Profile(int maxAttempts = 3, int retryDelayMinutes = 0)
    {
        return new DialerProfile
        {
            ItemId = "profile1",
            Name = "Test",
            QueueId = "q1",
            MaxAttempts = maxAttempts,
            RetryDelayMinutes = retryDelayMinutes,
            RespectDoNotCall = true,
        };
    }

    private sealed class Harness
    {
        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Mock<IContentManager> ContentManager { get; } = new();

        // The real libphonenumber-backed service, not a stub. A stub that answers "yes, +14255551212" to
        // every question cannot tell whether the dialer canonicalized the destination or merely happened to
        // be handed one that was already canonical, and it was exactly that blind spot that let a
        // national-format destination reach a do-not-call lookup as bare digits.
        public IPhoneNumberService PhoneNumberService { get; } = new DefaultPhoneNumberService();

        public Mock<IBusinessHoursService> BusinessHoursService { get; } = new();

        public Mock<IDialerAbandonmentPolicyService> AbandonmentPolicyService { get; } = new();

        public List<INationalDoNotCallRegistry> Registries { get; } = [];

        public Harness()
        {
            AbandonmentPolicyService
                .Setup(service => service.EvaluateAsync(It.IsAny<DialerProfile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DialerAbandonmentEvaluation.Permitted(true, 0, 0, "Not enforced."));
        }

        public Task<DialerEligibilityResult> EvaluateAsync(DialerProfile profile, OmnichannelActivity activity)
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(_now);

            var workStateService = new FakeContactCenterWorkStateService();
            workStateService.SeedFrom(activity);

            var service = new DefaultDialerEligibilityService(
                InteractionManager.Object,
                workStateService,
                ContentManager.Object,
                PhoneNumberService,
                BusinessHoursService.Object,
                AbandonmentPolicyService.Object,
                Registries,
                clock.Object,
                NullLogger<DefaultDialerEligibilityService>.Instance);

            return service.EvaluateAsync(new DialerEligibilityContext
            {
                Profile = profile,
                Activity = activity,
            }, CancellationToken.None);
        }
    }
}
