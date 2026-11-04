using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Bug: the safety hang-up after an abandoned-call message was a discarded task that waited in the background, so
/// nothing tracked it, a tenant shutdown left it running against a disposed shell, and a failure went unobserved. It is
/// now held by the tenant's deadline scheduler, which runs it in its own scope at the due time.
/// </summary>
public sealed class DialerAbandonmentSafetyHangupTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ArmSafetyHangup_SchedulesTheHangupOnTheDeadlineScheduler_AndTheWorkHangsTheCallUp()
    {
        // Arrange
        string scheduledKey = null;
        DateTime scheduledDue = default;
        Func<IServiceProvider, CancellationToken, Task<DateTime?>> scheduledWork = null;

        var scheduler = new Mock<IContactCenterDeadlineScheduler>();
        scheduler
            .Setup(service => service.Schedule(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<Func<IServiceProvider, CancellationToken, Task<DateTime?>>>()))
            .Callback((string key, DateTime due, Func<IServiceProvider, CancellationToken, Task<DateTime?>> work) =>
            {
                scheduledKey = key;
                scheduledDue = due;
                scheduledWork = work;
            });

        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(_now);

        var tracker = new DialerAbandonmentTracker(
            Mock.Of<IContactCenterEventPublisher>(),
            Mock.Of<IDialerProfileReader>(),
            Mock.Of<IQueueTreatmentProvider>(),
            Mock.Of<ICallSessionManager>(),
            Mock.Of<ISiteService>(),
            scheduler.Object,
            clock.Object,
            NullLogger<DialerAbandonmentTracker>.Instance);

        var telephony = new Mock<ITelephonyService>();
        telephony
            .Setup(service => service.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelephonyResult { Succeeded = true });

        using var services = new ServiceCollection().AddSingleton(telephony.Object).BuildServiceProvider();

        // Act
        tracker.ArmSafetyHangup("call-1", "Sorry we missed you, we will call back.");
        var next = await scheduledWork(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DialerAbandonmentTracker.SafetyHangupKeyPrefix + "call-1", scheduledKey);
        Assert.Equal(_now + DialerAbandonmentTracker.GetSafetyHangupDelay("Sorry we missed you, we will call back."), scheduledDue);
        Assert.Null(next);
        telephony.Verify(service => service.HangupAsync(It.Is<CallReference>(call => call.CallId == "call-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", 20)]
    [InlineData("one two three", 20)]
    public void GetSafetyHangupDelay_IsNeverShorterThanTheMinimum(string message, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), DialerAbandonmentTracker.GetSafetyHangupDelay(message));

    [Fact]
    public void GetSafetyHangupDelay_IsNeverLongerThanTheMaximum()
        => Assert.Equal(TimeSpan.FromMinutes(2), DialerAbandonmentTracker.GetSafetyHangupDelay(string.Join(' ', Enumerable.Repeat("word", 1000))));
}
