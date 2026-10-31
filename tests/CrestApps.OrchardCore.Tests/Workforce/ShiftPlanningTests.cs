using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Shift planning on a real SQLite store: rotations filling a draft schedule, publishing it to the team's calendar,
/// republishing only what changed with a change log, coverage that leaves out breaks, copying a period, and the rules
/// a draft shift must follow.
/// </summary>
public sealed class ShiftPlanningTests
{
    private const string Zone = "America/New_York";

    // Monday 9 November 2026; the period covers two weeks.
    private static readonly DateOnly _monday = new(2026, 11, 9);

    [Fact]
    public async Task ApplyPatterns_FillsTheDraftFromTheRotation()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        // Act
        var added = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ApplyPatternsAsync(setup.Period.PrimaryScenarioId, "tester"));
        var drafts = await ListDraftsAsync(harness, setup.Period);

        // Assert (week 1: Monday and Tuesday; week 2: Wednesday)
        Assert.Equal(3, added);
        Assert.Equal([_monday, _monday.AddDays(1), _monday.AddDays(9)], drafts.Select(draft => draft.Date).Order());
        Assert.All(drafts, draft =>
        {
            Assert.Equal(setup.Worker.ItemId, draft.WorkerId);
            Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(draft.Start));
            Assert.Equal(new TimeOnly(17, 0), TimeOnly.FromDateTime(draft.End));
            Assert.Equal(new TimeOnly(13, 0), TimeOnly.FromDateTime(Assert.Single(draft.Segments).Start));
        });
    }

    [Fact]
    public async Task Publish_PutsTheShiftsOnTheTeamCalendar_WithTheirSegmentsInUtc()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ApplyPatternsAsync(setup.Period.PrimaryScenarioId, "tester"));

        // Act
        var result = await PublishAsync(harness, setup.Period);
        var shifts = await ListShiftsAsync(harness, setup);
        var period = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().FindPeriodAsync(setup.Period.ItemId), commit: false);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(3, result.Added);
        Assert.Equal(3, shifts.Count);
        Assert.Equal(SchedulePeriodStatus.Published, period.Status);
        Assert.Equal(1, period.PublishedVersion);

        var first = shifts.OrderBy(shift => shift.StartUtc).First();
        var data = first.Event.TryGet<ShiftData>(out var shiftData) ? shiftData : null;

        Assert.Equal(WorkforceConstants.Source, first.Event.Source);
        Assert.Equal("staff-1", Assert.Single(first.Event.Attendees).UserId);
        Assert.NotNull(data);
        Assert.Equal(setup.Worker.ItemId, data.WorkerId);
        Assert.Equal(CalendarTime.ToUtc(_monday.ToDateTime(new TimeOnly(13, 0)), Zone), Assert.Single(data.Segments).StartUtc);
        Assert.Equal(ActivityKind.Meal, data.Segments[0].Kind);
    }

    [Fact]
    public async Task Republish_ChangesOnlyWhatChanged_AndLogsEveryChange()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ApplyPatternsAsync(setup.Period.PrimaryScenarioId, "tester"));
        await PublishAsync(harness, setup.Period);

        var before = (await ListShiftsAsync(harness, setup)).ToDictionary(shift => shift.Event.SourceId, shift => shift.Event.ItemId);
        var drafts = (await ListDraftsAsync(harness, setup.Period)).OrderBy(draft => draft.Date).ToList();

        // Act: move Monday an hour later, remove Tuesday, add Thursday of week 2
        await harness.RunAsync(async services =>
        {
            var planning = services.GetRequiredService<ISchedulePlanningService>();
            var monday = drafts[0];

            monday.Start = monday.Start.AddHours(1);
            monday.End = monday.End.AddHours(1);

            Assert.True((await planning.SaveDraftShiftAsync(monday, "tester")).Succeeded);
            Assert.True(await planning.DeleteDraftShiftAsync(drafts[1].ItemId, "tester"));
            Assert.True((await planning.SaveDraftShiftAsync(NewDraft(setup, _monday.AddDays(10)), "tester")).Succeeded);
        });

        var result = await PublishAsync(harness, setup.Period);
        var after = (await ListShiftsAsync(harness, setup)).ToDictionary(shift => shift.Event.SourceId, shift => shift.Event.ItemId);
        var changes = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ListChangesAsync(setup.Period.ItemId), commit: false);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal((1, 1, 1), (result.Added, result.Updated, result.Removed));
        Assert.Equal(3, after.Count);
        Assert.Equal(before[drafts[2].ShiftKey], after[drafts[2].ShiftKey]);
        Assert.Equal(before[drafts[0].ShiftKey], after[drafts[0].ShiftKey]);
        Assert.Equal(
            new[] { ScheduleChangeKind.Added, ScheduleChangeKind.Moved, ScheduleChangeKind.Removed }.Order(),
            changes.Select(change => change.Kind).Order());
        Assert.All(changes, change => Assert.Equal(2, change.PublishedVersion));
    }

    [Fact]
    public async Task Coverage_CountsTheWorkersOnShift_LessTheirBreaks()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        await harness.RunAsync(async services =>
        {
            var coverage = services.GetRequiredService<ICoverageService>();
            var requirement = new CoverageRequirement
            {
                TeamId = setup.Team.ItemId,
                Name = "Front desk",
                Rows = [new CoverageRow { Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0), Minimum = 1, Target = 2 }],
            };

            Assert.True((await coverage.SaveRequirementAsync(requirement)).Succeeded);
            Assert.True((await services.GetRequiredService<ISchedulePlanningService>().SaveDraftShiftAsync(NewDraft(setup, _monday), "tester")).Succeeded);
        });

        // Act
        var intervals = await harness.RunAsync(services => services.GetRequiredService<ICoverageService>().ComputeAsync(new CoverageQuery
        {
            TeamId = setup.Team.ItemId,
            From = _monday,
            To = _monday,
            IntervalMinutes = 30,
            ScenarioId = setup.Period.PrimaryScenarioId,
        }), commit: false);

        CoverageInterval At(int hour, int minute) => intervals.Single(interval => interval.StartUtc == CalendarTime.ToUtc(_monday.ToDateTime(new TimeOnly(hour, minute)), Zone));

        // Assert (09:00-17:00 shift with a 13:00-13:30 meal)
        Assert.Equal((1, 2, 1), (At(9, 0).Minimum, At(9, 0).Target, At(9, 0).Scheduled));
        Assert.Equal(0, At(13, 0).Scheduled);
        Assert.Equal(1, At(13, 30).Scheduled);
        Assert.Equal(0, At(8, 0).Target);
    }

    [Fact]
    public async Task CopyFrom_AnotherPeriod_MovesItsDraftsByTheDaysBetween()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ApplyPatternsAsync(setup.Period.PrimaryScenarioId, "tester"));

        var next = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().CreatePeriodAsync(setup.Team.ItemId, _monday.AddDays(14), 2, "Next two weeks", "tester"));

        // Act
        var copied = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().CopyFromPeriodAsync(next.Value.PrimaryScenarioId, setup.Period.ItemId, "tester"));
        var drafts = await ListDraftsAsync(harness, next.Value);

        // Assert
        Assert.Equal(3, copied);
        Assert.Equal([_monday.AddDays(14), _monday.AddDays(15), _monday.AddDays(23)], drafts.Select(draft => draft.Date).Order());
        Assert.All(drafts, draft => Assert.NotEqual(Guid.Empty.ToString(), draft.ShiftKey));
    }

    [Fact]
    public async Task SaveDraftShift_RefusesAShiftOutsideThePeriod_ForSomeoneOffTheTeam_OrThatEndsBeforeItStarts()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);
        var stranger = await WorkforceTests.AddWorkerAsync(harness, "Stranger");

        // Act
        var (outside, offTeam, backwards) = await harness.RunAsync(async services =>
        {
            var planning = services.GetRequiredService<ISchedulePlanningService>();
            var late = NewDraft(setup, _monday.AddDays(30));
            var other = NewDraft(setup, _monday);
            var reversed = NewDraft(setup, _monday);

            other.WorkerId = stranger.ItemId;
            reversed.End = reversed.Start.AddHours(-1);

            return (await planning.SaveDraftShiftAsync(late, "tester"), await planning.SaveDraftShiftAsync(other, "tester"), await planning.SaveDraftShiftAsync(reversed, "tester"));
        });

        // Assert
        Assert.False(outside.Succeeded);
        Assert.False(offTeam.Succeeded);
        Assert.False(backwards.Succeeded);
    }

    [Fact]
    public async Task Publish_AWorkerWithoutAnAccountOrEmail_StillPutsTheShiftOnTheCalendar()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);
        var temp = await WorkforceTests.AddWorkerAsync(harness, "Temp worker", worker =>
        {
            worker.UserId = null;
            worker.Email = null;
            worker.TeamMemberships.Add(new TeamMembership { TeamId = setup.Team.ItemId });
        });

        await harness.RunAsync(async services =>
        {
            var draft = NewDraft(setup, _monday);

            draft.WorkerId = temp.ItemId;
            Assert.True((await services.GetRequiredService<ISchedulePlanningService>().SaveDraftShiftAsync(draft, "tester")).Succeeded);
        });

        // Act
        var result = await PublishAsync(harness, setup.Period);
        var shift = Assert.Single(await ListShiftsAsync(harness, setup));

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(1, result.Added);
        Assert.Empty(shift.Event.Attendees);
        Assert.Equal("Temp worker", shift.Event.Title);
        Assert.True(shift.Event.TryGet<ShiftData>(out var data));
        Assert.Equal(temp.ItemId, data.WorkerId);
    }

    [Fact]
    public async Task Publish_LeavesOpenShiftsOffTheCalendar_AndCountsThem()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        await harness.RunAsync(async services =>
        {
            var open = NewDraft(setup, _monday.AddDays(3));

            open.WorkerId = null;
            Assert.True((await services.GetRequiredService<ISchedulePlanningService>().SaveDraftShiftAsync(open, "tester")).Succeeded);
            Assert.True((await services.GetRequiredService<ISchedulePlanningService>().SaveDraftShiftAsync(NewDraft(setup, _monday), "tester")).Succeeded);
        });

        // Act
        var result = await PublishAsync(harness, setup.Period);
        var shifts = await ListShiftsAsync(harness, setup);

        // Assert
        Assert.Equal((1, 1), (result.Added, result.Open));
        Assert.Single(shifts);
    }

    [Fact]
    public async Task Republish_ToSomeoneElse_LogsAReassignment()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);
        var cover = await WorkforceTests.AddWorkerAsync(harness, "Dr. Cover", worker =>
        {
            worker.UserId = "staff-2";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = setup.Team.ItemId });
        });

        await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().SaveDraftShiftAsync(NewDraft(setup, _monday), "tester"));
        await PublishAsync(harness, setup.Period);

        var draft = Assert.Single(await ListDraftsAsync(harness, setup.Period));

        // Act
        await harness.RunAsync(async services =>
        {
            draft.WorkerId = cover.ItemId;
            Assert.True((await services.GetRequiredService<ISchedulePlanningService>().SaveDraftShiftAsync(draft, "tester")).Succeeded);
        });

        var result = await PublishAsync(harness, setup.Period);
        var shift = Assert.Single(await ListShiftsAsync(harness, setup));
        var change = Assert.Single(await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ListChangesAsync(setup.Period.ItemId), commit: false));

        // Assert
        Assert.Equal(1, result.Updated);
        Assert.Equal("staff-2", Assert.Single(shift.Event.Attendees).UserId);
        Assert.Equal(ScheduleChangeKind.Reassigned, change.Kind);
        Assert.Equal(setup.Worker.ItemId, change.PreviousWorkerId);
        Assert.Equal(cover.ItemId, change.WorkerId);
    }

    [Fact]
    public async Task Coverage_OfThePublishedSchedule_CountsTheCalendarShifts()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().SaveDraftShiftAsync(NewDraft(setup, _monday), "tester"));
        await PublishAsync(harness, setup.Period);

        // Act
        var intervals = await harness.RunAsync(services => services.GetRequiredService<ICoverageService>().ComputeAsync(new CoverageQuery
        {
            TeamId = setup.Team.ItemId,
            From = _monday,
            To = _monday,
            IntervalMinutes = 60,
        }), commit: false);

        CoverageInterval At(int hour) => intervals.Single(interval => interval.StartUtc == CalendarTime.ToUtc(_monday.ToDateTime(new TimeOnly(hour, 0)), Zone));

        // Assert (the 13:00 hour holds the meal)
        Assert.Equal(24, intervals.Count);
        Assert.Equal(1, At(9).Scheduled);
        Assert.Equal(0, At(13).Scheduled);
        Assert.Equal(0, At(17).Scheduled);
    }

    [Fact]
    public async Task Coverage_ADateRow_ReplacesThatWeekdaysRows()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        await harness.RunAsync(async services =>
        {
            var requirement = new CoverageRequirement
            {
                TeamId = setup.Team.ItemId,
                Name = "Front desk",
                Rows =
                [
                    new CoverageRow { Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0), Minimum = 1, Target = 2 },
                    new CoverageRow { Date = _monday.AddDays(7), Start = new TimeOnly(10, 0), End = new TimeOnly(12, 0), Minimum = 3, Target = 4 },
                ],
            };

            Assert.True((await services.GetRequiredService<ICoverageService>().SaveRequirementAsync(requirement)).Succeeded);
        });

        // Act
        var intervals = await harness.RunAsync(services => services.GetRequiredService<ICoverageService>().ComputeAsync(new CoverageQuery
        {
            TeamId = setup.Team.ItemId,
            From = _monday,
            To = _monday.AddDays(7),
            IntervalMinutes = 60,
            ScenarioId = setup.Period.PrimaryScenarioId,
        }), commit: false);

        int TargetAt(DateOnly date, int hour) => intervals.Single(interval => interval.StartUtc == CalendarTime.ToUtc(date.ToDateTime(new TimeOnly(hour, 0)), Zone)).Target;

        // Assert
        Assert.Equal(2, TargetAt(_monday, 9));
        Assert.Equal(0, TargetAt(_monday.AddDays(7), 9));
        Assert.Equal(4, TargetAt(_monday.AddDays(7), 10));
        Assert.Equal(0, TargetAt(_monday.AddDays(1), 10));
    }

    [Fact]
    public async Task SaveRequirement_RefusesARowWhoseTargetIsBelowItsMinimum()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<ICoverageService>().SaveRequirementAsync(new CoverageRequirement
        {
            TeamId = setup.Team.ItemId,
            Name = "Front desk",
            Rows = [new CoverageRow { Day = DayOfWeek.Monday, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0), Minimum = 3, Target = 2 }],
        }));

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DeletePeriod_RemovesANeverPublishedSchedule_ButKeepsAPublishedOne()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var setup = await SetUpAsync(harness);
        var draftOnly = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().CreatePeriodAsync(setup.Team.ItemId, _monday.AddDays(14), 1, null, "tester"));

        await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ApplyPatternsAsync(setup.Period.PrimaryScenarioId, "tester"));
        await PublishAsync(harness, setup.Period);

        // Act
        var (deleted, kept) = await harness.RunAsync(async services =>
        {
            var planning = services.GetRequiredService<ISchedulePlanningService>();

            return (await planning.DeletePeriodAsync(draftOnly.Value.ItemId), await planning.DeletePeriodAsync(setup.Period.ItemId));
        });

        // Assert
        Assert.True(deleted);
        Assert.False(kept);
    }

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness)
    {
        var team = await WorkforceTests.AddTeamAsync(harness, "Clinic", TeamKind.Team);
        var worker = await WorkforceTests.AddWorkerAsync(harness, "Dr. Shift", w =>
        {
            w.UserId = "staff-1";
            w.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId });
        });

        return await harness.RunAsync(async services =>
        {
            var meal = new ActivityCode { Name = "Meal", Kind = ActivityKind.Meal, IsPaid = false, CountsAsWork = false, CountsTowardCoverage = false };

            await services.GetRequiredService<IActivityCodeManager>().CreateAsync(meal);

            var templates = services.GetRequiredService<IShiftTemplateManager>();
            var template = await templates.NewAsync();

            template.Name = "Day";
            template.Start = new TimeOnly(9, 0);
            template.LengthMinutes = 480;
            template.Segments = [new ShiftTemplateSegment { ActivityCodeId = meal.ItemId, OffsetMinutes = 240, LengthMinutes = 30 }];
            await templates.CreateAsync(template);

            var patterns = services.GetRequiredService<IShiftPatternManager>();
            var pattern = await patterns.NewAsync();

            pattern.Name = "Two-week rotation";
            pattern.TeamId = team.ItemId;
            pattern.WeekCount = 2;
            pattern.Days =
            [
                new ShiftPatternDay { Week = 0, Day = DayOfWeek.Monday, TemplateId = template.ItemId },
                new ShiftPatternDay { Week = 0, Day = DayOfWeek.Tuesday, TemplateId = template.ItemId },
                new ShiftPatternDay { Week = 1, Day = DayOfWeek.Wednesday, TemplateId = template.ItemId },
            ];
            pattern.Workers = [new ShiftPatternWorker { WorkerId = worker.ItemId, StartDate = _monday }];
            await patterns.CreateAsync(pattern);

            var period = await services.GetRequiredService<ISchedulePlanningService>().CreatePeriodAsync(team.ItemId, _monday, 2, "First two weeks", "tester");

            Assert.True(period.Succeeded, string.Join(" ", period.Errors));

            return new Setup(team, worker, template, meal, period.Value);
        });
    }

    private static DraftShift NewDraft(Setup setup, DateOnly date)
    {
        return new DraftShift
        {
            ScenarioId = setup.Period.PrimaryScenarioId,
            WorkerId = setup.Worker.ItemId,
            Date = date,
            Start = date.ToDateTime(new TimeOnly(9, 0)),
            End = date.ToDateTime(new TimeOnly(17, 0)),
            TemplateId = setup.Template.ItemId,
            Segments = [new DraftShiftSegment { ActivityCodeId = setup.Meal.ItemId, Start = date.ToDateTime(new TimeOnly(13, 0)), End = date.ToDateTime(new TimeOnly(13, 30)) }],
        };
    }

    private static Task<IReadOnlyList<DraftShift>> ListDraftsAsync(CalendarTestHarness harness, SchedulePeriod period)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().ListDraftShiftsAsync(period.PrimaryScenarioId), commit: false);
    }

    private static Task<PlanningPublishResult> PublishAsync(CalendarTestHarness harness, SchedulePeriod period)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().PublishAsync(period.ItemId, "tester"));
    }

    private static async Task<IReadOnlyList<CalendarOccurrence>> ListShiftsAsync(CalendarTestHarness harness, Setup setup)
    {
        var calendarId = await harness.RunAsync(async services => (await services.GetRequiredService<ICalendarService>().FindBySourceAsync(WorkforceConstants.Source, setup.Team.ItemId)).ItemId, commit: false);

        return await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = [calendarId],
            FromUtc = CalendarTime.ToUtc(_monday.ToDateTime(TimeOnly.MinValue), Zone),
            ToUtc = CalendarTime.ToUtc(_monday.AddDays(28).ToDateTime(TimeOnly.MinValue), Zone),
            EventTypes = [WorkforceConstants.EventTypes.Shift],
        });
    }

    private sealed record Setup(WorkforceTeam Team, WorkerProfile Worker, ShiftTemplate Template, ActivityCode Meal, SchedulePeriod Period);
}
