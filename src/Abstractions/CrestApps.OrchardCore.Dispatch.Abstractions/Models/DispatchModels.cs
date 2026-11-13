namespace CrestApps.OrchardCore.Dispatch.Models;

/// <summary>
/// How much each thing counts when candidates for a visit are ranked. A weight of 0 leaves it out.
/// </summary>
public sealed class DispatchObjective
{
    /// <summary>
    /// Gets or sets the weight of a short trip to the visit.
    /// </summary>
    public int TravelWeight { get; set; } = 5;

    /// <summary>
    /// Gets or sets the weight of starting soon, before the work order is due.
    /// </summary>
    public int UrgencyWeight { get; set; } = 3;

    /// <summary>
    /// Gets or sets the weight of a skill level above the one needed.
    /// </summary>
    public int ProficiencyWeight { get; set; } = 2;

    /// <summary>
    /// Gets or sets the weight of a worker the work order prefers.
    /// </summary>
    public int PreferredWeight { get; set; } = 4;

    /// <summary>
    /// Gets or sets the weight of a worker with fewer visits that day.
    /// </summary>
    public int BalanceWeight { get; set; } = 2;
}

/// <summary>
/// The site's dispatch settings.
/// </summary>
public sealed class DispatchSettings
{
    /// <summary>
    /// Gets or sets whether a new work order gets an unscheduled visit.
    /// </summary>
    public bool CreateVisitsForNewWorkOrders { get; set; } = true;

    /// <summary>
    /// Gets or sets the length of the arrival window promised to the customer, in minutes.
    /// </summary>
    public int ArrivalWindowMinutes { get; set; } = 120;

    /// <summary>
    /// Gets or sets how many days ahead candidates are looked for.
    /// </summary>
    public int HorizonDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets the minutes kept free between two visits on top of the travel.
    /// </summary>
    public int BufferMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether a worker must belong to the visit's territory.
    /// </summary>
    public bool StrictTerritory { get; set; }

    /// <summary>
    /// Gets or sets the time zone the board shows; the site's when empty.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the capabilities of the staff dispatch sends: a staff member with any of them is a technician on
    /// the board, in suggestions, the optimizer, territories and the technician pickers. Empty means Technician.
    /// </summary>
    public List<string> TechnicianCapabilities { get; set; } = [];

    /// <summary>
    /// Gets the capabilities of the staff dispatch sends, Technician when none is set.
    /// </summary>
    /// <returns>The capability keys.</returns>
    public IReadOnlyList<string> GetTechnicianCapabilities()
        => TechnicianCapabilities is { Count: > 0 } ? TechnicianCapabilities : [DispatchConstants.TechnicianCapability];

    /// <summary>
    /// Gets or sets the weights candidates are ranked by.
    /// </summary>
    public DispatchObjective Objective { get; set; } = new();

    /// <summary>
    /// Gets or sets how many minutes late a visit is at risk: running over, or not started.
    /// </summary>
    public int AtRiskMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets how long the optimizer looks for a better plan, in seconds.
    /// </summary>
    public int OptimizerSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether customers are told about their visit.
    /// </summary>
    public bool NoticesEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether notices carry a link to follow the technician.
    /// </summary>
    public bool TrackingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the local hour the reminders go out the day before.
    /// </summary>
    public int ReminderHour { get; set; } = 17;

    /// <summary>
    /// Gets or sets the notice when a visit is scheduled or moved.
    /// </summary>
    public string ScheduledText { get; set; } = "{Company}: your visit {Number} is on {Date}, arriving between {Window}.";

    /// <summary>
    /// Gets or sets the reminder the day before.
    /// </summary>
    public string ReminderText { get; set; } = "{Company}: a reminder that we visit tomorrow, {Date}, arriving between {Window}.";

    /// <summary>
    /// Gets or sets the notice when the technician is on the way.
    /// </summary>
    public string EnRouteText { get; set; } = "{Company}: {Technician} is on the way, expected at {Eta}. Follow along: {TrackingUrl}";

    /// <summary>
    /// Gets or sets the notice when the visit is done.
    /// </summary>
    public string CompletedText { get; set; } = "{Company}: your visit {Number} is done. Thank you.";

    /// <summary>
    /// Gets or sets the notice when the visit is canceled.
    /// </summary>
    public string CanceledText { get; set; } = "{Company}: your visit {Number} on {Date} is canceled. We'll be in touch to find another time.";

    /// <summary>
    /// Gets or sets the work order type booked when a customer chooses an arrival window through Scheduling.
    /// </summary>
    public string BookingWorkOrderTypeId { get; set; }
}

/// <summary>
/// A worker who can take a visit, when, and why they rank where they do.
/// </summary>
public sealed class DispatchCandidate
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the earliest start that fits, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets the end at that start, in UTC.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the travel from the worker's stop before, in minutes.
    /// </summary>
    public int TravelMinutes { get; set; }

    /// <summary>
    /// Gets or sets the travel from the worker's stop before, in kilometers.
    /// </summary>
    public double TravelKm { get; set; }

    /// <summary>
    /// Gets or sets the score, higher is better.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Gets or sets how much each part of the objective added to the score.
    /// </summary>
    public Dictionary<string, double> ScoreParts { get; set; } = [];

    /// <summary>
    /// Gets or sets things to know that don't rule the worker out.
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// A worker ruled out for a visit, and why.
/// </summary>
/// <param name="WorkerId">The worker.</param>
/// <param name="Name">The worker's name.</param>
/// <param name="Reason">Why they can't take it.</param>
public sealed record DispatchExclusion(string WorkerId, string Name, string Reason);

/// <summary>
/// The candidates for a visit, best first, and the workers ruled out.
/// </summary>
public sealed class DispatchCandidateResult
{
    /// <summary>
    /// Gets or sets the candidates, best first.
    /// </summary>
    public IReadOnlyList<DispatchCandidate> Candidates { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers ruled out.
    /// </summary>
    public IReadOnlyList<DispatchExclusion> Excluded { get; set; } = [];

    /// <summary>
    /// Gets or sets why nobody was looked at, such as a work order that must be done another day.
    /// </summary>
    public string Message { get; set; }
}

/// <summary>
/// What a visit is given: its workers and its start.
/// </summary>
public sealed class VisitAssignment
{
    /// <summary>
    /// Gets or sets the workers, the lead first.
    /// </summary>
    public IList<string> WorkerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the start, in UTC.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets how long it takes, in minutes; the visit's own length when empty.
    /// </summary>
    public int? DurationMinutes { get; set; }

    /// <summary>
    /// Gets or sets whether the visit is pinned where it is put.
    /// </summary>
    public bool? Pinned { get; set; }
}

/// <summary>
/// The outcome of a change to a visit.
/// </summary>
public sealed class DispatchResult
{
    /// <summary>
    /// Gets or sets the visit as it is now.
    /// </summary>
    public ServiceVisit Visit { get; set; }

    /// <summary>
    /// Gets or sets what stopped the change.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets whether the change was made.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>
    /// Makes a failed result.
    /// </summary>
    /// <param name="error">What stopped the change.</param>
    /// <returns>The result.</returns>
    public static DispatchResult Fail(string error) => new() { Errors = [error] };
}
