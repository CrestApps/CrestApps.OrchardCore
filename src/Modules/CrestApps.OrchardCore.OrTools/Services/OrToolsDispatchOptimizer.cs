using System.Diagnostics;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.OrTools.Telemetry;
using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;

namespace CrestApps.OrchardCore.OrTools.Services;

/// <summary>
/// Optimizes a dispatch day with the OR-Tools routing solver: one route per technician from their home base, a time
/// dimension for travel, buffers and visit lengths, each visit inside one of the technician's open stretches and its
/// window, only eligible technicians, fixed visits where they are, and a penalty for each visit left out. It starts
/// from the built-in optimizer's plan, so it never does worse, and keeps that plan when it finds nothing better.
/// </summary>
public sealed class OrToolsDispatchOptimizer : IDispatchOptimizer
{
    // Starts are on five-minute steps, as the built-in optimizer plans them.
    private const int Step = 5;

    private readonly DefaultDispatchOptimizer _builtIn = new();

    /// <inheritdoc/>
    public string Name => OrToolsConstants.SolverName;

    /// <inheritdoc/>
    public async Task<DispatchPlan> SolveAsync(DispatchProblem problem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        using var telemetry = SolverTelemetry.Start(SolverTelemetry.DispatchSolver, problem.Visits.Count);

        try
        {
            var watch = Stopwatch.StartNew();
            var start = await _builtIn.SolveAsync(problem, cancellationToken);
            var workers = problem.Workers.Where(worker => worker.Open.Count > 0).ToList();

            if (workers.Count == 0 || problem.Visits.Count == 0)
            {
                telemetry.Complete(SolverTelemetry.StatusSkipped, usedSolverAnswer: false);

                return Stamp(start, watch);
            }

            var left = problem.TimeLimit - watch.Elapsed;
            var (plan, status) = await Task.Run(() => new Routing(problem, workers).Solve(start, left < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : left, cancellationToken), CancellationToken.None);

            // The built-in plan stays when the solver finds nothing, or nothing cheaper.
            if (plan is null || Cost(problem, plan) > Cost(problem, start))
            {
                telemetry.Complete(GetStatus(status, plan is not null, cancellationToken), usedSolverAnswer: false);

                return Stamp(start, watch);
            }

            telemetry.Complete(GetStatus(status, found: true, cancellationToken), usedSolverAnswer: true);

            return Stamp(plan, watch);
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }
    }

    // A guided local search never proves its answer optimal and runs to its time limit, so an answer it found is
    // feasible unless the solver says it is optimal.
    private static string GetStatus(RoutingSearchStatus.Types.Value status, bool found, CancellationToken cancellationToken)
    {
        if (found)
        {
            return status == RoutingSearchStatus.Types.Value.RoutingOptimal
                ? SolverTelemetry.StatusOptimal
                : SolverTelemetry.StatusFeasible;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return SolverTelemetry.StatusCancelled;
        }

        return status is RoutingSearchStatus.Types.Value.RoutingInfeasible or RoutingSearchStatus.Types.Value.RoutingInvalid
            ? SolverTelemetry.StatusInfeasible
            : SolverTelemetry.StatusTimedOut;
    }

    private DispatchPlan Stamp(DispatchPlan plan, Stopwatch watch)
    {
        plan.Solver = Name;
        plan.Runtime = watch.Elapsed;

        return plan;
    }

    // What the built-in optimizer minimizes: travel, plus the penalty of every visit left out.
    private static long Cost(DispatchProblem problem, DispatchPlan plan)
    {
        var left = new HashSet<string>(plan.Unassigned, StringComparer.Ordinal);

        return plan.TravelMinutes + problem.Visits.Where(visit => left.Contains(visit.Id)).Sum(visit => (long)visit.UnassignedPenalty);
    }

    private sealed class Routing
    {
        private readonly DispatchProblem _problem;
        private readonly List<ProblemWorker> _workers;
        private readonly List<ProblemVisit> _visits;
        private readonly DateTime _origin;
        private readonly int _horizon;

        // Nodes: 0 is where every route ends (no trip back home is counted), then each technician's home, then the visits.
        private readonly GeoPoint?[] _places;
        private readonly int[] _service;

        public Routing(DispatchProblem problem, List<ProblemWorker> workers)
        {
            _problem = problem;
            _workers = workers;
            _visits = [.. problem.Visits];

            var times = workers.SelectMany(worker => worker.Open.SelectMany(span => new[] { span.StartUtc, span.EndUtc }))
                .Concat(_visits.Where(visit => visit.IsFixed).Select(visit => visit.FixedStartUtc.Value))
                .ToArray();

            _origin = times.Min().Date;
            _horizon = (int)Math.Ceiling((times.Max() - _origin).TotalMinutes) + (24 * 60);
            _places = [null, .. workers.Select(worker => worker.Home), .. _visits.Select(visit => visit.Place)];
            _service = [0, .. workers.Select(_ => 0), .. _visits.Select(visit => Math.Max(0, visit.DurationMinutes))];
        }

        private int FirstVisitNode => 1 + _workers.Count;

        public (DispatchPlan Plan, RoutingSearchStatus.Types.Value Status) Solve(DispatchPlan hint, TimeSpan limit, CancellationToken cancellationToken)
        {
            var nodes = FirstVisitNode + _visits.Count;
            var starts = Enumerable.Range(1, _workers.Count).ToArray();
            var ends = Enumerable.Repeat(0, _workers.Count).ToArray();
            var manager = new RoutingIndexManager(nodes, _workers.Count, starts, ends);
            var routing = new RoutingModel(manager);
            var travel = new int[nodes, nodes];

            for (var from = 0; from < nodes; from++)
            {
                for (var to = 0; to < nodes; to++)
                {
                    travel[from, to] = from == to || to == 0 || from == 0 ? 0 : Math.Max(0, _problem.TravelMinutes(_places[from], _places[to]));
                }
            }

            var cost = routing.RegisterTransitCallback((fromIndex, toIndex) => travel[manager.IndexToNode(fromIndex), manager.IndexToNode(toIndex)]);
            var time = routing.RegisterTransitCallback((fromIndex, toIndex) =>
            {
                var from = manager.IndexToNode(fromIndex);
                var to = manager.IndexToNode(toIndex);
                var buffer = from >= FirstVisitNode && to >= FirstVisitNode ? _problem.BufferMinutes : 0;

                return _service[from] + buffer + travel[from, to];
            });

            routing.SetArcCostEvaluatorOfAllVehicles(cost);
            routing.AddDimension(time, _horizon, _horizon, false, "Time");

            var dimension = routing.GetMutableDimension("Time");
            var solver = routing.solver();

            for (var v = 0; v < _workers.Count; v++)
            {
                var open = _workers[v].Open.OrderBy(span => span.StartUtc).ToList();
                var first = Minutes(open[0].StartUtc);
                var last = Minutes(open[^1].EndUtc);

                dimension.CumulVar(routing.Start(v)).SetRange(first, last);
                dimension.CumulVar(routing.End(v)).SetRange(first, last);

                // The gaps between open stretches, such as a break, take no visits.
                var breaks = new IntervalVarVector();

                for (var gap = 1; gap < open.Count; gap++)
                {
                    var from = Minutes(open[gap - 1].EndUtc);
                    var to = Minutes(open[gap].StartUtc);

                    if (to > from)
                    {
                        breaks.Add(solver.MakeFixedDurationIntervalVar(from, from, to - from, false, $"break_{v}_{gap}"));
                    }
                }

                if (breaks.Count > 0)
                {
                    dimension.SetBreakIntervalsOfVehicle(breaks, v, _service.Select(minutes => (long)minutes).ToArray());
                }
            }

            for (var i = 0; i < _visits.Count; i++)
            {
                var visit = _visits[i];
                var index = manager.NodeToIndex(FirstVisitNode + i);
                var cumul = dimension.CumulVar(index);

                if (visit.IsFixed)
                {
                    var vehicle = _workers.FindIndex(worker => worker.Id == visit.FixedWorkerId);

                    if (vehicle < 0)
                    {
                        routing.AddDisjunction([index], 0);

                        continue;
                    }

                    routing.VehicleVar(index).SetValue(vehicle);
                    cumul.SetValue(Minutes(visit.FixedStartUtc.Value));

                    continue;
                }

                var allowed = Enumerable.Range(0, _workers.Count)
                    .Where(v => visit.EligibleWorkerIds.Count == 0 || visit.EligibleWorkerIds.Contains(_workers[v].Id))
                    .ToArray();
                var startTimes = allowed
                    .SelectMany(v => Starts(_workers[v], visit))
                    .Distinct()
                    .Order()
                    .ToArray();

                // A visit nobody can fit is left out, and costs its penalty.
                routing.AddDisjunction([index], Math.Max(1, visit.UnassignedPenalty));

                // An empty list of vehicles would allow every one, so a visit nobody can do is switched off.
                if (allowed.Length == 0 || startTimes.Length == 0)
                {
                    routing.ActiveVar(index).SetValue(0);

                    continue;
                }

                // -1 keeps leaving it out possible.
                routing.VehicleVar(index).SetValues([-1, .. allowed.Select(v => (long)v)]);
                cumul.SetValues(startTimes);
            }

            var parameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();

            parameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
            parameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
            parameters.TimeLimit = Duration.FromTimeSpan(limit);

            routing.CloseModelWithParameters(parameters);

            using var stop = cancellationToken.Register(() => solver.FinishCurrentSearch());

            // Starting from the built-in plan, when the model accepts it.
            var initial = Read(hint, routing, manager);
            var solution = (initial is null ? null : routing.SolveFromAssignmentWithParameters(initial, parameters)) ?? routing.SolveWithParameters(parameters);

            return (solution is null ? null : ToPlan(solution, routing, manager, dimension), routing.GetStatus());
        }

        // The minutes from the origin each visit can start at on a technician's day: five-minute steps that fit one of
        // their open stretches and the visit's window.
        private IEnumerable<long> Starts(ProblemWorker worker, ProblemVisit visit)
        {
            foreach (var (startUtc, endUtc) in worker.Open)
            {
                var from = Math.Max(Minutes(startUtc), visit.EarliestUtc is { } earliest ? Minutes(earliest) : 0);
                var to = Minutes(endUtc) - visit.DurationMinutes;

                if (visit.LatestUtc is { } latest)
                {
                    to = Math.Min(to, Minutes(latest) - visit.DurationMinutes);
                }

                for (var minute = (from + Step - 1) / Step * Step; minute <= to; minute += Step)
                {
                    yield return minute;
                }
            }
        }

        private Assignment Read(DispatchPlan hint, RoutingModel routing, RoutingIndexManager manager)
        {
            var nodeOf = _visits.Select((visit, i) => (visit.Id, Node: FirstVisitNode + i)).ToDictionary(pair => pair.Id, pair => pair.Node, StringComparer.Ordinal);
            var routes = _workers
                .Select(worker => hint.Stops
                    .Where(stop => stop.WorkerId == worker.Id && nodeOf.ContainsKey(stop.VisitId))
                    .OrderBy(stop => stop.StartUtc)
                    .Select(stop => manager.NodeToIndex(nodeOf[stop.VisitId]))
                    .ToArray())
                .ToArray();

            return routing.ReadAssignmentFromRoutes(routes, true);
        }

        private DispatchPlan ToPlan(Assignment solution, RoutingModel routing, RoutingIndexManager manager, RoutingDimension dimension)
        {
            var plan = new DispatchPlan();
            var placed = new HashSet<int>();

            for (var v = 0; v < _workers.Count; v++)
            {
                var index = routing.Start(v);
                var previous = manager.IndexToNode(index);

                index = solution.Value(routing.NextVar(index));

                while (!routing.IsEnd(index))
                {
                    var node = manager.IndexToNode(index);
                    var trip = Math.Max(0, _problem.TravelMinutes(_places[previous], _places[node]));
                    var visit = _visits[node - FirstVisitNode];

                    plan.Stops.Add(new PlannedStop(visit.Id, _workers[v].Id, _origin.AddMinutes(solution.Min(dimension.CumulVar(index))), trip));
                    plan.TravelMinutes += trip;
                    placed.Add(node);
                    previous = node;
                    index = solution.Value(routing.NextVar(index));
                }
            }

            plan.Unassigned = [.. _visits.Where((visit, i) => !visit.IsFixed && !placed.Contains(FirstVisitNode + i)).Select(visit => visit.Id)];

            return plan;
        }

        private long Minutes(DateTime utc) => (long)Math.Round((utc - _origin).TotalMinutes);
    }
}
