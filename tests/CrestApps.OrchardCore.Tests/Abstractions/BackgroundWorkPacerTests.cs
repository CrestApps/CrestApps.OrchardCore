using CrestApps.OrchardCore.BackgroundWork;

namespace CrestApps.OrchardCore.Tests.Abstractions;

public sealed class BackgroundWorkPacerTests
{
    [Fact]
    public void GetPause_WithTheDefaultShare_WaitsThreeTimesTheBatch()
    {
        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromSeconds(1), new BackgroundWorkPacingOptions());

        Assert.Equal(TimeSpan.FromSeconds(3), pause);
    }

    [Fact]
    public void GetPause_WithoutOptions_UsesTheDefaults()
    {
        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromMilliseconds(200), null);

        Assert.Equal(TimeSpan.FromMilliseconds(600), pause);
    }

    [Fact]
    public void GetPause_WithAFullShare_DoesNotWait()
    {
        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromSeconds(5), new BackgroundWorkPacingOptions { DatabaseShare = 1 });

        Assert.Equal(TimeSpan.Zero, pause);
    }

    [Fact]
    public void GetPause_NeverExceedsTheMaximum()
    {
        var options = new BackgroundWorkPacingOptions { DatabaseShare = 0.1, MaxPause = TimeSpan.FromSeconds(10) };

        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromSeconds(5), options);

        Assert.Equal(TimeSpan.FromSeconds(10), pause);
    }

    [Fact]
    public void GetPause_RaisesATooSmallShareToTheMinimum()
    {
        var pause = BackgroundWorkPacer.GetPause(TimeSpan.FromSeconds(1), new BackgroundWorkPacingOptions { DatabaseShare = 0 });

        // At the 0.05 minimum a one-second batch is followed by a 19-second pause, not an endless one.
        Assert.Equal(TimeSpan.FromSeconds(19), pause);
    }
}
