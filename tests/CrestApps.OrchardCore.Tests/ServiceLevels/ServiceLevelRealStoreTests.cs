using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.BusinessHours.Core.Handlers;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Core;
using CrestApps.OrchardCore.ServiceLevels.Core.Handlers;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Documents;
using OrchardCore.Localization;
using YesSql;

namespace CrestApps.OrchardCore.Tests.ServiceLevels;

/// <summary>
/// Service levels on a real database and the real catalogs: an owner's change made between the monitor's list and its
/// mark wins and nobody is told, and an owner's change after the mark keeps it and the breach is told once; a schedule's new hours count from when they changed; a policy
/// is refused when its schedule is gone or never opens, when its scope is unknown or its name too long; and a policy
/// with running timers can't be deleted.
/// </summary>
public sealed class ServiceLevelRealStoreTests
{
    private static readonly RecordRef _ticket = new("Ticket", "t1");

    // Monday 2 November 2026, 12:00 UTC: the harness's clock.
    private static readonly DateTime _now = CalendarTestHarness.Now;

    [Fact]
    public async Task AnOwnerWhoCompletesTheTimer_AfterTheMonitorReadItsList_Wins_AndNobodyIsTold()
    {
        // Arrange: a one-hour resolution that started 90 minutes ago, so the monitor lists it as breached.
        await using var harness = await CreateAsync();
        var policyId = await StartOverdueTimerAsync(harness);

        // The owner completes the timer in a session of its own after the monitor read its list and before it marks.
        Interleave = () => harness.RunAsync(services => services.GetRequiredService<IServiceLevelTracker>().CompleteAsync(_ticket, ServiceLevelMetrics.Resolution));

        // Act
        var raised = await harness.RunAsync(services => services.GetRequiredService<ServiceLevelMonitor>().CheckAsync());

        // Assert
        Assert.Equal(0, raised);
        var stored = await TimerAsync(harness);
        Assert.Equal(ServiceLevelTimerState.Completed, stored.State);

        // Completed after its due time, the timer was breached: the owner's completion records it, not the monitor.
        Assert.Equal(_now.AddMinutes(-30), stored.BreachedUtc);
        Assert.NotNull(policyId);
    }

    [Fact]
    public async Task AnOwnerWhoPausesAfterTheMonitorMarkedTheBreach_KeepsTheMark_AndTheBreachIsToldOnce()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await StartOverdueTimerAsync(harness);
        var raised = await harness.RunAsync(services => services.GetRequiredService<ServiceLevelMonitor>().CheckAsync());

        // Act: the owner reads the timer after the monitor's mark committed, and pauses it.
        await harness.RunAsync(services => services.GetRequiredService<IServiceLevelTracker>().PauseAsync(_ticket));
        var again = await harness.RunAsync(services => services.GetRequiredService<ServiceLevelMonitor>().CheckAsync());

        // Assert
        Assert.Equal(1, raised);
        Assert.Equal(0, again);
        var stored = await TimerAsync(harness);
        Assert.Equal(ServiceLevelTimerState.Paused, stored.State);
        Assert.Equal(_now.AddMinutes(-30), stored.BreachedUtc);
    }

    private static Func<Task> Interleave { get; set; }

    private static async Task<string> StartOverdueTimerAsync(CalendarTestHarness harness)
    {
        var policyId = await CreatePolicyAsync(harness, scheduleId: null, minutes: 60);

        await harness.RunAsync(services => services.GetRequiredService<IServiceLevelTracker>().StartAsync(new ServiceLevelStart
        {
            Subject = _ticket,
            Metric = ServiceLevelMetrics.Resolution,
            PolicyId = policyId,
            StartUtc = _now.AddMinutes(-90),
        }));

        return policyId;
    }

    [Fact]
    public async Task ChangingAScheduleHours_CountsTheTimeSoFarOnTheOldHours_AndTheRestOnTheNewOnes()
    {
        // Arrange: weekdays 9 to 5 UTC; a four-hour resolution started today at 11:00, due 15:00.
        await using var harness = await CreateAsync();
        var scheduleId = await harness.RunAsync(async services =>
        {
            var schedules = services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>();
            var schedule = await schedules.NewAsync();
            schedule.Name = "Support";
            schedule.Hours = Weekdays(9, 17);
            await schedules.CreateAsync(schedule);

            return schedule.ItemId;
        });
        var policyId = await CreatePolicyAsync(harness, scheduleId, minutes: 240);
        await harness.RunAsync(services => services.GetRequiredService<IServiceLevelTracker>().StartAsync(new ServiceLevelStart
        {
            Subject = _ticket,
            Metric = ServiceLevelMetrics.Resolution,
            PolicyId = policyId,
            StartUtc = _now.AddHours(-1),
        }));
        Assert.Equal(_now.AddHours(3), (await TimerAsync(harness)).DueUtc);

        // Act: at 12:00 the schedule closes at 13:00 instead; the background step catches the timers up once it commits.
        await harness.RunAsync(async services =>
        {
            var schedules = services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>();
            var schedule = await schedules.FindByIdAsync(scheduleId);
            schedule.Hours = Weekdays(9, 13);
            await schedules.UpdateAsync(schedule);
        });
        var pendingBefore = await harness.RunAsync(services => services.GetRequiredService<ResumableSteps>().IsPendingAsync(ServiceLevelScheduleChangesStep.StepName), commit: false);
        var ran = await harness.RunAsync(services => services.GetRequiredService<ResumableSteps>().RunPendingAsync(services.GetServices<IResumableStep>(), NullLogger.Instance));
        var pendingAfter = await harness.RunAsync(services => services.GetRequiredService<ResumableSteps>().IsPendingAsync(ServiceLevelScheduleChangesStep.StepName), commit: false);

        // Assert: one hour used (11 to 12); three left: 12 to 13 today, then 9 to 11 tomorrow.
        var timer = await TimerAsync(harness);
        Assert.True(pendingBefore);
        Assert.Equal(1, ran);
        Assert.False(pendingAfter);
        Assert.Equal(3600, timer.UsedSeconds);
        Assert.Equal(new DateTime(2026, 11, 3, 11, 0, 0, DateTimeKind.Utc), timer.DueUtc);
    }

    [Fact]
    public async Task APolicy_IsRefused_ForAMissingOrNeverOpenSchedule_AnUnknownScope_OrALongName()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var closedId = await harness.RunAsync(async services =>
        {
            var schedules = services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>();
            var schedule = await schedules.NewAsync();
            schedule.Name = "Closed";
            schedule.Hours = new HoursSchedule { TimeZoneId = "UTC" };
            await schedules.CreateAsync(schedule);

            return schedule.ItemId;
        });

        // Act
        var errors = await harness.RunAsync(async services =>
        {
            var policies = services.GetRequiredService<IServiceLevelPolicyManager>();

            async Task<string[]> ErrorsAsync(Action<ServiceLevelPolicy> configure)
            {
                var policy = await policies.NewAsync();
                policy.Name = "Standard";
                policy.Scope = "Tickets";
                policy.Targets = [new ServiceLevelTarget { Metric = ServiceLevelMetrics.Resolution, Minutes = 60 }];
                configure(policy);

                return [.. (await policies.ValidateAsync(policy)).Errors.SelectMany(error => error.MemberNames)];
            }

            return new
            {
                Valid = await ErrorsAsync(_ => { }),
                Missing = await ErrorsAsync(policy => policy.HoursScheduleId = "4missingschedule0000000000"),
                NeverOpen = await ErrorsAsync(policy => policy.HoursScheduleId = closedId),
                UnknownScope = await ErrorsAsync(policy => policy.Scope = "Nowhere"),
                LongName = await ErrorsAsync(policy => policy.Name = new string('x', 256)),
            };
        }, commit: false);

        // Assert
        Assert.Empty(errors.Valid);
        Assert.Equal([nameof(ServiceLevelPolicy.HoursScheduleId)], errors.Missing);
        Assert.Equal([nameof(ServiceLevelPolicy.HoursScheduleId)], errors.NeverOpen);
        Assert.Equal([nameof(ServiceLevelPolicy.Scope)], errors.UnknownScope);
        Assert.Equal([nameof(ServiceLevelPolicy.Name)], errors.LongName);
    }

    [Fact]
    public async Task APolicyWithRunningTimers_IsInUse_UntilTheyStop()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var policyId = await CreatePolicyAsync(harness, scheduleId: null, minutes: 60);
        await harness.RunAsync(services => services.GetRequiredService<IServiceLevelTracker>().StartAsync(new ServiceLevelStart
        {
            Subject = _ticket,
            Metric = ServiceLevelMetrics.Resolution,
            PolicyId = policyId,
        }));

        // Act
        var whileRunning = await UsagesAsync(harness, policyId);
        await harness.RunAsync(services => services.GetRequiredService<IServiceLevelTracker>().CancelAsync(_ticket));
        var afterwards = await UsagesAsync(harness, policyId);

        // Assert
        Assert.Single(whileRunning);
        Assert.Empty(afterwards);
    }

    private static Task<IReadOnlyList<string>> UsagesAsync(CalendarTestHarness harness, string policyId)
        => harness.RunAsync(
            async services =>
            {
                var policy = await services.GetRequiredService<IServiceLevelPolicyManager>().FindByIdAsync(policyId);
                var usages = new List<string>();

                foreach (var provider in services.GetServices<IServiceLevelPolicyUsageProvider>())
                {
                    usages.AddRange(await provider.GetUsagesAsync(policy));
                }

                return (IReadOnlyList<string>)usages;
            },
            commit: false);

    private static HoursSchedule Weekdays(int opens, int closes)
        => new()
        {
            TimeZoneId = "UTC",
            Windows =
            [
                .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                    .Select(day => new HoursWindow { Day = day, OpenMinute = opens * 60, CloseMinute = closes * 60 }),
            ],
        };

    private static Task<string> CreatePolicyAsync(CalendarTestHarness harness, string scheduleId, int minutes)
        => harness.RunAsync(async services =>
        {
            var policies = services.GetRequiredService<IServiceLevelPolicyManager>();
            var policy = await policies.NewAsync();
            policy.Name = "Standard";
            policy.Scope = "Tickets";
            policy.HoursScheduleId = scheduleId;
            policy.Targets = [new ServiceLevelTarget { Metric = ServiceLevelMetrics.Resolution, Minutes = minutes }];
            await policies.CreateAsync(policy);

            return policy.ItemId;
        });

    private static Task<ServiceLevelTimer> TimerAsync(CalendarTestHarness harness)
        => harness.RunAsync(
            async services => Assert.Single(await services.GetRequiredService<IServiceLevelTracker>().GetTimersAsync(_ticket)),
            commit: false);

    // Reads the stored timer row as it is now, the way an owner's scope that read it earlier holds it.
    private static Task<(long Id, long Version, ServiceLevelTimer Timer)> ReadStoredTimerAsync(CalendarTestHarness harness)
        => harness.RunAsync(
            async services =>
            {
                var session = services.GetRequiredService<ISession>();
                var connection = await session.CreateConnectionAsync();
                var configuration = harness.Store.Configuration;
                var table = configuration.TablePrefix + configuration.TableNameConvention.GetDocumentTable(ServiceLevelsConstants.CollectionName);

                await using var command = connection.CreateCommand();
                command.Transaction = await session.BeginTransactionAsync();
                command.CommandText = $"SELECT \"Id\", \"Version\", \"Content\" FROM \"{table}\" WHERE \"Type\" LIKE '%ServiceLevelTimer%'";

                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());

                var timer = (ServiceLevelTimer)configuration.ContentSerializer.Deserialize(reader.GetString(2), typeof(ServiceLevelTimer));

                return (reader.GetInt64(0), reader.GetInt64(1), timer);
            },
            commit: false);

    private static async Task<CalendarTestHarness> CreateAsync()
        => await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddCatalogs().AddCatalogManagers();
                services.AddSingleton<CommittedDocuments>();
                services.AddScoped<IDocumentManager<DictionaryDocument<BusinessHoursSchedule>>, CommittingDocumentManager<BusinessHoursSchedule>>();
                services.AddBusinessHoursSchedules();
                services.AddServiceLevelsCore();
                services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<BusinessHoursSchedule>, BusinessHoursScheduleHandler>());
                services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ServiceLevelPolicy>, ServiceLevelPolicyHandler>());
                services.AddSingleton<IServiceLevelScopeProvider>(new TicketsScope());
                services.AddScoped<IServiceLevelTimerStore>(provider => new InterleavingTimerStore(provider.GetRequiredService<ServiceLevelTimerStore>()));
            },
            ServiceLevelsIndexSchema.CreateAsync,
            [new ServiceLevelPolicyIndexProvider(), new ServiceLevelTimerIndexProvider()],
            [ServiceLevelsConstants.CollectionName]);

    // Runs the interleaved work once, in another session, right after the monitor ends the transaction it listed in.
    private sealed class InterleavingTimerStore : IServiceLevelTimerStore
    {
        private readonly ServiceLevelTimerStore _inner;

        public InterleavingTimerStore(ServiceLevelTimerStore inner)
        {
            _inner = inner;
        }

        public ValueTask CreateAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default) => _inner.CreateAsync(timer, cancellationToken);

        public ValueTask UpdateAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default) => _inner.UpdateAsync(timer, cancellationToken);

        public Task UpdateIfUnchangedAsync(ServiceLevelTimer timer, CancellationToken cancellationToken = default) => _inner.UpdateIfUnchangedAsync(timer, cancellationToken);

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            await _inner.FlushAsync(cancellationToken);

            if (Interleave is { } interleave)
            {
                Interleave = null;
                await interleave();
            }
        }

        public Task<ServiceLevelTimer> MarkIfStillDueAsync(string timerId, Func<ServiceLevelTimer, bool> stillDue, Action<ServiceLevelTimer> mark, CancellationToken cancellationToken = default)
            => _inner.MarkIfStillDueAsync(timerId, stillDue, mark, cancellationToken);

        public Task<IReadOnlyList<ServiceLevelTimer>> ListBySubjectAsync(RecordRef subject, bool activeOnly, CancellationToken cancellationToken = default) => _inner.ListBySubjectAsync(subject, activeOnly, cancellationToken);

        public Task<IReadOnlyList<ServiceLevelTimer>> ListRunningByPoliciesAsync(IEnumerable<string> policyIds, CancellationToken cancellationToken = default) => _inner.ListRunningByPoliciesAsync(policyIds, cancellationToken);

        public Task<bool> AnyActiveAsync(string policyId, CancellationToken cancellationToken = default) => _inner.AnyActiveAsync(policyId, cancellationToken);

        public Task<IReadOnlyList<ServiceLevelTimer>> ListNewlyBreachedAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) => _inner.ListNewlyBreachedAsync(nowUtc, take, cancellationToken);

        public Task<IReadOnlyList<ServiceLevelTimer>> ListNewlyWarnedAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default) => _inner.ListNewlyWarnedAsync(nowUtc, take, cancellationToken);
    }

    private sealed class TicketsScope : IServiceLevelScopeProvider
    {
        public string Name => "Tickets";

        public LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketsScope>("Tickets");

        public Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelOption>>([]);

        public Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ServiceLevelOption>>([]);
    }
}
