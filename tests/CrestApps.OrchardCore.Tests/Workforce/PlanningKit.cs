using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Shared setup for the shift planning tests: skills, templates, schedules, draft shifts, publishing and reading the
/// team calendar back.
/// </summary>
internal static class PlanningKit
{
    public const string Zone = "America/New_York";

    // Monday 9 November 2026, a week after the harness clock.
    public static readonly DateOnly Monday = new(2026, 11, 9);

    public static Task<Skill> AddSkillAsync(CalendarTestHarness harness, string name)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();
            var skill = await manager.NewAsync();

            skill.Name = name;
            await manager.CreateAsync(skill);

            return skill;
        });
    }

    public static Task GrantSkillAsync(CalendarTestHarness harness, string workerId, string skillId)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>()
            .AssignAsync(SkillSubject.Worker(workerId), new SkillAssignmentInput { SkillId = skillId }, "tester"));
    }

    public static Task<WorkerProfile> AddMemberAsync(CalendarTestHarness harness, string name, WorkforceTeam team, Action<WorkerProfile> configure = null)
    {
        return WorkforceTests.AddWorkerAsync(harness, name, worker =>
        {
            worker.UserId = "user-" + name.ToLowerInvariant().Replace(' ', '-');
            worker.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId });
            configure?.Invoke(worker);
        });
    }

    public static Task<ShiftTemplate> AddTemplateAsync(CalendarTestHarness harness, string name, TimeOnly start, int lengthMinutes = 480, Action<ShiftTemplate> configure = null)
    {
        return harness.RunAsync(async services =>
        {
            var templates = services.GetRequiredService<IShiftTemplateManager>();
            var template = await templates.NewAsync();

            template.Name = name;
            template.Start = start;
            template.LengthMinutes = lengthMinutes;
            configure?.Invoke(template);
            await templates.CreateAsync(template);

            return template;
        });
    }

    public static Task<SchedulePeriod> CreatePeriodAsync(CalendarTestHarness harness, WorkforceTeam team, int weeks = 1)
    {
        return harness.RunAsync(async services =>
        {
            var period = await services.GetRequiredService<ISchedulePlanningService>().CreatePeriodAsync(team.ItemId, Monday, weeks, null, "tester");

            Assert.True(period.Succeeded, string.Join(" ", period.Errors));

            return period.Value;
        });
    }

    public static Task<DraftShift> AddDraftAsync(CalendarTestHarness harness, SchedulePeriod period, string workerId, DateOnly date, TimeOnly start, TimeOnly end, string templateId = null)
    {
        return harness.RunAsync(async services =>
        {
            var endDate = end <= start ? date.AddDays(1) : date;
            var saved = await services.GetRequiredService<ISchedulePlanningService>().SaveDraftShiftAsync(new DraftShift
            {
                ScenarioId = period.PrimaryScenarioId,
                WorkerId = workerId,
                Date = date,
                Start = date.ToDateTime(start),
                End = endDate.ToDateTime(end),
                TemplateId = templateId,
            }, "tester");

            Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));

            return saved.Value;
        });
    }

    public static async Task<PlanningPublishResult> PublishAsync(CalendarTestHarness harness, SchedulePeriod period)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().PublishAsync(period.ItemId, "tester"));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result;
    }

    public static Task<IReadOnlyList<DraftShift>> ListDraftsAsync(CalendarTestHarness harness, SchedulePeriod period)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ListDraftShiftsAsync(period.PrimaryScenarioId), commit: false);
    }

    public static Task<IReadOnlyList<ScheduleChange>> ListChangesAsync(CalendarTestHarness harness, SchedulePeriod period)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ListChangesAsync(period.ItemId), commit: false);
    }

    /// <summary>
    /// Returns the live Shift events on a team's calendar in the four weeks from <see cref="Monday"/>.
    /// </summary>
    public static async Task<IReadOnlyList<CalendarEvent>> ListShiftsAsync(CalendarTestHarness harness, WorkforceTeam team)
    {
        var calendarId = await harness.RunAsync(async services => (await services.GetRequiredService<ICalendarService>().FindBySourceAsync(WorkforceConstants.Source, team.ItemId)).ItemId, commit: false);
        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = [calendarId],
            FromUtc = Utc(Monday, new TimeOnly(0, 0)).AddDays(-7),
            ToUtc = Utc(Monday, new TimeOnly(0, 0)).AddDays(28),
            EventTypes = [WorkforceConstants.EventTypes.Shift],
        });

        return occurrences
            .Where(occurrence => occurrence.Event.Status != CalendarEventStatus.Cancelled)
            .Select(occurrence => occurrence.Event)
            .OrderBy(calendarEvent => calendarEvent.StartUtc)
            .ToArray();
    }

    public static string WorkerOf(CalendarEvent calendarEvent)
    {
        return calendarEvent.TryGet<ShiftData>(out var data) ? data.WorkerId : null;
    }

    public static DateTime Utc(DateOnly date, TimeOnly time)
    {
        return CalendarTime.ToUtc(date.ToDateTime(time), Zone);
    }

    public static Task<WorkforceTeam> UpdateTeamAsync(CalendarTestHarness harness, WorkforceTeam team, Action<WorkforceTeam> change)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITeamManager>();
            var stored = await manager.FindByIdAsync(team.ItemId);

            change(stored);
            await manager.UpdateAsync(stored);

            return stored;
        });
    }
}
