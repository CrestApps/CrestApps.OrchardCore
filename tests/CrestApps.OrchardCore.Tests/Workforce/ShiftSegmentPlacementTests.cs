using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Learning plan Phase 6: another module places a segment, such as a class as training, on a worker's published shift
/// and takes it back by its source; the schedule's draft follows, each change is logged, placing again replaces the
/// segment, and a time no shift covers changes nothing.
/// </summary>
public sealed class ShiftSegmentPlacementTests
{
    private const string Source = "Tests.Class";

    [Fact]
    public async Task ATrainingSegment_PlacedOverPartOfAShift_IsOnTheShiftAndTheDraft_AndLogged_AndPlacingAgainReplacesIt()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var (team, ana, period) = await PublishAsync(harness);

        // Act
        var placed = await PlaceAsync(harness, ana, new TimeOnly(10, 0), new TimeOnly(11, 0), "class-1");
        var shift = Assert.Single(await PlanningKit.ListShiftsAsync(harness, team));
        var draft = Assert.Single(await PlanningKit.ListDraftsAsync(harness, period));
        var changes = await PlanningKit.ListChangesAsync(harness, period);
        var training = await TrainingCodeAsync(harness);

        // Assert
        Assert.True(placed.Succeeded, string.Join(" ", placed.Errors));
        Assert.Equal(shift.ItemId, Assert.Single(placed.Value).ItemId);

        var segment = Assert.Single(Segments(shift));
        Assert.Equal((ActivityKind.Training, training.ItemId, Source, "class-1"), (segment.Kind, segment.ActivityCodeId, segment.Source, segment.SourceId));
        Assert.Equal((PlanningKit.Utc(PlanningKit.Monday, new TimeOnly(10, 0)), PlanningKit.Utc(PlanningKit.Monday, new TimeOnly(11, 0))), (segment.StartUtc, segment.EndUtc));

        var draftSegment = Assert.Single(draft.Segments);
        Assert.Equal((training.ItemId, Source, "class-1"), (draftSegment.ActivityCodeId, draftSegment.Source, draftSegment.SourceId));
        Assert.Equal((new TimeOnly(10, 0), new TimeOnly(11, 0)), (TimeOnly.FromDateTime(draftSegment.Start), TimeOnly.FromDateTime(draftSegment.End)));

        var change = Assert.Single(changes, item => item.Kind == ScheduleChangeKind.SegmentsChanged);
        Assert.Equal(("Class: First aid", ana.ItemId, "manager"), (change.Reason, change.WorkerId, change.ChangedBy));

        // Act: placed again for the same source, later and running past the end of the shift.
        var again = await PlaceAsync(harness, ana, new TimeOnly(16, 30), new TimeOnly(18, 0), "class-1");
        shift = Assert.Single(await PlanningKit.ListShiftsAsync(harness, team));
        draft = Assert.Single(await PlanningKit.ListDraftsAsync(harness, period));

        // Assert: one segment, clipped to the shift.
        Assert.True(again.Succeeded, string.Join(" ", again.Errors));
        segment = Assert.Single(Segments(shift));
        Assert.Equal((PlanningKit.Utc(PlanningKit.Monday, new TimeOnly(16, 30)), PlanningKit.Utc(PlanningKit.Monday, new TimeOnly(17, 0))), (segment.StartUtc, segment.EndUtc));
        Assert.Equal(new TimeOnly(17, 0), TimeOnly.FromDateTime(Assert.Single(draft.Segments).End));
    }

    [Fact]
    public async Task ATimeNoShiftCovers_Fails_AndChangesNothing()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var (team, ana, period) = await PublishAsync(harness);

        // Act (Tuesday: Ana isn't working)
        var placed = await harness.RunAsync(services => services.GetRequiredService<IShiftService>().PlaceSegmentAsync(new ShiftSegmentPlacement
        {
            WorkerId = ana.ItemId,
            Kind = ActivityKind.Training,
            StartUtc = PlanningKit.Utc(PlanningKit.Monday.AddDays(1), new TimeOnly(10, 0)),
            EndUtc = PlanningKit.Utc(PlanningKit.Monday.AddDays(1), new TimeOnly(11, 0)),
            Source = Source,
            SourceId = "class-1",
            Actor = "manager",
        }));

        // Assert
        Assert.False(placed.Succeeded);
        Assert.Equal("No published shift covers that time.", Assert.Single(placed.Errors));
        Assert.Empty(Segments(Assert.Single(await PlanningKit.ListShiftsAsync(harness, team))));
        Assert.Empty(Assert.Single(await PlanningKit.ListDraftsAsync(harness, period)).Segments);
        Assert.DoesNotContain(await PlanningKit.ListChangesAsync(harness, period), item => item.Kind == ScheduleChangeKind.SegmentsChanged);
    }

    [Fact]
    public async Task RemovingTheSegments_TakesThemOffTheShiftAndTheDraft_EvenAfterTheScheduleIsPublishedAgain()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var (team, ana, period) = await PublishAsync(harness);

        await PlaceAsync(harness, ana, new TimeOnly(10, 0), new TimeOnly(11, 0), "class-1");
        await PlaceAsync(harness, ana, new TimeOnly(14, 0), new TimeOnly(15, 0), "class-2");

        // Publishing again keeps what placed each segment.
        await PlanningKit.PublishAsync(harness, period);

        // Act
        var removed = await RemoveAsync(harness, ana, "class-1");
        var twice = await RemoveAsync(harness, ana, "class-1");
        var shift = Assert.Single(await PlanningKit.ListShiftsAsync(harness, team));
        var draft = Assert.Single(await PlanningKit.ListDraftsAsync(harness, period));

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(0, twice);
        Assert.Equal("class-2", Assert.Single(Segments(shift)).SourceId);
        Assert.Equal("class-2", Assert.Single(draft.Segments).SourceId);
        Assert.Equal(3, (await PlanningKit.ListChangesAsync(harness, period)).Count(item => item.Kind == ScheduleChangeKind.SegmentsChanged));
    }

    [Fact]
    public async Task AReassignedShift_DropsTheSegmentsPlacedForThePreviousWorker()
    {
        // Arrange: Ana's class is on her shift; the shift then goes to Ben, who is not in the class.
        await using var harness = await WorkforceTests.CreateAsync();
        var (team, ana, period) = await PublishAsync(harness);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", team);

        await PlaceAsync(harness, ana, new TimeOnly(10, 0), new TimeOnly(11, 0), "class-1");
        var shift = Assert.Single(await PlanningKit.ListShiftsAsync(harness, team));

        // Act
        var moved = await harness.RunAsync(services => services.GetRequiredService<IShiftService>().ReassignAsync(shift.ItemId, ben.ItemId, ScheduleChangeInitiator.Employer, "Cover", "manager"));
        shift = Assert.Single(await PlanningKit.ListShiftsAsync(harness, team));
        var draft = Assert.Single(await PlanningKit.ListDraftsAsync(harness, period));

        // Assert
        Assert.True(moved.Succeeded, string.Join(" ", moved.Errors));
        Assert.Empty(Segments(shift));
        Assert.Empty(draft.Segments);
    }

    private static async Task<(Team Team, StaffMember Ana, SchedulePeriod Period)> PublishAsync(CalendarTestHarness harness)
    {
        await harness.RunAsync(services => services.GetRequiredService<ActivityCodeSeeder>().SeedAsync());

        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, PlanningKit.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, period);

        return (team, ana, period);
    }

    private static Task<PlanningResult<IReadOnlyList<CalendarEvent>>> PlaceAsync(CalendarTestHarness harness, StaffMember worker, TimeOnly start, TimeOnly end, string sourceId)
    {
        return harness.RunAsync(services => services.GetRequiredService<IShiftService>().PlaceSegmentAsync(new ShiftSegmentPlacement
        {
            WorkerId = worker.ItemId,
            Kind = ActivityKind.Training,
            StartUtc = PlanningKit.Utc(PlanningKit.Monday, start),
            EndUtc = PlanningKit.Utc(PlanningKit.Monday, end),
            Source = Source,
            SourceId = sourceId,
            Reason = "Class: First aid",
            Actor = "manager",
        }));
    }

    private static Task<int> RemoveAsync(CalendarTestHarness harness, StaffMember worker, string sourceId)
    {
        return harness.RunAsync(services => services.GetRequiredService<IShiftService>().RemoveSegmentsAsync(
            worker.ItemId,
            Source,
            sourceId,
            PlanningKit.Utc(PlanningKit.Monday, new TimeOnly(0, 0)),
            PlanningKit.Utc(PlanningKit.Monday.AddDays(1), new TimeOnly(0, 0)),
            "manager"));
    }

    private static async Task<ActivityCode> TrainingCodeAsync(CalendarTestHarness harness)
    {
        var codes = await harness.RunAsync(services => services.GetRequiredService<IActivityCodeManager>().GetEnabledAsync(), commit: false);

        return codes.First(code => code.Kind == ActivityKind.Training);
    }

    private static IList<ShiftSegment> Segments(CalendarEvent calendarEvent)
    {
        return calendarEvent.TryGet<ShiftData>(out var data) ? data.Segments : [];
    }
}
