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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Documents;
using OrchardCore.Localization;
using YesSql;

namespace CrestApps.OrchardCore.Tests.ServiceLevels;

/// <summary>
/// Service levels on a real database and the real catalogs: every write of a timer moves the version the monitor's
/// checked save compares, so neither can overwrite the other; a schedule's new hours count from when they changed; a policy
/// is refused when its schedule is gone or never opens, when its scope is unknown or its name too long; and a policy
/// with running timers can't be deleted.
/// </summary>
public sealed class ServiceLevelRealStoreTests
{
    private static readonly RecordRef _ticket = new("Ticket", "t1");

    // Monday 2 November 2026, 12:00 UTC: the harness's clock.
    private static readonly DateTime _now = CalendarTestHarness.Now;

    // The monitor saves its warning and breach marks with a version check (YesSql compares the version it read). The
    // check sees an owner's change only if the owner's save moves the version, which YesSql does only for checked saves.
    // SQLite locks a read transaction until it ends, so two sessions can't interleave here; the mechanism is tested.
    [Fact]
    public async Task EveryWriteOfATimer_MovesTheVersionTheMonitorChecks()
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
        var (_, before, _) = await ReadStoredTimerAsync(harness);

        // Act
        await harness.RunAsync(services => services.GetRequiredService<IServiceLevelTracker>().PauseAsync(_ticket));
        var (_, after, _) = await ReadStoredTimerAsync(harness);

        // Assert
        Assert.True(after > before, $"The version stayed at {before}, so the monitor's check can't see the pause.");
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

        // Act: at 12:00 the schedule closes at 13:00 instead; the deferred step reschedules once the change commits.
        await harness.RunAsync(async services =>
        {
            var schedules = services.GetRequiredService<INamedCatalogManager<BusinessHoursSchedule>>();
            var schedule = await schedules.FindByIdAsync(scheduleId);
            schedule.Hours = Weekdays(9, 13);
            await schedules.UpdateAsync(schedule);
        });
        var rescheduled = await harness.RunAsync(services => services.GetRequiredService<ServiceLevelScheduleObserver>().RescheduleAsync(scheduleId));

        // Assert: one hour used (11 to 12); three left: 12 to 13 today, then 9 to 11 tomorrow.
        var timer = await TimerAsync(harness);
        Assert.Equal(1, rescheduled);
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
            },
            ServiceLevelsIndexSchema.CreateAsync,
            [new ServiceLevelPolicyIndexProvider(), new ServiceLevelTimerIndexProvider()],
            [ServiceLevelsConstants.CollectionName]);

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
