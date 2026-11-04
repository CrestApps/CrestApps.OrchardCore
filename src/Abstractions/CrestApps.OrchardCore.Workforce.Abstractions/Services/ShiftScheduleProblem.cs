namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A snapshot of what an <see cref="IShiftScheduleSolver"/> schedules. Times are minutes from midnight of the first
/// day on the team's wall clock; interval <c>i</c> runs from <c>i * IntervalMinutes</c>.
/// </summary>
public sealed class ShiftScheduleProblem
{
    /// <summary>
    /// Gets or sets how many days are scheduled.
    /// </summary>
    public int Days { get; set; }

    /// <summary>
    /// Gets or sets the length of each interval, which divides a day.
    /// </summary>
    public int IntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets how many people are needed, per skill and interval.
    /// </summary>
    public IList<ShiftScheduleDemand> Demands { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers who can be scheduled.
    /// </summary>
    public IList<ShiftScheduleWorker> Workers { get; set; } = [];

    /// <summary>
    /// Gets or sets the shifts that can be given.
    /// </summary>
    public IList<ShiftScheduleTemplate> Templates { get; set; } = [];

    /// <summary>
    /// Gets or sets the shifts already in the schedule, which count and stay.
    /// </summary>
    public IList<ShiftScheduleFixedShift> FixedShifts { get; set; } = [];

    /// <summary>
    /// Gets or sets the fewest minutes between the end of a worker's shift and the start of their next.
    /// </summary>
    public int MinRestMinutes { get; set; } = 600;

    /// <summary>
    /// Gets or sets the most days in a row a worker works.
    /// </summary>
    public int MaxConsecutiveDays { get; set; } = 6;

    /// <summary>
    /// Gets or sets how long the solver may run.
    /// </summary>
    public TimeSpan TimeLimit { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the seed that makes a run repeatable.
    /// </summary>
    public int Seed { get; set; } = 1;

    /// <summary>
    /// Gets how many intervals there are in a day.
    /// </summary>
    public int IntervalsPerDay => (24 * 60) / IntervalMinutes;
}

/// <summary>
/// How many people are needed in one interval, of everyone or of the holders of a skill.
/// </summary>
/// <param name="SkillId">The skill; <see langword="null"/> when anyone counts.</param>
/// <param name="Interval">The interval.</param>
/// <param name="Minimum">The fewest people needed.</param>
/// <param name="Target">The people wanted.</param>
/// <param name="Maximum">The most people useful, or <see langword="null"/>.</param>
public sealed record ShiftScheduleDemand(string SkillId, int Interval, int Minimum, int Target, int? Maximum = null);

/// <summary>
/// A window of time in minutes, absolute from the problem's start or relative to a shift's start.
/// </summary>
/// <param name="Start">The first minute.</param>
/// <param name="End">The minute after the last.</param>
/// <param name="Preference">Whether the worker is available, prefers it, or is unavailable.</param>
public sealed record ShiftScheduleWindow(int Start, int End, Models.AvailabilityPreference Preference = Models.AvailabilityPreference.Available);

/// <summary>
/// A worker who can be scheduled.
/// </summary>
public sealed class ShiftScheduleWorker
{
    /// <summary>
    /// Gets or sets the worker id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the skills the worker holds through the period.
    /// </summary>
    public ISet<string> SkillIds { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the days the worker may work: on the team, active and not off for the whole day.
    /// </summary>
    public ISet<int> Days { get; set; } = new HashSet<int>();

    /// <summary>
    /// Gets or sets the worker's availability on particular days, in absolute minutes. On a day with available or
    /// preferred windows, a shift must fit inside one of them; a shift must never overlap an unavailable window.
    /// </summary>
    public IList<ShiftScheduleWindow> Windows { get; set; } = [];

    /// <summary>
    /// Gets or sets the most paid minutes in each week of the period.
    /// </summary>
    public int MaxMinutesPerWeek { get; set; } = 40 * 60;

    /// <summary>
    /// Gets or sets the paid minutes the worker should reach each week; 0 for none.
    /// </summary>
    public int MinMinutesPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the most paid minutes in a day; 0 for no limit.
    /// </summary>
    public int MaxMinutesPerDay { get; set; }

    /// <summary>
    /// Gets or sets the fewest minutes between the worker's shifts, in place of the problem's; empty for the
    /// problem's.
    /// </summary>
    public int? MinRestMinutes { get; set; }

    /// <summary>
    /// Gets or sets the most days in a row the worker may work, in place of the problem's; empty for the problem's.
    /// </summary>
    public int? MaxConsecutiveDays { get; set; }
}

/// <summary>
/// A shift that can be given, as a start time of day and a length.
/// </summary>
public sealed class ShiftScheduleTemplate
{
    /// <summary>
    /// Gets or sets the template id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the start, in minutes from midnight.
    /// </summary>
    public int StartMinute { get; set; }

    /// <summary>
    /// Gets or sets the length in minutes, which may run past midnight.
    /// </summary>
    public int LengthMinutes { get; set; }

    /// <summary>
    /// Gets or sets the paid minutes, the length less unpaid breaks.
    /// </summary>
    public int PaidMinutes { get; set; }

    /// <summary>
    /// Gets or sets the breaks that don't count toward coverage, relative to the shift's start.
    /// </summary>
    public IList<ShiftScheduleWindow> Away { get; set; } = [];

    /// <summary>
    /// Gets or sets the skills a worker needs to work it.
    /// </summary>
    public ISet<string> RequiredSkillIds { get; set; } = new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>
/// A shift already in the schedule.
/// </summary>
public sealed class ShiftScheduleFixedShift
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the first minute.
    /// </summary>
    public int Start { get; set; }

    /// <summary>
    /// Gets or sets the minute after the last.
    /// </summary>
    public int End { get; set; }

    /// <summary>
    /// Gets or sets the paid minutes.
    /// </summary>
    public int PaidMinutes { get; set; }

    /// <summary>
    /// Gets or sets the breaks that don't count toward coverage, in absolute minutes.
    /// </summary>
    public IList<ShiftScheduleWindow> Away { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the shift counts toward the demand; a shift on another team only takes the worker's time.
    /// </summary>
    public bool CountsTowardCoverage { get; set; } = true;
}
