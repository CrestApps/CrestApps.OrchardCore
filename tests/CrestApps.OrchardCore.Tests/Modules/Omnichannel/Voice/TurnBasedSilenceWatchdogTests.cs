using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class TurnBasedSilenceWatchdogTests
{
    private const string ActivityId = "activity-1";

    [Fact]
    public void AWatch_NobodySpokeDuring_IsSilent()
    {
        // Arrange
        var watchdog = CreateWatchdog();

        // Act
        var watch = watchdog.Begin(ActivityId);

        // Assert
        Assert.True(watchdog.IsStillSilent(ActivityId, watch));
    }

    [Fact]
    public void AWatch_TheCallerWasHeardDuring_IsNotSilent()
    {
        // Arrange
        // Live, the caller's line was saved 30 ms after the watch fired, so the call was taken for a voicemail and
        // hung up on. Being heard is recorded when the words arrive, not when they are saved.
        var watchdog = CreateWatchdog();
        var watch = watchdog.Begin(ActivityId);

        // Act
        watchdog.CallerHeard(ActivityId);

        // Assert
        Assert.False(watchdog.IsStillSilent(ActivityId, watch));
    }

    [Fact]
    public void AWatch_ALaterWatchBegan_IsNotSilent()
    {
        // Arrange
        // Live, a watch begun when one line ended fired 1.3 seconds after the next line, while the caller was
        // answering it, and cut their sentence off with "are you still there?".
        var watchdog = CreateWatchdog();
        var first = watchdog.Begin(ActivityId);

        // Act
        var second = watchdog.Begin(ActivityId);

        // Assert
        Assert.False(watchdog.IsStillSilent(ActivityId, first));
        Assert.True(watchdog.IsStillSilent(ActivityId, second));
    }

    [Fact]
    public void AnotherCall_DoesNotAffectTheWatch()
    {
        // Arrange
        var watchdog = CreateWatchdog();
        var watch = watchdog.Begin(ActivityId);

        // Act
        watchdog.CallerHeard("activity-2");
        watchdog.Begin("activity-2");

        // Assert
        Assert.True(watchdog.IsStillSilent(ActivityId, watch));
    }

    [Fact]
    public void CallerHeard_CountsEachLine_AndForgetClearsTheCall()
    {
        // Arrange
        var watchdog = CreateWatchdog();

        // Act
        var first = watchdog.CallerHeard(ActivityId);
        var second = watchdog.CallerHeard(ActivityId);
        watchdog.Forget(ActivityId);

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(0, watchdog.HeardCount(ActivityId));
    }

    private static TurnBasedSilenceWatchdog CreateWatchdog()
        => new(Mock.Of<IShellHost>(), new ShellSettings(), NullLogger<TurnBasedSilenceWatchdog>.Instance);
}
