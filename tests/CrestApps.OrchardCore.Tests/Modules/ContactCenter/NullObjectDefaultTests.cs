using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins what each optional contract does when the feature that implements it is not enabled. These defaults exist
/// so a consumer can take the contract as a plain constructor parameter instead of scanning for it, which means
/// their behaviour is now the behaviour of every tenant that has not turned the owning feature on. A default that
/// quietly did the wrong thing would be worse than the scan it replaced, so each one is stated here.
/// </summary>
public sealed class NullObjectDefaultTests
{
    [Fact]
    public async Task BusinessHours_AreOpen_WhenNoCalendarFeatureIsEnabled()
    {
        // Arrange
        // Without a calendar feature there are no closing times, so gating a send on one would stop every send
        // rather than the after-hours ones.
        var gate = new AlwaysOpenBusinessHoursGate();

        // Act
        var open = await gate.IsOpenAsync("any-calendar", DateTime.UtcNow, timeZoneId: null, TestContext.Current.CancellationToken);
        var options = await gate.GetCalendarOptionsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(open);
        Assert.Empty(options);
    }

    [Fact]
    public async Task Callbacks_AreNotScheduled_WhenTheCallbackFeatureIsNotEnabled()
    {
        // Arrange
        var service = new NoCallbackService();

        // Act
        var scheduled = await service.ScheduleAsync(new CallbackRequest { ItemId = "cb1" }, TestContext.Current.CancellationToken);
        var promoted = await service.PromoteDueAsync(TestContext.Current.CancellationToken);

        // Assert
        // Nothing was stored, so nothing can be promoted. The caller is told a callback was not taken rather than
        // being handed one that will never be called back.
        Assert.Null(scheduled);
        Assert.Equal(0, promoted);
    }

    [Fact]
    public async Task WorkStateHealing_HealsNothing_WhenTheQueuesFeatureIsNotEnabled()
    {
        // Arrange
        var service = new NoAgentWorkStateHealingService();

        // Act
        var forReset = await service.HealForResetAsync("a1", TestContext.Current.CancellationToken);
        var forAvailability = await service.HealForAvailabilityAsync("a1", TestContext.Current.CancellationToken);

        // Assert
        // There are no queues to strand work in, so there is nothing to heal.
        Assert.Equal(0, forReset);
        Assert.Equal(0, forAvailability);
    }

    [Fact]
    public async Task QueuedVoiceWork_IsNotOffered_WhenTheVoiceFeatureIsNotEnabled()
    {
        // Arrange
        var service = new NoQueuedVoiceWorkOfferService();

        // Act
        var forAgent = await service.OfferForAgentAsync("a1", TestContext.Current.CancellationToken);
        var forUser = await service.OfferForUserAsync("u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, forAgent);
        Assert.Equal(0, forUser);
    }

    [Fact]
    public async Task DialerProfiles_AreAbsent_WhenTheDialerFeatureIsNotEnabled()
    {
        // Arrange
        // A caller that finds no profile falls back to the non-dialer path, which is exactly what a tenant
        // without the dialer wants. Returning an empty profile instead would apply dialer pacing to a tenant
        // that has none configured.
        var reader = new NullDialerProfileReader();

        // Act
        var profile = await reader.FindByIdAsync("d1", TestContext.Current.CancellationToken);
        var enabled = await reader.GetEnabledAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(profile);
        Assert.Empty(enabled);
    }
}
