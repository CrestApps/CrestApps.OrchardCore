using CrestApps.OrchardCore.Geo.Models;

namespace CrestApps.OrchardCore.Dispatch.Models;

/// <summary>
/// A technician in an optimization problem: where they start and when they can work.
/// </summary>
public sealed class ProblemWorker
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where they start the day.
    /// </summary>
    public GeoPoint? Home { get; set; }

    /// <summary>
    /// Gets or sets the spans they can work, earliest first.
    /// </summary>
    public IList<(DateTime StartUtc, DateTime EndUtc)> Open { get; set; } = [];
}

/// <summary>
/// A visit in an optimization problem.
/// </summary>
public sealed class ProblemVisit
{
    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets where the job is.
    /// </summary>
    public GeoPoint? Place { get; set; }

    /// <summary>
    /// Gets or sets how long it takes, in minutes.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    /// Gets or sets the earliest start, in UTC.
    /// </summary>
    public DateTime? EarliestUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest end, in UTC.
    /// </summary>
    public DateTime? LatestUtc { get; set; }

    /// <summary>
    /// Gets or sets the workers who may do it.
    /// </summary>
    public IList<string> EligibleWorkerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how much leaving it unassigned costs, in travel minutes; higher for urgent work.
    /// </summary>
    public int UnassignedPenalty { get; set; } = 1000;

    /// <summary>
    /// Gets or sets who does it now, for a visit that may move; the plan starts from there.
    /// </summary>
    public string CurrentWorkerId { get; set; }

    /// <summary>
    /// Gets or sets when it starts now, for a visit that may move.
    /// </summary>
    public DateTime? CurrentStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the worker it is fixed to, when it can't move, such as a pinned or started visit.
    /// </summary>
    public string FixedWorkerId { get; set; }

    /// <summary>
    /// Gets or sets the start it is fixed at, when it can't move.
    /// </summary>
    public DateTime? FixedStartUtc { get; set; }

    /// <summary>
    /// Gets whether it can't move.
    /// </summary>
    public bool IsFixed => FixedWorkerId is not null && FixedStartUtc is not null;
}

/// <summary>
/// A day's routing problem: technicians, visits, and the trips between places.
/// </summary>
public sealed class DispatchProblem
{
    /// <summary>
    /// Gets or sets the technicians.
    /// </summary>
    public IList<ProblemWorker> Workers { get; set; } = [];

    /// <summary>
    /// Gets or sets the visits.
    /// </summary>
    public IList<ProblemVisit> Visits { get; set; } = [];

    /// <summary>
    /// Gets or sets the minutes kept free next to each visit.
    /// </summary>
    public int BufferMinutes { get; set; }

    /// <summary>
    /// Gets or sets the trip in minutes between two places; 0 when one isn't known.
    /// </summary>
    public Func<GeoPoint?, GeoPoint?, int> TravelMinutes { get; set; } = (_, _) => 0;

    /// <summary>
    /// Gets or sets how long the solver may look for a better plan.
    /// </summary>
    public TimeSpan TimeLimit { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// A visit's place in a plan.
/// </summary>
/// <param name="VisitId">The visit.</param>
/// <param name="WorkerId">Who does it.</param>
/// <param name="StartUtc">When it starts.</param>
/// <param name="TravelMinutes">The trip to it from the stop before.</param>
public sealed record PlannedStop(string VisitId, string WorkerId, DateTime StartUtc, int TravelMinutes);

/// <summary>
/// A solver's answer: every visit's place, and the visits left out.
/// </summary>
public sealed class DispatchPlan
{
    /// <summary>
    /// Gets or sets the stops, by worker and time.
    /// </summary>
    public IList<PlannedStop> Stops { get; set; } = [];

    /// <summary>
    /// Gets or sets the visits that didn't fit.
    /// </summary>
    public IList<string> Unassigned { get; set; } = [];

    /// <summary>
    /// Gets or sets the plan's total travel, in minutes.
    /// </summary>
    public int TravelMinutes { get; set; }

    /// <summary>
    /// Gets or sets the solver's name.
    /// </summary>
    public string Solver { get; set; }

    /// <summary>
    /// Gets or sets how long the solver took.
    /// </summary>
    public TimeSpan Runtime { get; set; }
}

/// <summary>
/// One change a proposal makes to a visit.
/// </summary>
public sealed class ProposalMove
{
    /// <summary>
    /// Gets or sets the visit.
    /// </summary>
    public string VisitId { get; set; }

    /// <summary>
    /// Gets or sets the visit's number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the work order's summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets who does it now, if anyone.
    /// </summary>
    public string FromWorkerId { get; set; }

    /// <summary>
    /// Gets or sets when it starts now, if scheduled.
    /// </summary>
    public DateTime? FromStartUtc { get; set; }

    /// <summary>
    /// Gets or sets who would do it.
    /// </summary>
    public string ToWorkerId { get; set; }

    /// <summary>
    /// Gets or sets when it would start.
    /// </summary>
    public DateTime ToStartUtc { get; set; }
}

/// <summary>
/// A suggested rearrangement of a day, never applied on its own.
/// </summary>
public sealed class DispatchProposal
{
    /// <summary>
    /// Gets or sets the changes, in the order they apply.
    /// </summary>
    public IList<ProposalMove> Moves { get; set; } = [];

    /// <summary>
    /// Gets or sets the day's travel now, in minutes.
    /// </summary>
    public int TravelBefore { get; set; }

    /// <summary>
    /// Gets or sets the day's travel with the proposal, in minutes.
    /// </summary>
    public int TravelAfter { get; set; }

    /// <summary>
    /// Gets or sets the visits still left unscheduled.
    /// </summary>
    public IList<string> Unassigned { get; set; } = [];

    /// <summary>
    /// Gets or sets the solver's name.
    /// </summary>
    public string Solver { get; set; }
}

/// <summary>
/// What happened when moves were applied.
/// </summary>
public sealed class DispatchApplyResult
{
    /// <summary>
    /// Gets or sets the visits moved.
    /// </summary>
    public IList<string> Applied { get; set; } = [];

    /// <summary>
    /// Gets or sets why the others weren't, by visit.
    /// </summary>
    public IDictionary<string, string> Errors { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
