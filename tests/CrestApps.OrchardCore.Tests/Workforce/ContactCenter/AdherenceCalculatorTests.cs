using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// The adherence calculator against hand-computed fixtures: the schedule's work fills the shift around its segments,
/// each moment is in adherence when the agent's state is one the scheduled activity allows, grace at the start and end
/// of an activity and short slips are forgiven, exceptions leave the scheduled time, and conformance compares worked
/// with scheduled work time.
/// </summary>
public sealed class AdherenceCalculatorTests
{
    private static readonly DateTime _day = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Schedule_FillsTheShiftWithWorkAroundItsSegments()
    {
        var schedule = AdherenceCalculator.BuildSchedule([Shift(9, 0, 17, 0, Segment(ActivityKind.Meal, 12, 0, 12, 30), Segment(ActivityKind.Break, 15, 0, 15, 15))]);

        Assert.Equal(
            [
                (At(9, 0), At(12, 0), ActivityKind.Work),
                (At(12, 0), At(12, 30), ActivityKind.Meal),
                (At(12, 30), At(15, 0), ActivityKind.Work),
                (At(15, 0), At(15, 15), ActivityKind.Break),
                (At(15, 15), At(17, 0), ActivityKind.Work),
            ],
            schedule.Select(piece => (piece.StartUtc, piece.EndUtc, piece.Kind)));
    }

    [Fact]
    public void AnAgentWhoFollowsTheSchedule_IsFullyInAdherence()
    {
        var schedule = AdherenceCalculator.BuildSchedule([Shift(9, 0, 11, 0, Segment(ActivityKind.Break, 10, 0, 10, 15))]);
        var actual = new[]
        {
            Actual(AgentPresenceStatus.Available, 9, 0, 9, 40),
            Actual(AgentPresenceStatus.Busy, 9, 40, 10, 0),
            Actual(AgentPresenceStatus.Break, 10, 0, 10, 15),
            Actual(AgentPresenceStatus.Available, 10, 15, 11, 0),
        };

        var result = AdherenceCalculator.Compute(schedule, actual, AdherenceCalculator.DefaultRules(), [], At(0, 0), At(24, 0));

        Assert.Equal(7200, result.ScheduledSeconds);
        Assert.Equal(7200, result.InAdherenceSeconds);
        Assert.Equal(1d, result.Adherence);
        Assert.Equal(6300, result.ScheduledWorkSeconds);
        Assert.Equal(6300, result.ActualWorkSeconds);
        Assert.Equal(1d, result.Conformance);
    }

    [Fact]
    public void ALateBreak_IsOutOfAdherenceBeyondTheGrace()
    {
        // The break is scheduled 10:00 to 10:15; the agent takes it 10:10 to 10:25. With two minutes of grace at the
        // start and end of each activity, 10:02 to 10:10 (on the phone during the break) and 10:15 to 10:23 (still on
        // break when work resumed) are out of adherence: 16 minutes.
        var schedule = AdherenceCalculator.BuildSchedule([Shift(9, 0, 11, 0, Segment(ActivityKind.Break, 10, 0, 10, 15))]);
        var actual = new[]
        {
            Actual(AgentPresenceStatus.Available, 9, 0, 10, 10),
            Actual(AgentPresenceStatus.Break, 10, 10, 10, 25),
            Actual(AgentPresenceStatus.Available, 10, 25, 11, 0),
        };

        var result = AdherenceCalculator.Compute(schedule, actual, AdherenceCalculator.DefaultRules(), [], At(0, 0), At(24, 0));

        Assert.Equal(7200 - (16 * 60), result.InAdherenceSeconds);
        Assert.Equal(2, result.Segments.Count(segment => !segment.InAdherence && !segment.Excepted));
    }

    [Fact]
    public void ASlipShorterThanTheMinimum_IsForgiven()
    {
        var schedule = AdherenceCalculator.BuildSchedule([Shift(9, 0, 10, 0)]);
        var actual = new[]
        {
            Actual(AgentPresenceStatus.Available, 9, 0, 9, 30),
            Actual(AgentPresenceStatus.Away, 9, 30, 9, 30, endSecond: 45),
            Actual(AgentPresenceStatus.Available, 9, 30, 10, 0, startSecond: 45),
        };

        var result = AdherenceCalculator.Compute(schedule, actual, AdherenceCalculator.DefaultRules(), [], At(0, 0), At(24, 0));

        Assert.Equal(3600, result.InAdherenceSeconds);
    }

    [Fact]
    public void ASignedOutAgent_IsOutOfAdherenceAndHasNoWorkTime()
    {
        var schedule = AdherenceCalculator.BuildSchedule([Shift(9, 0, 10, 0)]);

        var result = AdherenceCalculator.Compute(schedule, [], AdherenceCalculator.DefaultRules(), [], At(0, 0), At(24, 0));

        Assert.Equal(3600, result.ScheduledSeconds);
        Assert.Equal(0, result.InAdherenceSeconds);
        Assert.Equal(0d, result.Adherence);
        Assert.Equal(0d, result.Conformance);
    }

    [Fact]
    public void AnException_LeavesTheScheduledTime()
    {
        var schedule = AdherenceCalculator.BuildSchedule([Shift(9, 0, 11, 0)]);
        var actual = new[] { Actual(AgentPresenceStatus.Available, 9, 0, 10, 0) };
        var exceptions = new[] { new AdherenceException { StartUtc = At(10, 0), EndUtc = At(11, 0), Reason = AdherenceExceptionReason.Coaching } };

        var result = AdherenceCalculator.Compute(schedule, actual, AdherenceCalculator.DefaultRules(), exceptions, At(0, 0), At(24, 0));

        Assert.Equal(3600, result.ExceptionSeconds);
        Assert.Equal(3600, result.InAdherenceSeconds);
        Assert.Equal(1d, result.Adherence);
    }

    [Fact]
    public void ARuleCanAskForAReason()
    {
        var schedule = AdherenceCalculator.BuildSchedule([Shift(9, 0, 10, 0, Segment(ActivityKind.Meal, 9, 0, 10, 0))]);
        var rules = AdherenceCalculator.DefaultRules();
        rules.Single(rule => rule.Kind == ActivityKind.Meal).AllowedReasons = ["Meal"];

        var onBreak = AdherenceCalculator.Compute(schedule, [Actual(AgentPresenceStatus.Break, 9, 0, 10, 0, reason: "Personal")], rules, [], At(0, 0), At(24, 0));
        var atMeal = AdherenceCalculator.Compute(schedule, [Actual(AgentPresenceStatus.Break, 9, 0, 10, 0, reason: "Meal")], rules, [], At(0, 0), At(24, 0));

        Assert.True(onBreak.InAdherenceSeconds < 3600);
        Assert.Equal(3600, atMeal.InAdherenceSeconds);
    }

    [Fact]
    public void Current_TellsHowLongTheAgentHasBeenOutAndWhetherToAlert()
    {
        var schedule = AdherenceCalculator.BuildSchedule([Shift(9, 0, 11, 0, Segment(ActivityKind.Break, 10, 0, 10, 15))]);
        var rules = AdherenceCalculator.DefaultRules();

        // On break since 9:50, ten minutes before the break: out of adherence since 9:50, alert after five minutes.
        var state = AdherenceCalculator.Current(schedule, AgentPresenceStatus.Break, null, At(9, 50), rules, At(9, 57));

        Assert.False(state.InAdherence);
        Assert.Equal(ActivityKind.Work, state.Scheduled.Kind);
        Assert.Equal(At(9, 50), state.OutSinceUtc);
        Assert.True(state.Alert);

        var early = AdherenceCalculator.Current(schedule, AgentPresenceStatus.Break, null, At(9, 50), rules, At(9, 53));
        Assert.False(early.Alert);

        var onTime = AdherenceCalculator.Current(schedule, AgentPresenceStatus.Break, null, At(10, 0), rules, At(10, 5));
        Assert.True(onTime.InAdherence);
    }

    private static DateTime At(int hour, int minute, int second = 0)
        => _day.AddHours(hour).AddMinutes(minute).AddSeconds(second);

    private static AdherenceShift Shift(int startHour, int startMinute, int endHour, int endMinute, params ShiftSegment[] segments)
        => new() { StartUtc = At(startHour, startMinute), EndUtc = At(endHour, endMinute), Segments = [.. segments] };

    private static ShiftSegment Segment(ActivityKind kind, int startHour, int startMinute, int endHour, int endMinute)
        => new() { Kind = kind, ActivityCodeId = kind.ToString(), StartUtc = At(startHour, startMinute), EndUtc = At(endHour, endMinute) };

    private static AdherenceActual Actual(AgentPresenceStatus status, int startHour, int startMinute, int endHour, int endMinute, string reason = null, int startSecond = 0, int endSecond = 0)
        => new() { Status = status, Reason = reason, StartUtc = At(startHour, startMinute, startSecond), EndUtc = At(endHour, endMinute, endSecond) };
}
