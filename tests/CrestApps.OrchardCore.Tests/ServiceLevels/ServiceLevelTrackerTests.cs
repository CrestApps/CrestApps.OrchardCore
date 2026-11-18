using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.ServiceLevels;

/// <summary>
/// A service level timer counts its target on the policy's business hours: closed hours and paused time never count,
/// a change of priority keeps the time already used, and the monitor raises each warning and breach once.
/// </summary>
public sealed class ServiceLevelTrackerTests
{
    private const string Schedule = "support";

    // Friday 6 November 2026; the schedule is open on weekdays from 9:00 to 17:00 UTC.
    private static readonly DateOnly _friday = new(2026, 11, 6);
    private static readonly DateOnly _monday = new(2026, 11, 9);
    private static readonly RecordRef _ticket = new("Ticket", "t1");

    [Fact]
    public async Task ATimer_IsDueOnBusinessHours_AndWarnsAtItsWarningPoint()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));

        // Act
        var timer = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");

        // Assert: two hours from Friday 4 pm is Monday 10 am; half of it is used up at Friday 5 pm.
        Assert.Equal(ServiceLevelTimerState.Running, timer.State);
        Assert.Equal(7200, timer.TargetSeconds);
        Assert.Equal(Utc(_monday, 10, 0), timer.DueUtc);
        Assert.Equal(Utc(_friday, 17, 0), timer.WarnAtUtc);
        Assert.Same(timer, Assert.Single(kit.Store.Timers));
    }

    [Fact]
    public async Task APolicyWithoutASchedule_CountsAroundTheClock()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        kit.Policy.HoursScheduleId = null;

        // Act
        var timer = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");

        // Assert
        Assert.Equal(Utc(_friday, 18, 0), timer.DueUtc);
    }

    [Fact]
    public async Task ThePriority_PicksItsOwnTarget_OrTheMetricsTargetForEveryPriority()
    {
        // Arrange
        var kit = new Kit(Utc(_monday, 9, 0));

        // Act
        var low = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "Low");
        var noTarget = await kit.StartAsync(ServiceLevelMetrics.NextResponse, "High");

        // Assert
        Assert.Equal(4 * 3600, low.TargetSeconds);
        Assert.Equal(Utc(_monday, 13, 0), low.DueUtc);
        Assert.Null(noTarget);
    }

    [Fact]
    public async Task PausedTime_DoesNotCount()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");

        // Act: 30 minutes used, then paused over the weekend and Monday morning.
        kit.Clock.UtcNow = Utc(_friday, 16, 30);
        var paused = Assert.Single(await kit.Tracker.PauseAsync(_ticket, cancellationToken: TestContext.Current.CancellationToken));
        var pausedState = paused.State;

        kit.Clock.UtcNow = Utc(_monday, 12, 0);
        var resumed = Assert.Single(await kit.Tracker.ResumeAsync(_ticket, cancellationToken: TestContext.Current.CancellationToken));

        // Assert: 90 minutes are left from Monday noon, and 30 minutes to the warning.
        Assert.Equal(ServiceLevelTimerState.Paused, pausedState);
        Assert.Equal(1800, resumed.UsedSeconds);
        Assert.Equal(ServiceLevelTimerState.Running, resumed.State);
        Assert.Null(resumed.PausedSinceUtc);
        Assert.Equal(Utc(_monday, 13, 30), resumed.DueUtc);
        Assert.Equal(Utc(_monday, 12, 30), resumed.WarnAtUtc);
    }

    [Fact]
    public async Task CompletingInTime_RecordsTheBusinessTimeTaken()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");

        // Act: answered Monday at 9:30, one and a half business hours after it was asked.
        kit.Clock.UtcNow = Utc(_monday, 9, 30);
        var completed = await kit.Tracker.CompleteAsync(_ticket, ServiceLevelMetrics.FirstResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceLevelTimerState.Completed, completed.State);
        Assert.Equal(5400, completed.ElapsedSeconds);
        Assert.False(completed.IsBreached);
        Assert.Equal(Utc(_monday, 9, 30), completed.EndedUtc);
        Assert.Same(completed, Assert.Single(kit.Observer.Completed));
        Assert.Null(await kit.Tracker.CompleteAsync(_ticket, ServiceLevelMetrics.FirstResponse, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompletingLate_IsABreach_EvenBeforeTheMonitorSawIt()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");

        // Act
        kit.Clock.UtcNow = Utc(_monday, 11, 0);
        var completed = await kit.Tracker.CompleteAsync(_ticket, ServiceLevelMetrics.FirstResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: it took three business hours against a target of two.
        Assert.True(completed.IsBreached);
        Assert.Equal(Utc(_monday, 10, 0), completed.BreachedUtc);
        Assert.Equal(3 * 3600, completed.ElapsedSeconds);
    }

    [Fact]
    public async Task StartingAMetricAgain_CancelsItsPreviousTimer()
    {
        // Arrange
        var kit = new Kit(Utc(_monday, 9, 0));
        var first = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");

        // Act
        kit.Clock.UtcNow = Utc(_monday, 10, 0);
        var second = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");
        var active = await kit.Tracker.GetTimersAsync(_ticket, activeOnly: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ServiceLevelTimerState.Cancelled, first.State);
        Assert.Equal(Utc(_monday, 10, 0), first.EndedUtc);
        Assert.Same(second, Assert.Single(active));
    }

    [Fact]
    public async Task ANewPriority_KeepsTheTimeAlreadyUsed()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");
        await kit.StartAsync(ServiceLevelMetrics.Resolution, "High");

        // Act: after 30 minutes the ticket becomes low priority, which has four hours to answer and no resolution target.
        kit.Clock.UtcNow = Utc(_friday, 16, 30);
        var kept = Assert.Single(await kit.Tracker.RetargetAsync(_ticket, kit.Policy.ItemId, "Low", TestContext.Current.CancellationToken));

        // Assert: three and a half hours are left; 30 minutes on Friday, three hours on Monday.
        Assert.Equal(ServiceLevelMetrics.FirstResponse, kept.Metric);
        Assert.Equal("Low", kept.Priority);
        Assert.Equal(4 * 3600, kept.TargetSeconds);
        Assert.Equal(1800, kept.UsedSeconds);
        Assert.Equal(Utc(_monday, 12, 0), kept.DueUtc);
        Assert.Equal(ServiceLevelTimerState.Cancelled, kit.Store.Timers.Single(timer => timer.Metric == ServiceLevelMetrics.Resolution).State);
    }

    [Fact]
    public async Task TheMonitor_RaisesAWarningThenABreach_Once()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        var timer = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");

        // Act
        var early = await kit.Monitor.CheckAsync(TestContext.Current.CancellationToken);

        kit.Clock.UtcNow = Utc(_friday, 17, 0);
        var atWarning = await kit.Monitor.CheckAsync(TestContext.Current.CancellationToken);
        var atWarningAgain = await kit.Monitor.CheckAsync(TestContext.Current.CancellationToken);

        kit.Clock.UtcNow = Utc(_monday, 10, 5);
        var atBreach = await kit.Monitor.CheckAsync(TestContext.Current.CancellationToken);
        var atBreachAgain = await kit.Monitor.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, early);
        Assert.Equal(1, atWarning);
        Assert.Equal(0, atWarningAgain);
        Assert.Equal(1, atBreach);
        Assert.Equal(0, atBreachAgain);
        Assert.Equal(Utc(_friday, 17, 0), timer.WarnedUtc);
        Assert.Equal(Utc(_monday, 10, 0), timer.BreachedUtc);
        Assert.Same(timer, Assert.Single(kit.Observer.Warned));
        Assert.Same(timer, Assert.Single(kit.Observer.Breached));
        Assert.Equal(ServiceLevelTimerState.Running, timer.State);
    }

    [Fact]
    public async Task TheMonitor_TellsNobody_WhenATimerChangedUnderIt()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");
        kit.Clock.UtcNow = Utc(_monday, 10, 5);
        kit.Store.ConflictOnFlush = true;

        // Act
        var raised = await kit.Monitor.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, raised);
        Assert.Empty(kit.Observer.Breached);
        Assert.Empty(kit.Observer.Warned);
    }

    [Fact]
    public async Task PausingAnOverdueTimer_RaisesItsBreach()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        var timer = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");

        // Act
        kit.Clock.UtcNow = Utc(_monday, 10, 30);
        await kit.Tracker.PauseAsync(_ticket, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Utc(_monday, 10, 0), timer.BreachedUtc);
        Assert.Same(timer, Assert.Single(kit.Observer.Breached));
        Assert.Equal(9000, timer.UsedSeconds);
    }

    [Fact]
    public async Task ATimerCopiedFromAnotherSystem_DoesNotRaiseAWarningOrBreachAlreadyRaised()
    {
        // Arrange: a timer started in the past, whose warning and breach were raised before it was copied.
        var kit = new Kit(Utc(_monday, 12, 0));

        // Act
        var timer = await kit.Tracker.StartAsync(
            new ServiceLevelStart
            {
                Subject = _ticket,
                Metric = ServiceLevelMetrics.FirstResponse,
                PolicyId = kit.Policy.ItemId,
                Priority = "High",
                StartUtc = Utc(_monday, 9, 0),
                AlreadyWarned = true,
                AlreadyBreached = true,
            },
            TestContext.Current.CancellationToken);
        var raised = await kit.Monitor.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, raised);
        Assert.Equal(Utc(_monday, 10, 0), timer.WarnedUtc);
        Assert.Equal(Utc(_monday, 11, 0), timer.BreachedUtc);
        Assert.Empty(kit.Observer.Warned);
        Assert.Empty(kit.Observer.Breached);
    }

    [Fact]
    public async Task Completing_AnswersEveryRunningCycleOfTheMetric()
    {
        // Arrange: two running cycles of one metric, as two nodes upgrading at once could leave.
        var kit = new Kit(Utc(_friday, 16, 0));
        var first = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");
        var twin = new ServiceLevelTimer
        {
            ItemId = "twin",
            Subject = first.Subject,
            Metric = first.Metric,
            Priority = first.Priority,
            PolicyId = first.PolicyId,
            HoursScheduleId = first.HoursScheduleId,
            TargetSeconds = first.TargetSeconds,
            StartedUtc = first.StartedUtc,
            CountingSinceUtc = first.CountingSinceUtc,
            DueUtc = first.DueUtc,
            WarnAtUtc = first.WarnAtUtc,
            State = ServiceLevelTimerState.Running,
        };
        await kit.Store.CreateAsync(twin, TestContext.Current.CancellationToken);

        // Act
        kit.Clock.UtcNow = Utc(_friday, 16, 30);
        await kit.Tracker.CompleteAsync(_ticket, ServiceLevelMetrics.FirstResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.All(kit.Store.Timers, timer => Assert.Equal(ServiceLevelTimerState.Completed, timer.State));
        Assert.Equal(2, kit.Observer.Completed.Count);
    }

    [Fact]
    public async Task AMetricThatWasCancelled_IsNotReopened()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 9, 0));
        await kit.StartAsync(ServiceLevelMetrics.Resolution, "High");
        await kit.Tracker.CancelAsync(_ticket, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var reopened = await kit.Tracker.ReopenAsync(_ticket, ServiceLevelMetrics.Resolution, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(reopened);
    }

    [Fact]
    public async Task AStoredDueTimeAtClosingTime_IsKeptExactly()
    {
        // Arrange: started Friday 15:00 with its due time stored as Friday 17:00, when the schedule closes.
        var kit = new Kit(Utc(_friday, 15, 0));

        // Act
        var timer = await kit.Tracker.StartAsync(
            new ServiceLevelStart
            {
                Subject = _ticket,
                Metric = ServiceLevelMetrics.Resolution,
                PolicyId = kit.Policy.ItemId,
                Priority = "High",
                StartUtc = Utc(_friday, 15, 0),
                DueUtc = Utc(_friday, 17, 0),
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Utc(_friday, 17, 0), timer.DueUtc);
        Assert.Equal(7200, timer.TargetSeconds);
    }

    [Fact]
    public async Task CompletingLate_AnnouncesTheBreach_BeforeTheCompletion()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        var timer = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");

        // Act: due Monday 10:00, answered at 11:00, before the monitor's next pass.
        kit.Clock.UtcNow = Utc(_monday, 11, 0);
        await kit.Tracker.CompleteAsync(_ticket, ServiceLevelMetrics.FirstResponse, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Utc(_monday, 10, 0), timer.BreachedUtc);
        Assert.Same(timer, Assert.Single(kit.Observer.Breached));
        Assert.Same(timer, Assert.Single(kit.Observer.Completed));
    }

    [Fact]
    public async Task Reopening_CountsOnFromTheTimeUsed_AndLeavesACountingMetricAlone()
    {
        // Arrange: four hours used of eight, then solved.
        var kit = new Kit(Utc(_friday, 9, 0));
        var timer = await kit.StartAsync(ServiceLevelMetrics.Resolution, "High");
        kit.Clock.UtcNow = Utc(_friday, 13, 0);
        await kit.Tracker.CompleteAsync(_ticket, ServiceLevelMetrics.Resolution, cancellationToken: TestContext.Current.CancellationToken);

        // Act: reopened Monday at 9:00.
        kit.Clock.UtcNow = Utc(_monday, 9, 0);
        var reopened = await kit.Tracker.ReopenAsync(_ticket, ServiceLevelMetrics.Resolution, TestContext.Current.CancellationToken);
        var again = await kit.Tracker.ReopenAsync(_ticket, ServiceLevelMetrics.Resolution, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(timer, reopened);
        Assert.Same(timer, again);
        Assert.Equal(ServiceLevelTimerState.Running, timer.State);
        Assert.Null(timer.EndedUtc);
        Assert.Equal(4 * 3600, timer.UsedSeconds);
        Assert.Equal(Utc(_monday, 13, 0), timer.DueUtc);
        Assert.Single(kit.Store.Timers);
    }

    [Fact]
    public async Task RetargetingAPausedTimer_MovesItsDueTime()
    {
        // Arrange: paused after one hour of a two-hour High target.
        var kit = new Kit(Utc(_monday, 9, 0));
        var timer = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");
        kit.Clock.UtcNow = Utc(_monday, 10, 0);
        await kit.Tracker.PauseAsync(_ticket, cancellationToken: TestContext.Current.CancellationToken);

        // Act: made Normal (four hours) while paused, at 12:00.
        kit.Clock.UtcNow = Utc(_monday, 12, 0);
        await kit.Tracker.RetargetAsync(_ticket, kit.Policy.ItemId, "Normal", TestContext.Current.CancellationToken);

        // Assert: three hours left from now.
        Assert.Equal(ServiceLevelTimerState.Paused, timer.State);
        Assert.Equal(14400, timer.TargetSeconds);
        Assert.Equal(Utc(_monday, 15, 0), timer.DueUtc);
    }

    [Fact]
    public async Task AFailingObserver_DoesNotStopTheOthers_NorTheMonitor()
    {
        // Arrange
        var kit = new Kit(Utc(_friday, 16, 0));
        var recording = new Observer();
        var monitor = new ServiceLevelMonitor(kit.Store, [new FailingObserver(), recording], kit.Clock, NullLogger<ServiceLevelMonitor>.Instance);
        var timer = await kit.StartAsync(ServiceLevelMetrics.FirstResponse, "High");
        kit.Clock.UtcNow = Utc(_monday, 10, 5);

        // Act
        var raised = await monitor.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, raised);
        Assert.Same(timer, Assert.Single(recording.Breached));
        Assert.NotNull(timer.BreachedUtc);
    }

    private static DateTime Utc(DateOnly date, int hour, int minute)
        => new(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Utc);

    private sealed class Kit
    {
        public Kit(DateTime now)
        {
            Clock = new TestClock(now);

            Policy = new ServiceLevelPolicy
            {
                ItemId = "p1",
                Name = "Standard",
                Scope = "Ticket",
                HoursScheduleId = Schedule,
                WarningPercent = 50,
                Targets =
                [
                    new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Priority = "High", Minutes = 120 },
                    new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Minutes = 240 },
                    new ServiceLevelTarget { Metric = ServiceLevelMetrics.Resolution, Priority = "High", Minutes = 480 },
                ],
            };

            var timerClock = new ServiceLevelClock(new DefaultOpenHoursService([new HoursSource()]));

            Tracker = new DefaultServiceLevelTracker(Store, new Policies(Policy), timerClock, [Observer], Clock);
            Monitor = new ServiceLevelMonitor(Store, [Observer], Clock, NullLogger<ServiceLevelMonitor>.Instance);
        }

        public TestClock Clock { get; }

        public ServiceLevelPolicy Policy { get; }

        public TimerStore Store { get; } = new();

        public Observer Observer { get; } = new();

        public DefaultServiceLevelTracker Tracker { get; }

        public ServiceLevelMonitor Monitor { get; }

        public Task<ServiceLevelTimer> StartAsync(string metric, string priority)
            => Tracker.StartAsync(
                new ServiceLevelStart { Subject = _ticket, Metric = metric, PolicyId = Policy.ItemId, Priority = priority },
                TestContext.Current.CancellationToken);
    }

    private sealed class HoursSource : IHoursScheduleSource
    {
        private static readonly HoursSchedule _weekdays = new()
        {
            Windows =
            [
                .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                    .Select(day => new HoursWindow { Day = day, OpenMinute = 9 * 60, CloseMinute = 17 * 60 }),
            ],
        };

        public Task<HoursSchedule> FindAsync(string scheduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(scheduleId == Schedule ? _weekdays : null);

        public Task<IReadOnlyList<HoursScheduleOption>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HoursScheduleOption>>([new HoursScheduleOption(Schedule, "Support")]);
    }

    private sealed class Policies : IServiceLevelPolicyLookup
    {
        private readonly ServiceLevelPolicy _policy;

        public Policies(ServiceLevelPolicy policy)
        {
            _policy = policy;
        }

        public ValueTask<ServiceLevelPolicy> FindAsync(string policyId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(policyId == _policy.ItemId ? _policy : null);
    }

    private sealed class FailingObserver : IServiceLevelObserver
    {
        public Task WarningAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Warning failed.");

        public Task BreachedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Breach failed.");

        public Task CompletedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Completion failed.");
    }

    private sealed class Observer : IServiceLevelObserver
    {
        public List<ServiceLevelTimer> Warned { get; } = [];

        public List<ServiceLevelTimer> Breached { get; } = [];

        public List<ServiceLevelTimer> Completed { get; } = [];

        public Task WarningAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        {
            Warned.Add(timer);

            return Task.CompletedTask;
        }

        public Task BreachedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        {
            Breached.Add(timer);

            return Task.CompletedTask;
        }

        public Task CompletedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        {
            Completed.Add(timer);

            return Task.CompletedTask;
        }
    }

    // Keeps timers in memory and answers the monitor's queries the way the index does.
    private sealed class TimerStore : IServiceLevelTimerStore
    {
        public List<ServiceLevelTimer> Timers { get; } = [];

        public bool ConflictOnFlush { get; set; }

        public ValueTask CreateAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
        {
            timer.ItemId ??= $"timer{Timers.Count + 1}";
            Timers.Add(timer);

            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public Task UpdateIfUnchangedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task FlushAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ServiceLevelTimer>> ListBySubjectAsync(RecordRef subject, bool activeOnly, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelTimer>>(
            [
                .. Timers
                    .Where(timer => timer.Subject == subject && (!activeOnly || timer.IsActive))
                    .OrderByDescending(timer => timer.StartedUtc),
            ]);

        public Task<IReadOnlyList<ServiceLevelTimer>> ListRunningByPoliciesAsync(IEnumerable<string> policyIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelTimer>>([.. Timers.Where(timer => timer.State == ServiceLevelTimerState.Running && policyIds.Contains(timer.PolicyId))]);

        public Task<bool> AnyActiveAsync(string policyId, CancellationToken cancellationToken = default)
            => Task.FromResult(Timers.Any(timer => timer.IsActive && timer.PolicyId == policyId));

        // Changed meanwhile, the timer isn't marked: the monitor leaves it for the next pass.
        public Task<ServiceLevelTimer> MarkIfStillDueAsync(string timerId, Func<ServiceLevelTimer, bool> stillDue, Action<ServiceLevelTimer> mark, CancellationToken cancellationToken = default)
        {
            var timer = Timers.FirstOrDefault(candidate => candidate.ItemId == timerId);

            if (ConflictOnFlush || timer is null || !stillDue(timer))
            {
                return Task.FromResult<ServiceLevelTimer>(null);
            }

            mark(timer);

            return Task.FromResult(timer);
        }

        public Task<IReadOnlyList<ServiceLevelTimer>> ListNewlyBreachedAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelTimer>>(
            [
                .. Timers
                    .Where(timer => timer.State == ServiceLevelTimerState.Running && !timer.IsBreached && timer.DueUtc <= nowUtc)
                    .OrderBy(timer => timer.DueUtc)
                    .Take(take),
            ]);

        public Task<IReadOnlyList<ServiceLevelTimer>> ListNewlyWarnedAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelTimer>>(
            [
                .. Timers
                    .Where(timer => timer.State == ServiceLevelTimerState.Running && timer.WarnedUtc is null && !timer.IsBreached && timer.WarnAtUtc <= nowUtc && timer.DueUtc > nowUtc)
                    .OrderBy(timer => timer.WarnAtUtc)
                    .Take(take),
            ]);
    }
}
