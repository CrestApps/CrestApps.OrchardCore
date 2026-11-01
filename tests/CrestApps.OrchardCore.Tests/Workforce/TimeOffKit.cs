using System.Collections.Concurrent;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Shared setup for the time-off tests: a harness with the time-off services and a real lock, types, policies,
/// balances, allotments and requests.
/// </summary>
internal static class TimeOffKit
{
    public static Task<CalendarTestHarness> CreateAsync()
    {
        return WorkforceTests.CreateAsync(services =>
        {
            services.AddWorkforceTimeOffCore();
            services.AddSingleton<IDistributedLock, KeyedLock>();
        });
    }

    public static Task<TimeOffType> AddTypeAsync(CalendarTestHarness harness, string name, Action<TimeOffType> configure = null)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITimeOffTypeManager>();
            var type = await manager.NewAsync();

            type.Name = name;
            configure?.Invoke(type);
            await manager.CreateAsync(type);

            return type;
        });
    }

    public static Task<TimeOffPolicy> AddPolicyAsync(CalendarTestHarness harness, TimeOffType type, WorkforceTeam team, Action<TimeOffPolicy> configure = null)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITimeOffPolicyManager>();
            var policy = await manager.NewAsync();

            policy.Name = type.Name;
            policy.TypeId = type.ItemId;
            policy.AccrualMethod = TimeOffAccrualMethod.None;
            policy.Assignments.Add(new TimeOffPolicyAssignment { TeamId = team.ItemId });
            configure?.Invoke(policy);

            var validation = await manager.ValidateAsync(policy);

            Assert.True(validation.Succeeded, string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(policy);

            return policy;
        });
    }

    public static Task<TimeOffLedgerEntry> OpenBalanceAsync(CalendarTestHarness harness, WorkerProfile worker, TimeOffPolicy policy, decimal amount)
    {
        return harness.RunAsync(async services =>
        {
            var result = await services.GetRequiredService<ITimeOffBalanceService>().AdjustAsync(new TimeOffAdjustment
            {
                WorkerId = worker.ItemId,
                PolicyId = policy.ItemId,
                Kind = TimeOffLedgerKind.Opening,
                Amount = amount,
                EffectiveDate = new DateOnly(2026, 1, 1),
            }, "tester");

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));

            return result.Value;
        });
    }

    public static Task<TimeOffAllotment> AddAllotmentAsync(CalendarTestHarness harness, WorkforceTeam team, int slots, Action<TimeOffAllotment> configure = null)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITimeOffAllotmentManager>();
            var allotment = await manager.NewAsync();

            allotment.Name = team.Name + " allotment";
            allotment.TeamId = team.ItemId;
            allotment.WeekdaySlots = [slots, slots, slots, slots, slots, slots, slots];
            configure?.Invoke(allotment);
            await manager.CreateAsync(allotment);

            return allotment;
        });
    }

    public static Task<PlanningResult<TimeOffRequest>> SubmitAsync(CalendarTestHarness harness, WorkerProfile worker, TimeOffType type, DateOnly start, DateOnly? end = null, Action<TimeOffRequestInput> configure = null)
    {
        return harness.RunAsync(services =>
        {
            var input = new TimeOffRequestInput
            {
                WorkerId = worker.ItemId,
                TypeId = type.ItemId,
                StartDate = start,
                EndDate = end ?? start,
            };

            configure?.Invoke(input);

            return services.GetRequiredService<ITimeOffRequestService>().SubmitAsync(input, "tester");
        });
    }

    public static Task<TimeOffPreview> PreviewAsync(CalendarTestHarness harness, WorkerProfile worker, TimeOffType type, DateOnly start, DateOnly? end = null, Action<TimeOffRequestInput> configure = null)
    {
        return harness.RunAsync(services =>
        {
            var input = new TimeOffRequestInput
            {
                WorkerId = worker.ItemId,
                TypeId = type.ItemId,
                StartDate = start,
                EndDate = end ?? start,
            };

            configure?.Invoke(input);

            return services.GetRequiredService<ITimeOffRequestService>().PreviewAsync(input);
        }, commit: false);
    }

    public static Task<TimeOffRequest> FindAsync(CalendarTestHarness harness, string id)
    {
        return harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().FindAsync(id), commit: false);
    }

    public static Task<TimeOffBalance> BalanceAsync(CalendarTestHarness harness, WorkerProfile worker, TimeOffPolicy policy, DateOnly date)
    {
        return harness.RunAsync(services => services.GetRequiredService<ITimeOffBalanceService>().GetBalanceAsync(worker.ItemId, policy.ItemId, date), commit: false);
    }

    public static async Task<IReadOnlyList<TimeOffLedgerEntry>> LedgerAsync(CalendarTestHarness harness, WorkerProfile worker, TimeOffPolicy policy)
    {
        var page = await harness.RunAsync(services => services.GetRequiredService<ITimeOffBalanceService>().PageLedgerAsync(worker.ItemId, policy.ItemId, 0, 100), commit: false);

        return page.Items;
    }

    /// <summary>
    /// Returns the Time off occurrences on a team's calendar in November 2026, with the pending ones from the event
    /// source.
    /// </summary>
    public static async Task<IReadOnlyList<CalendarOccurrence>> ListTimeOffAsync(CalendarTestHarness harness, WorkforceTeam team)
    {
        var calendarId = await harness.RunAsync(async services => (await services.GetRequiredService<ICalendarService>().FindBySourceAsync(WorkforceConstants.Source, team.ItemId)).ItemId, commit: false);
        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = [calendarId],
            FromUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            EventTypes = [WorkforceConstants.EventTypes.TimeOff],
            IncludeEventSources = true,
        });

        return occurrences.Where(occurrence => occurrence.Event.Status != CalendarEventStatus.Cancelled).ToArray();
    }

    /// <summary>
    /// A lock that really excludes, one semaphore per key, for the race tests.
    /// </summary>
    internal sealed class KeyedLock : IDistributedLock
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);

        public async Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        {
            var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync(TestContext.Current.CancellationToken);

            return new Locker(semaphore);
        }

        public async Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        {
            var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            return await semaphore.WaitAsync(timeout, TestContext.Current.CancellationToken)
                ? (new Locker(semaphore), true)
                : (null, false);
        }

        public Task<bool> IsLockAcquiredAsync(string key)
        {
            return Task.FromResult(_semaphores.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 0);
        }

        private sealed class Locker : ILocker
        {
            private SemaphoreSlim _semaphore;

            public Locker(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();

                return ValueTask.CompletedTask;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _semaphore, null)?.Release();
            }
        }
    }
}
