using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers;
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
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222", Attempts = 3 };

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

        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" };

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
            PreferredDestination = "+15551112222",
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
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" };

        var profile = Profile();
        profile.EnforceCallingWindow = true;
        profile.CallingWindowStartHour = 13;
        profile.CallingWindowEndHour = 21;

        // Act (12:00 UTC is before the 13:00 window in the default UTC time zone)
        var result = await harness.EvaluateAsync(profile, activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.OutsideCallingWindow, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenOnNationalRegistry_SuppressesRegistry()
    {
        // Arrange
        var harness = new Harness();

        var registry = new Mock<INationalDoNotCallRegistry>();
        registry
            .Setup(r => r.GetRegisteredNumbersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["+15551112222"]);

        harness.Registries.Add(registry.Object);

        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" };

        // Act
        var result = await harness.EvaluateAsync(Profile(), activity);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.NationalDoNotCallRegistry, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAllChecksPass_ReturnsEligible()
    {
        // Arrange
        var harness = new Harness();
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+15551112222",
            Attempts = 1,
        };

        // Act
        var result = await harness.EvaluateAsync(Profile(retryDelayMinutes: 0), activity);

        // Assert
        Assert.True(result.IsEligible);
        Assert.Equal(DialerSuppressionReason.None, result.Reason);
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

        public Mock<IPhoneNumberService> PhoneNumberService { get; } = new();

        public List<INationalDoNotCallRegistry> Registries { get; } = [];

        public Harness()
        {
            var e164 = "+15551112222";
            PhoneNumberService
                .Setup(s => s.TryFormatToE164(It.IsAny<string>(), It.IsAny<string>(), out e164))
                .Returns(true);
        }

        public Task<DialerEligibilityResult> EvaluateAsync(DialerProfile profile, OmnichannelActivity activity)
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(_now);

            var service = new DefaultDialerEligibilityService(
                InteractionManager.Object,
                ContentManager.Object,
                PhoneNumberService.Object,
                Registries,
                clock.Object);

            return service.EvaluateAsync(new DialerEligibilityContext
            {
                Profile = profile,
                Activity = activity,
            }, CancellationToken.None);
        }
    }
}
