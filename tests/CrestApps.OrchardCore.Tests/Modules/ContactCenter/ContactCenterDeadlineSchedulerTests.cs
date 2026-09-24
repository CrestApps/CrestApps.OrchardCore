using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The in-process deadline scheduler runs work when it falls due — not before, and not at the next sweep — holds one
/// deadline per key, and lets work that is not finished ask to run again without overriding a newer deadline.
/// </summary>
public sealed class ContactCenterDeadlineSchedulerTests : IDisposable
{
    private readonly TestClock _clock = new();
    private readonly ManualTimeProvider _time;
    private readonly List<DateTime> _runs = [];
    private readonly ContactCenterDeadlineScheduler _scheduler;

    public ContactCenterDeadlineSchedulerTests()
    {
        _time = new ManualTimeProvider(_clock);
        _scheduler = new ContactCenterDeadlineScheduler(_clock, _time, work => work(null), NullLogger.Instance);
    }

    [Fact]
    public async Task Schedule_RunsTheWorkWhenItFallsDue_AndNotBefore()
    {
        // Arrange
        var due = _clock.UtcNow.AddSeconds(30);
        _scheduler.Schedule("offer:1", due, Record(next: null));

        // Act
        await AdvanceAsync(TimeSpan.FromSeconds(29.9));
        var runsBeforeDue = _runs.Count;

        await AdvanceAsync(TimeSpan.FromSeconds(0.2));

        // Assert
        Assert.Equal(0, runsBeforeDue);
        var ran = Assert.Single(_runs);
        Assert.InRange(ran - due, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        Assert.Equal(0, _scheduler.Count);
    }

    [Fact]
    public async Task Schedule_WithADeadlineAlreadyPassed_RunsStraightAway()
    {
        // Arrange
        _scheduler.Schedule("offer:1", _clock.UtcNow.AddSeconds(-5), Record(next: null));

        // Act
        await AdvanceAsync(TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Single(_runs);
    }

    [Fact]
    public async Task Schedule_TheSameKeyAgain_ReplacesTheEarlierDeadline()
    {
        // Arrange
        _scheduler.Schedule("offer:1", _clock.UtcNow.AddSeconds(10), Record(next: null));
        _scheduler.Schedule("offer:1", _clock.UtcNow.AddSeconds(20), Record(next: null));

        // Act
        await AdvanceAsync(TimeSpan.FromSeconds(15));
        var runsAtFifteen = _runs.Count;

        await AdvanceAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(0, runsAtFifteen);
        Assert.Single(_runs);
    }

    [Fact]
    public async Task Cancel_DropsTheDeadline()
    {
        // Arrange
        _scheduler.Schedule("offer:1", _clock.UtcNow.AddSeconds(10), Record(next: null));

        // Act
        _scheduler.Cancel("offer:1");
        await AdvanceAsync(TimeSpan.FromSeconds(60));

        // Assert
        Assert.Empty(_runs);
        Assert.Equal(0, _time.PendingTimers);
    }

    [Fact]
    public async Task Work_ThatAsksToRunAgain_IsRunAgainAtThatTime()
    {
        // Arrange
        var again = _clock.UtcNow.AddSeconds(40);
        var calls = 0;
        _scheduler.Schedule("offer:1", _clock.UtcNow.AddSeconds(30), (_, _) =>
        {
            _runs.Add(_clock.UtcNow);

            return Task.FromResult<DateTime?>(++calls == 1 ? again : null);
        });

        // Act
        await AdvanceAsync(TimeSpan.FromSeconds(35));
        var runsAtThirtyFive = _runs.Count;

        await AdvanceAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(1, runsAtThirtyFive);
        Assert.Equal(2, _runs.Count);
        Assert.InRange(_runs[1] - again, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Work_ThatAsksToRunAgainAtOnce_WaitsAtLeastTheMinimumDelay()
    {
        // Arrange: work that keeps answering "now" must not spin.
        _scheduler.Schedule("offer:1", _clock.UtcNow, (_, _) =>
        {
            _runs.Add(_clock.UtcNow);

            return Task.FromResult<DateTime?>(_runs.Count < 3 ? _clock.UtcNow : null);
        });

        // Act
        await AdvanceAsync(TimeSpan.FromMilliseconds(500));
        var runsInHalfASecond = _runs.Count;

        await AdvanceAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, runsInHalfASecond);
        Assert.Equal(3, _runs.Count);
    }

    [Fact]
    public async Task Work_ThatAsksToRunAgain_DoesNotOverrideADeadlineScheduledWhileItRan()
    {
        // Arrange: while the work runs, something arms a newer deadline for the same key.
        var newer = _clock.UtcNow.AddSeconds(100);
        _scheduler.Schedule("offer:1", _clock.UtcNow.AddSeconds(10), (_, _) =>
        {
            _runs.Add(_clock.UtcNow);
            _scheduler.Schedule("offer:1", newer, Record(next: null));

            return Task.FromResult<DateTime?>(_clock.UtcNow.AddSeconds(20));
        });

        // Act
        await AdvanceAsync(TimeSpan.FromSeconds(50));
        var runsAtFifty = _runs.Count;

        await AdvanceAsync(TimeSpan.FromSeconds(60));

        // Assert
        Assert.Equal(1, runsAtFifty);
        Assert.Equal(2, _runs.Count);
        Assert.InRange(_runs[1] - newer, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Work_ThatThrows_DoesNotStopOtherDeadlines()
    {
        // Arrange
        _scheduler.Schedule("offer:1", _clock.UtcNow.AddSeconds(5), (_, _) => throw new InvalidOperationException("boom"));
        _scheduler.Schedule("offer:2", _clock.UtcNow.AddSeconds(10), Record(next: null));

        // Act
        await AdvanceAsync(TimeSpan.FromSeconds(20));

        // Assert
        Assert.Single(_runs);
    }

    [Fact]
    public void Schedule_FarBeyondTheLeadTime_LeavesItToTheSweep()
    {
        // Act
        _scheduler.Schedule("queue-wait:1", _clock.UtcNow.AddHours(3), Record(next: null));

        // Assert
        Assert.Equal(0, _scheduler.Count);
    }

    [Fact]
    public async Task Dispose_DropsEveryDeadline()
    {
        // Arrange
        _scheduler.Schedule("offer:1", _clock.UtcNow.AddSeconds(10), Record(next: null));

        // Act
        _scheduler.Dispose();
        await AdvanceAsync(TimeSpan.FromSeconds(60));

        // Assert
        Assert.Empty(_runs);
        Assert.Equal(0, _time.PendingTimers);
    }

    public void Dispose() => _scheduler.Dispose();

    private Task AdvanceAsync(TimeSpan by) => _time.AdvanceAsync(by, _scheduler.WhenIdleAsync);

    private Func<IServiceProvider, CancellationToken, Task<DateTime?>> Record(DateTime? next)
        => (_, _) =>
        {
            _runs.Add(_clock.UtcNow);

            return Task.FromResult(next);
        };
}
