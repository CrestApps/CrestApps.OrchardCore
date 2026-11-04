using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services;

/// <summary>
/// Compares a worker's published schedule with their agent's actual states. Pure: the same schedule, states, rules and
/// exceptions always give the same result.
/// </summary>
/// <remarks>
/// A moment of scheduled time is in adherence when the agent's state is one the scheduled activity allows. Between two
/// activities of the same shift, the agent may still be in the previous one for the new activity's start grace, and
/// already in the next one for the old activity's end grace. A slip shorter than the rule's minimum is forgiven.
/// Exceptions leave scheduled time out of the share. Time outside the schedule is not counted.
/// </remarks>
public static class AdherenceCalculator
{
    private static readonly HashSet<AgentPresenceStatus> _workStates =
    [
        AgentPresenceStatus.Available,
        AgentPresenceStatus.Reserved,
        AgentPresenceStatus.Busy,
        AgentPresenceStatus.WrapUp,
    ];

    /// <summary>
    /// Gets the agent states that count as work for conformance.
    /// </summary>
    public static IReadOnlySet<AgentPresenceStatus> WorkStates => _workStates;

    /// <summary>
    /// Gets the rules used when a team has no rule set: work is available, reserved, busy or wrapping up; breaks and
    /// meals are a break; meetings, training and coaching are their own states; time off is signed out; and other
    /// activities are any state but signed out.
    /// </summary>
    /// <returns>A new list of rules.</returns>
    public static List<AdherenceRule> DefaultRules()
    {
        var signedIn = Enum.GetValues<AgentPresenceStatus>().Where(status => status != AgentPresenceStatus.Offline).ToList();

        return
        [
            new AdherenceRule { Kind = ActivityKind.Work, AllowedStatuses = [.. _workStates] },
            new AdherenceRule { Kind = ActivityKind.Break, AllowedStatuses = [AgentPresenceStatus.Break] },
            new AdherenceRule { Kind = ActivityKind.Meal, AllowedStatuses = [AgentPresenceStatus.Break] },
            new AdherenceRule { Kind = ActivityKind.Meeting, AllowedStatuses = [AgentPresenceStatus.Meeting, AgentPresenceStatus.Away, AgentPresenceStatus.DoNotDisturb] },
            new AdherenceRule { Kind = ActivityKind.Training, AllowedStatuses = [AgentPresenceStatus.Training, AgentPresenceStatus.Away, AgentPresenceStatus.DoNotDisturb] },
            new AdherenceRule { Kind = ActivityKind.Coaching, AllowedStatuses = [AgentPresenceStatus.Training, AgentPresenceStatus.Meeting, AgentPresenceStatus.Away, AgentPresenceStatus.DoNotDisturb] },
            new AdherenceRule { Kind = ActivityKind.OfflineWork, AllowedStatuses = [AgentPresenceStatus.Away, AgentPresenceStatus.DoNotDisturb, AgentPresenceStatus.Meeting] },
            new AdherenceRule { Kind = ActivityKind.Travel, AllowedStatuses = [.. signedIn, AgentPresenceStatus.Offline] },
            new AdherenceRule { Kind = ActivityKind.OnCall, AllowedStatuses = [.. signedIn, AgentPresenceStatus.Offline] },
            new AdherenceRule { Kind = ActivityKind.TimeOff, AllowedStatuses = [AgentPresenceStatus.Offline] },
            new AdherenceRule { Kind = ActivityKind.Other, AllowedStatuses = signedIn },
        ];
    }

    /// <summary>
    /// Finds the rule for a scheduled activity: the one for its code, else the one for its kind, else the default.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <param name="kind">The kind of activity.</param>
    /// <param name="activityCodeId">The activity code.</param>
    /// <returns>The rule.</returns>
    public static AdherenceRule RuleFor(IReadOnlyList<AdherenceRule> rules, ActivityKind kind, string activityCodeId)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return (activityCodeId is null ? null : rules.FirstOrDefault(rule => string.Equals(rule.ActivityCodeId, activityCodeId, StringComparison.Ordinal)))
            ?? rules.FirstOrDefault(rule => rule.ActivityCodeId is null && rule.Kind == kind)
            ?? DefaultRules().First(rule => rule.Kind == kind);
    }

    /// <summary>
    /// Builds the schedule from published shifts: each shift's segments, with work filling the time around them.
    /// </summary>
    /// <param name="shifts">The shifts.</param>
    /// <returns>The scheduled pieces, in order.</returns>
    public static List<AdherenceScheduledPiece> BuildSchedule(IEnumerable<AdherenceShift> shifts)
    {
        ArgumentNullException.ThrowIfNull(shifts);

        var pieces = new List<AdherenceScheduledPiece>();
        var index = 0;

        foreach (var shift in shifts.Where(shift => shift.EndUtc > shift.StartUtc).OrderBy(shift => shift.StartUtc))
        {
            var cursor = shift.StartUtc;

            foreach (var segment in shift.Segments.Where(segment => segment.EndUtc > segment.StartUtc && segment.Kind != ActivityKind.Work).OrderBy(segment => segment.StartUtc))
            {
                var start = segment.StartUtc < shift.StartUtc ? shift.StartUtc : segment.StartUtc;
                var end = segment.EndUtc > shift.EndUtc ? shift.EndUtc : segment.EndUtc;

                if (end <= cursor)
                {
                    continue;
                }

                if (start < cursor)
                {
                    start = cursor;
                }

                if (start > cursor)
                {
                    pieces.Add(new AdherenceScheduledPiece { StartUtc = cursor, EndUtc = start, Kind = ActivityKind.Work, ActivityCodeId = shift.WorkActivityCodeId, Shift = index });
                }

                pieces.Add(new AdherenceScheduledPiece { StartUtc = start, EndUtc = end, Kind = segment.Kind, ActivityCodeId = segment.ActivityCodeId, Shift = index });
                cursor = end;
            }

            if (cursor < shift.EndUtc)
            {
                pieces.Add(new AdherenceScheduledPiece { StartUtc = cursor, EndUtc = shift.EndUtc, Kind = ActivityKind.Work, ActivityCodeId = shift.WorkActivityCodeId, Shift = index });
            }

            index++;
        }

        return pieces;
    }

    /// <summary>
    /// Compares the schedule with the actual states over a stretch of time.
    /// </summary>
    /// <param name="schedule">The scheduled pieces, in order.</param>
    /// <param name="actual">The agent's states. Time without one is signed out.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="exceptions">The exceptions.</param>
    /// <param name="fromUtc">The start of the stretch.</param>
    /// <param name="toUtc">The end of the stretch.</param>
    /// <returns>The segments and totals.</returns>
    public static AdherenceResult Compute(
        IReadOnlyList<AdherenceScheduledPiece> schedule,
        IEnumerable<AdherenceActual> actual,
        IReadOnlyList<AdherenceRule> rules,
        IEnumerable<AdherenceException> exceptions,
        DateTime fromUtc,
        DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(exceptions);

        var states = actual.Where(state => state.EndUtc > state.StartUtc).OrderBy(state => state.StartUtc).ToArray();
        var excepted = exceptions.Where(exception => exception.EndUtc > exception.StartUtc).ToArray();
        var result = new AdherenceResult();
        var pieces = new List<AdherenceSegment>();

        for (var i = 0; i < schedule.Count; i++)
        {
            var scheduled = schedule[i];
            var start = Max(scheduled.StartUtc, fromUtc);
            var end = Min(scheduled.EndUtc, toUtc);

            if (end <= start)
            {
                continue;
            }

            var rule = RuleFor(rules, scheduled.Kind, scheduled.ActivityCodeId);
            var previous = i > 0 && schedule[i - 1].Shift == scheduled.Shift && schedule[i - 1].EndUtc == scheduled.StartUtc ? schedule[i - 1] : null;
            var next = i < schedule.Count - 1 && schedule[i + 1].Shift == scheduled.Shift && schedule[i + 1].StartUtc == scheduled.EndUtc ? schedule[i + 1] : null;
            var startGraceEnd = previous is null ? scheduled.StartUtc : scheduled.StartUtc.AddMinutes(rule.StartGraceMinutes);
            var endGraceStart = next is null ? scheduled.EndUtc : scheduled.EndUtc.AddMinutes(-rule.EndGraceMinutes);

            var boundaries = new SortedSet<DateTime> { start, end };

            foreach (var state in states)
            {
                Add(boundaries, state.StartUtc, start, end);
                Add(boundaries, state.EndUtc, start, end);
            }

            foreach (var exception in excepted)
            {
                Add(boundaries, exception.StartUtc, start, end);
                Add(boundaries, exception.EndUtc, start, end);
            }

            Add(boundaries, startGraceEnd, start, end);
            Add(boundaries, endGraceStart, start, end);

            var points = boundaries.ToArray();

            for (var p = 0; p < points.Length - 1; p++)
            {
                var pieceStart = points[p];
                var pieceEnd = points[p + 1];
                var state = states.LastOrDefault(candidate => candidate.StartUtc <= pieceStart && candidate.EndUtc > pieceStart);
                var status = state?.Status ?? AgentPresenceStatus.Offline;
                var reason = state?.Reason;
                var inAdherence = rule.Allows(status, reason);

                if (!inAdherence && previous is not null && pieceStart < startGraceEnd &&
                    RuleFor(rules, previous.Kind, previous.ActivityCodeId).Allows(status, reason))
                {
                    inAdherence = true;
                }

                if (!inAdherence && next is not null && pieceStart >= endGraceStart &&
                    RuleFor(rules, next.Kind, next.ActivityCodeId).Allows(status, reason))
                {
                    inAdherence = true;
                }

                pieces.Add(new AdherenceSegment
                {
                    StartUtc = pieceStart,
                    EndUtc = pieceEnd,
                    Kind = scheduled.Kind,
                    ActivityCodeId = scheduled.ActivityCodeId,
                    Status = status,
                    Reason = reason,
                    InAdherence = inAdherence,
                    Excepted = excepted.Any(exception => exception.StartUtc <= pieceStart && exception.EndUtc >= pieceEnd),
                });
            }
        }

        ForgiveShortSlips(pieces, schedule, rules);

        foreach (var piece in pieces)
        {
            result.ScheduledSeconds += piece.Seconds;

            if (piece.Kind == ActivityKind.Work)
            {
                result.ScheduledWorkSeconds += piece.Seconds;
            }

            if (piece.Excepted)
            {
                result.ExceptionSeconds += piece.Seconds;
            }
            else if (piece.InAdherence)
            {
                result.InAdherenceSeconds += piece.Seconds;
            }
        }

        foreach (var state in states.Where(state => _workStates.Contains(state.Status)))
        {
            var start = Max(state.StartUtc, fromUtc);
            var end = Min(state.EndUtc, toUtc);

            if (end > start)
            {
                result.ActualWorkSeconds += (end - start).TotalSeconds;
            }
        }

        result.Segments = Merge(pieces);

        return result;
    }

    /// <summary>
    /// Tells whether an agent is in adherence right now, since when they are out, and whether to alert.
    /// </summary>
    /// <param name="schedule">The scheduled pieces, in order.</param>
    /// <param name="status">The agent's state.</param>
    /// <param name="reason">The state's reason.</param>
    /// <param name="statusSinceUtc">Since when the agent is in the state.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The current state.</returns>
    public static AdherenceCurrentState Current(
        IReadOnlyList<AdherenceScheduledPiece> schedule,
        AgentPresenceStatus status,
        string reason,
        DateTime? statusSinceUtc,
        IReadOnlyList<AdherenceRule> rules,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(rules);

        var index = -1;

        for (var i = 0; i < schedule.Count; i++)
        {
            if (schedule[i].StartUtc <= nowUtc && schedule[i].EndUtc > nowUtc)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return new AdherenceCurrentState { InAdherence = true };
        }

        var scheduled = schedule[index];
        var rule = RuleFor(rules, scheduled.Kind, scheduled.ActivityCodeId);

        if (rule.Allows(status, reason))
        {
            return new AdherenceCurrentState { Scheduled = scheduled, InAdherence = true };
        }

        var previous = index > 0 && schedule[index - 1].Shift == scheduled.Shift && schedule[index - 1].EndUtc == scheduled.StartUtc ? schedule[index - 1] : null;
        var next = index < schedule.Count - 1 && schedule[index + 1].Shift == scheduled.Shift && schedule[index + 1].StartUtc == scheduled.EndUtc ? schedule[index + 1] : null;
        var outSince = scheduled.StartUtc;

        if (previous is not null && RuleFor(rules, previous.Kind, previous.ActivityCodeId).Allows(status, reason))
        {
            outSince = scheduled.StartUtc.AddMinutes(rule.StartGraceMinutes);
        }

        if (statusSinceUtc > outSince)
        {
            outSince = statusSinceUtc.Value;
        }

        var inGrace = nowUtc < outSince ||
            (next is not null && nowUtc >= scheduled.EndUtc.AddMinutes(-rule.EndGraceMinutes) && RuleFor(rules, next.Kind, next.ActivityCodeId).Allows(status, reason));

        if (inGrace)
        {
            return new AdherenceCurrentState { Scheduled = scheduled, InAdherence = true };
        }

        return new AdherenceCurrentState
        {
            Scheduled = scheduled,
            InAdherence = false,
            OutSinceUtc = outSince,
            Alert = nowUtc - outSince >= TimeSpan.FromMinutes(Math.Max(rule.AlertAfterMinutes, rule.MinimumOutMinutes)),
        };
    }

    private static void ForgiveShortSlips(List<AdherenceSegment> pieces, IReadOnlyList<AdherenceScheduledPiece> schedule, IReadOnlyList<AdherenceRule> rules)
    {
        var i = 0;

        while (i < pieces.Count)
        {
            if (pieces[i].InAdherence || pieces[i].Excepted)
            {
                i++;
                continue;
            }

            var runEnd = i;

            while (runEnd + 1 < pieces.Count && !pieces[runEnd + 1].InAdherence && !pieces[runEnd + 1].Excepted && pieces[runEnd + 1].StartUtc == pieces[runEnd].EndUtc)
            {
                runEnd++;
            }

            var minimum = TimeSpan.FromMinutes(RuleFor(rules, pieces[i].Kind, pieces[i].ActivityCodeId).MinimumOutMinutes);

            if (pieces[runEnd].EndUtc - pieces[i].StartUtc < minimum)
            {
                for (var p = i; p <= runEnd; p++)
                {
                    pieces[p].InAdherence = true;
                }
            }

            i = runEnd + 1;
        }
    }

    private static List<AdherenceSegment> Merge(List<AdherenceSegment> pieces)
    {
        var merged = new List<AdherenceSegment>();

        foreach (var piece in pieces)
        {
            var last = merged.Count > 0 ? merged[^1] : null;

            if (last is not null &&
                last.EndUtc == piece.StartUtc &&
                last.Kind == piece.Kind &&
                string.Equals(last.ActivityCodeId, piece.ActivityCodeId, StringComparison.Ordinal) &&
                last.Status == piece.Status &&
                string.Equals(last.Reason, piece.Reason, StringComparison.Ordinal) &&
                last.InAdherence == piece.InAdherence &&
                last.Excepted == piece.Excepted)
            {
                last.EndUtc = piece.EndUtc;
            }
            else
            {
                merged.Add(piece);
            }
        }

        return merged;
    }

    private static void Add(SortedSet<DateTime> boundaries, DateTime value, DateTime start, DateTime end)
    {
        if (value > start && value < end)
        {
            boundaries.Add(value);
        }
    }

    private static DateTime Max(DateTime first, DateTime second) => first > second ? first : second;

    private static DateTime Min(DateTime first, DateTime second) => first < second ? first : second;
}
