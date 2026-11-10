using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.OrTools.Telemetry;

/// <summary>
/// The meter and activity source of the optimization solvers, <c>CrestApps.OrchardCore.Solver</c>: how long a solve
/// takes, how it ended and how big the problem was. The problem's ids, workers and visits go nowhere; its size is
/// recorded as a power-of-ten class.
/// </summary>
public static class SolverTelemetry
{
    /// <summary>
    /// The name of the solvers' meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Solver";

    /// <summary>
    /// The name of the span of one solve.
    /// </summary>
    public const string SolveSpanName = "crestapps.solver.solve";

    /// <summary>
    /// The tag that records which solver ran: <see cref="ShiftScheduleSolver"/> or <see cref="DispatchSolver"/>.
    /// </summary>
    public const string SolverTag = "solver";

    /// <summary>
    /// The tag that records how the search ended: <see cref="StatusOptimal"/>, <see cref="StatusFeasible"/>,
    /// <see cref="StatusTimedOut"/>, <see cref="StatusInfeasible"/>, <see cref="StatusCancelled"/> or
    /// <see cref="StatusSkipped"/>.
    /// </summary>
    public const string StatusTag = "status";

    /// <summary>
    /// The tag that records the power-of-ten class of the problem's size, from <see cref="GetSizeClass(long)"/>.
    /// </summary>
    public const string SizeTag = "size";

    /// <summary>
    /// The CP-SAT shift schedule solver. Its size is workers × days × shift templates: the yes-or-no choices of the model.
    /// </summary>
    public const string ShiftScheduleSolver = "shift_schedule";

    /// <summary>
    /// The routing solver of a dispatch day. Its size is the number of visits to place.
    /// </summary>
    public const string DispatchSolver = "dispatch";

    /// <summary>
    /// The search proved its answer optimal.
    /// </summary>
    public const string StatusOptimal = "optimal";

    /// <summary>
    /// The search found an answer without proving it optimal, such as a routing search that ran to its time limit.
    /// </summary>
    public const string StatusFeasible = "feasible";

    /// <summary>
    /// The search ran out of time before it found an answer.
    /// </summary>
    public const string StatusTimedOut = "timed_out";

    /// <summary>
    /// The search proved there is no answer, or the model was invalid.
    /// </summary>
    public const string StatusInfeasible = "infeasible";

    /// <summary>
    /// The search was stopped because the caller cancelled it.
    /// </summary>
    public const string StatusCancelled = CrestAppsOrchardCoreTelemetry.OutcomeCancelled;

    /// <summary>
    /// The problem had nothing to solve, so the built-in answer was returned without a search.
    /// </summary>
    public const string StatusSkipped = CrestAppsOrchardCoreTelemetry.OutcomeSkipped;

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/> of a solve whose own answer was returned.
    /// </summary>
    public const string ResultSolver = "solver";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/> of a solve that kept the built-in solver's answer,
    /// because the search found nothing or nothing better.
    /// </summary>
    public const string ResultBuiltIn = "builtin";

    /// <summary>
    /// The meter of the solvers.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Solver");

    /// <summary>
    /// The activity source of the solvers.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Solver");

    /// <summary>
    /// The duration of a solve, the built-in starting answer included, tagged with <see cref="SolverTag"/>,
    /// <see cref="StatusTag"/>, <see cref="SizeTag"/>, the <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/> and
    /// the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// </summary>
    public static Histogram<double> Duration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.solver.duration",
        "The duration of an optimization solve.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of solves, tagged as <see cref="Duration"/> is.
    /// </summary>
    public static Counter<long> Runs { get; } = Meter.CreateCounter<long>(
        "crestapps.solver.runs",
        "{run}",
        "The number of optimization solves, by how the search ended.");

    /// <summary>
    /// Starts measuring a solve.
    /// </summary>
    /// <param name="solver">The solver, <see cref="ShiftScheduleSolver"/> or <see cref="DispatchSolver"/>.</param>
    /// <param name="size">The size of the problem, in the solver's unit.</param>
    /// <returns>The run; set its status with <see cref="SolverRun.Complete(string, bool)"/> and dispose it when the solve ends.</returns>
    public static SolverRun Start(string solver, long size)
    {
        return new SolverRun(solver, GetSizeClass(size));
    }

    /// <summary>
    /// Gets the power-of-ten class of a problem's size, so problems of a similar size can be compared without the
    /// exact size becoming an unbounded tag.
    /// </summary>
    /// <param name="size">The size of the problem.</param>
    /// <returns><c>le_10</c>, <c>le_100</c>, <c>le_1000</c>, <c>le_10000</c>, <c>le_100000</c> or <c>gt_100000</c>.</returns>
    public static string GetSizeClass(long size)
    {
        return size switch
        {
            <= 10 => "le_10",
            <= 100 => "le_100",
            <= 1_000 => "le_1000",
            <= 10_000 => "le_10000",
            <= 100_000 => "le_100000",
            _ => "gt_100000",
        };
    }
}
