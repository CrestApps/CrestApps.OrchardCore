using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Models;

/// <summary>
/// What a scheduled activity allows: the agent states, optionally with given reasons, the grace at its start and end,
/// the shortest slip that counts and when to alert.
/// </summary>
public sealed class AdherenceRule
{
    /// <summary>
    /// Gets or sets the kind of activity the rule is for.
    /// </summary>
    public ActivityKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the activity code the rule is for, or <see langword="null"/> for every code of the kind.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the agent states that are in adherence.
    /// </summary>
    public List<AgentPresenceStatus> AllowedStatuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the reasons the state must have, or none for any reason.
    /// </summary>
    public List<string> AllowedReasons { get; set; } = [];

    /// <summary>
    /// Gets or sets how many minutes after the activity starts the agent may still be in the previous activity.
    /// </summary>
    public int StartGraceMinutes { get; set; } = 2;

    /// <summary>
    /// Gets or sets how many minutes before the activity ends the agent may already be in the next activity.
    /// </summary>
    public int EndGraceMinutes { get; set; } = 2;

    /// <summary>
    /// Gets or sets the shortest time out of adherence that counts, in minutes.
    /// </summary>
    public int MinimumOutMinutes { get; set; } = 1;

    /// <summary>
    /// Gets or sets how many minutes out of adherence raise an alert.
    /// </summary>
    public int AlertAfterMinutes { get; set; } = 5;

    /// <summary>
    /// Gets whether an agent state is in adherence with the activity.
    /// </summary>
    /// <param name="status">The state.</param>
    /// <param name="reason">The state's reason.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool Allows(AgentPresenceStatus status, string reason)
        => AllowedStatuses.Contains(status) &&
            (AllowedReasons.Count == 0 || (reason is not null && AllowedReasons.Contains(reason, StringComparer.OrdinalIgnoreCase)));
}

/// <summary>
/// A named set of adherence rules for some teams. A team without a rule set uses the default rules.
/// </summary>
public sealed class AdherenceRuleSet : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the teams that use it; their sub-teams use it too unless they have their own.
    /// </summary>
    public List<string> TeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the rules.
    /// </summary>
    public List<AdherenceRule> Rules { get; set; } = [];

    /// <summary>
    /// Gets or sets when the set was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the set was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// Why a stretch of a worker's schedule is left out of their adherence.
/// </summary>
public enum AdherenceExceptionReason
{
    /// <summary>
    /// A system the agent needed was down.
    /// </summary>
    SystemOutage,

    /// <summary>
    /// The agent was coached.
    /// </summary>
    Coaching,

    /// <summary>
    /// A manager approved a change to the schedule.
    /// </summary>
    ApprovedScheduleChange,

    /// <summary>
    /// Another reason, in the note.
    /// </summary>
    Other,
}

/// <summary>
/// A stretch of a worker's schedule that is left out of their adherence.
/// </summary>
public sealed class AdherenceException : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's team when the exception was recorded.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the reason.
    /// </summary>
    public AdherenceExceptionReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets who recorded it.
    /// </summary>
    public string RecordedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// A published shift as the adherence calculator reads it.
/// </summary>
public sealed class AdherenceShift
{
    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the activity code of the shift's work, when known.
    /// </summary>
    public string WorkActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the segments: breaks, meals, meetings and the rest. The time around them is work.
    /// </summary>
    public List<ShiftSegment> Segments { get; set; } = [];
}

/// <summary>
/// A stretch of the schedule with one activity.
/// </summary>
public sealed class AdherenceScheduledPiece
{
    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the kind of activity.
    /// </summary>
    public ActivityKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the activity code, when known.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the shift the piece belongs to, by its position in the schedule.
    /// </summary>
    public int Shift { get; set; }
}

/// <summary>
/// A stretch of an agent's actual state.
/// </summary>
public sealed class AdherenceActual
{
    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public AgentPresenceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the state's reason.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// A stretch of scheduled time with one activity, one actual state and one outcome.
/// </summary>
public sealed class AdherenceSegment
{
    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the scheduled kind of activity.
    /// </summary>
    public ActivityKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the scheduled activity code.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the actual state.
    /// </summary>
    public AgentPresenceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the actual state's reason.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets whether the stretch is in adherence.
    /// </summary>
    public bool InAdherence { get; set; }

    /// <summary>
    /// Gets or sets whether an exception covers the stretch.
    /// </summary>
    public bool Excepted { get; set; }

    /// <summary>
    /// Gets the stretch's length in seconds.
    /// </summary>
    public double Seconds => (EndUtc - StartUtc).TotalSeconds;
}

/// <summary>
/// What the adherence calculator found over a stretch of time.
/// </summary>
public sealed class AdherenceResult
{
    /// <summary>
    /// Gets or sets the segments of scheduled time, in order.
    /// </summary>
    public List<AdherenceSegment> Segments { get; set; } = [];

    /// <summary>
    /// Gets or sets the scheduled seconds, exceptions included.
    /// </summary>
    public double ScheduledSeconds { get; set; }

    /// <summary>
    /// Gets or sets the seconds in adherence.
    /// </summary>
    public double InAdherenceSeconds { get; set; }

    /// <summary>
    /// Gets or sets the scheduled seconds an exception covers.
    /// </summary>
    public double ExceptionSeconds { get; set; }

    /// <summary>
    /// Gets or sets the scheduled seconds of work.
    /// </summary>
    public double ScheduledWorkSeconds { get; set; }

    /// <summary>
    /// Gets or sets the seconds the agent was in a working state, scheduled or not.
    /// </summary>
    public double ActualWorkSeconds { get; set; }

    /// <summary>
    /// Gets the share of scheduled time, less exceptions, in adherence; <see langword="null"/> without scheduled time.
    /// </summary>
    public double? Adherence => ScheduledSeconds - ExceptionSeconds > 0 ? InAdherenceSeconds / (ScheduledSeconds - ExceptionSeconds) : null;

    /// <summary>
    /// Gets the worked time as a share of the scheduled work; <see langword="null"/> without scheduled work.
    /// </summary>
    public double? Conformance => ScheduledWorkSeconds > 0 ? ActualWorkSeconds / ScheduledWorkSeconds : null;
}

/// <summary>
/// An agent's adherence right now.
/// </summary>
public sealed class AdherenceCurrentState
{
    /// <summary>
    /// Gets or sets the activity scheduled now, or <see langword="null"/> when nothing is.
    /// </summary>
    public AdherenceScheduledPiece Scheduled { get; set; }

    /// <summary>
    /// Gets or sets whether the agent is in adherence.
    /// </summary>
    public bool InAdherence { get; set; }

    /// <summary>
    /// Gets or sets since when the agent is out of adherence.
    /// </summary>
    public DateTime? OutSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the agent has been out long enough to alert.
    /// </summary>
    public bool Alert { get; set; }
}
