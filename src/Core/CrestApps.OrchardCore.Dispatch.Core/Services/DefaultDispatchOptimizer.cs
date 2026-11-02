using System.Diagnostics;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// The built-in solver: it starts from the plan given (visits where they are), fits in the visits left out by
/// cheapest insertion, then improves by moving a visit elsewhere, swapping two, and reversing part of a route, until
/// nothing helps or the time is up. Fixed visits never move. The same problem always gives the same plan.
/// </summary>
public sealed class DefaultDispatchOptimizer : IDispatchOptimizer
{
    /// <summary>
    /// The solver's name.
    /// </summary>
    public const string SolverName = "Built-in";

    /// <inheritdoc/>
    public string Name => SolverName;

    /// <inheritdoc/>
    public Task<DispatchPlan> SolveAsync(DispatchProblem problem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var watch = Stopwatch.StartNew();
        var solver = new Solver(problem);

        // Two starts: the day as it is, and a fresh one from the fixed visits alone, which gets out of the arrangements
        // where no single move helps (two technicians each holding the other's jobs). The cheaper one wins.
        var kept = solver.Start(keepCurrent: true);

        solver.InsertLeftOut(kept, cancellationToken);
        solver.Improve(kept, watch, cancellationToken);

        var fresh = solver.Start(keepCurrent: false);

        solver.InsertLeftOut(fresh, cancellationToken);
        solver.Improve(fresh, watch, cancellationToken);

        var plan = solver.ToPlan(solver.CostOf(fresh) < solver.CostOf(kept) - 0.0001 ? fresh : kept);

        plan.Solver = Name;
        plan.Runtime = watch.Elapsed;

        return Task.FromResult(plan);
    }

    /// <summary>
    /// Works out the times and travel of the plan a problem starts from, where every visit is as given.
    /// </summary>
    /// <param name="problem">The problem, with each visit's current worker and start in <paramref name="current"/>.</param>
    /// <param name="current">Each visit's worker and start now.</param>
    /// <returns>The plan's total travel, in minutes.</returns>
    public static int TravelOf(DispatchProblem problem, IReadOnlyDictionary<string, (string WorkerId, DateTime StartUtc)> current)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(current);

        var visits = problem.Visits.ToDictionary(visit => visit.Id, StringComparer.Ordinal);
        var total = 0;

        foreach (var worker in problem.Workers)
        {
            var stops = problem.Visits
                .Select(visit => (Visit: visit, Where: visit.IsFixed ? (visit.FixedWorkerId, visit.FixedStartUtc.Value) : current.TryGetValue(visit.Id, out var at) ? at : (null, DateTime.MinValue)))
                .Where(stop => stop.Where.Item1 == worker.Id)
                .OrderBy(stop => stop.Where.Item2)
                .Select(stop => stop.Visit)
                .ToList();
            var place = worker.Home;

            foreach (var visit in stops)
            {
                total += problem.TravelMinutes(place, visit.Place);
                place = visit.Place;
            }
        }

        return total;
    }

    private sealed class Solver
    {
        private readonly DispatchProblem _problem;
        private readonly Dictionary<string, ProblemWorker> _workers;
        private readonly Dictionary<string, ProblemVisit> _visits;

        public Solver(DispatchProblem problem)
        {
            _problem = problem;
            _workers = problem.Workers.ToDictionary(worker => worker.Id, StringComparer.Ordinal);
            _visits = problem.Visits.ToDictionary(visit => visit.Id, StringComparer.Ordinal);
        }

        // Each worker's route: their visits in the order they do them. Fixed visits start in their place by time.
        public Dictionary<string, List<ProblemVisit>> Start(bool keepCurrent)
        {
            var routes = _problem.Workers.ToDictionary(worker => worker.Id, _ => new List<ProblemVisit>(), StringComparer.Ordinal);

            foreach (var visit in _problem.Visits.Where(visit => visit.IsFixed && routes.ContainsKey(visit.FixedWorkerId)).OrderBy(visit => visit.FixedStartUtc))
            {
                routes[visit.FixedWorkerId].Add(visit);
            }

            // A movable visit that has a worker and start keeps them to begin with, when that still works.
            foreach (var visit in _problem.Visits.Where(visit => keepCurrent && !visit.IsFixed && visit.CurrentWorkerId is not null && routes.ContainsKey(visit.CurrentWorkerId)).OrderBy(visit => visit.CurrentStartUtc))
            {
                var route = routes[visit.CurrentWorkerId];
                var trial = route.Append(visit).OrderBy(item => item.FixedStartUtc ?? item.CurrentStartUtc ?? DateTime.MaxValue).ToList();

                if (Evaluate(visit.CurrentWorkerId, trial) is not null)
                {
                    routes[visit.CurrentWorkerId] = trial;
                }
            }

            return routes;
        }

        public void InsertLeftOut(Dictionary<string, List<ProblemVisit>> routes, CancellationToken cancellationToken)
        {
            var placed = routes.Values.SelectMany(route => route).Select(visit => visit.Id).ToHashSet(StringComparer.Ordinal);

            // The most costly to leave out first, then the tightest windows.
            foreach (var visit in _problem.Visits
                .Where(visit => !placed.Contains(visit.Id) && !visit.IsFixed)
                .OrderByDescending(visit => visit.UnassignedPenalty)
                .ThenBy(visit => visit.LatestUtc ?? DateTime.MaxValue)
                .ThenBy(visit => visit.Id, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var best = BestInsertion(routes, visit, null);

                if (best is { } insertion)
                {
                    routes[insertion.WorkerId].Insert(insertion.Index, visit);
                }
            }
        }

        public void Improve(Dictionary<string, List<ProblemVisit>> routes, Stopwatch watch, CancellationToken cancellationToken)
        {
            var improved = true;

            while (improved && watch.Elapsed < _problem.TimeLimit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                improved = Relocate(routes) || Swap(routes) || TwoOpt(routes);
            }
        }

        public double CostOf(Dictionary<string, List<ProblemVisit>> routes) => Cost(routes);

        public DispatchPlan ToPlan(Dictionary<string, List<ProblemVisit>> routes)
        {
            var plan = new DispatchPlan();

            foreach (var (workerId, route) in routes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var timed = Evaluate(workerId, route);

                if (timed is null)
                {
                    continue;
                }

                plan.TravelMinutes += timed.Value.Travel;

                foreach (var stop in timed.Value.Stops)
                {
                    plan.Stops.Add(stop);
                }
            }

            var placed = plan.Stops.Select(stop => stop.VisitId).ToHashSet(StringComparer.Ordinal);

            plan.Unassigned = [.. _problem.Visits.Where(visit => !visit.IsFixed && !placed.Contains(visit.Id)).Select(visit => visit.Id)];

            return plan;
        }

        private bool Relocate(Dictionary<string, List<ProblemVisit>> routes)
        {
            var before = Cost(routes);

            foreach (var (workerId, route) in routes.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToList())
            {
                foreach (var visit in route.Where(visit => !visit.IsFixed).ToList())
                {
                    var index = route.IndexOf(visit);

                    route.RemoveAt(index);

                    var best = BestInsertion(routes, visit, (workerId, index));

                    if (best is { } insertion && insertion.Cost < before - 0.0001)
                    {
                        routes[insertion.WorkerId].Insert(insertion.Index, visit);

                        return true;
                    }

                    route.Insert(index, visit);
                }
            }

            return false;
        }

        private bool Swap(Dictionary<string, List<ProblemVisit>> routes)
        {
            var before = Cost(routes);
            var keys = routes.Keys.Order(StringComparer.Ordinal).ToList();

            for (var a = 0; a < keys.Count; a++)
            {
                for (var b = a + 1; b < keys.Count; b++)
                {
                    var first = routes[keys[a]];
                    var second = routes[keys[b]];

                    for (var i = 0; i < first.Count; i++)
                    {
                        for (var j = 0; j < second.Count; j++)
                        {
                            var x = first[i];
                            var y = second[j];

                            if (x.IsFixed || y.IsFixed || !CanDo(keys[b], x) || !CanDo(keys[a], y))
                            {
                                continue;
                            }

                            first[i] = y;
                            second[j] = x;

                            if (Evaluate(keys[a], first) is not null && Evaluate(keys[b], second) is not null && Cost(routes) < before - 0.0001)
                            {
                                return true;
                            }

                            first[i] = x;
                            second[j] = y;
                        }
                    }
                }
            }

            return false;
        }

        private bool TwoOpt(Dictionary<string, List<ProblemVisit>> routes)
        {
            foreach (var (workerId, route) in routes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var before = Evaluate(workerId, route)?.Travel ?? int.MaxValue;

                for (var i = 0; i < route.Count - 1; i++)
                {
                    for (var j = i + 1; j < route.Count; j++)
                    {
                        if (route.Skip(i).Take(j - i + 1).Any(visit => visit.IsFixed))
                        {
                            continue;
                        }

                        route.Reverse(i, j - i + 1);

                        if (Evaluate(workerId, route) is { } after && after.Travel < before)
                        {
                            return true;
                        }

                        route.Reverse(i, j - i + 1);
                    }
                }
            }

            return false;
        }

        // The cheapest place to put a visit: the whole plan's cost with it there, or null when it fits nowhere.
        private (string WorkerId, int Index, double Cost)? BestInsertion(Dictionary<string, List<ProblemVisit>> routes, ProblemVisit visit, (string WorkerId, int Index)? skip)
        {
            (string WorkerId, int Index, double Cost)? best = null;
            var others = Cost(routes) - visit.UnassignedPenalty;

            foreach (var (workerId, route) in routes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (!CanDo(workerId, visit))
                {
                    continue;
                }

                var current = Evaluate(workerId, route)?.Travel ?? 0;

                for (var index = 0; index <= route.Count; index++)
                {
                    if (skip is { } same && same.WorkerId == workerId && same.Index == index)
                    {
                        continue;
                    }

                    route.Insert(index, visit);

                    var timed = Evaluate(workerId, route);

                    route.RemoveAt(index);

                    if (timed is null)
                    {
                        continue;
                    }

                    var cost = others - current + timed.Value.Travel;

                    if (best is null || cost < best.Value.Cost - 0.0001)
                    {
                        best = (workerId, index, cost);
                    }
                }
            }

            return best;
        }

        private static bool CanDo(string workerId, ProblemVisit visit)
            => visit.EligibleWorkerIds.Count == 0 || visit.EligibleWorkerIds.Contains(workerId);

        // Travel of every route, plus what leaving visits out costs.
        private double Cost(Dictionary<string, List<ProblemVisit>> routes)
        {
            var cost = 0d;
            var placed = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (workerId, route) in routes)
            {
                cost += Evaluate(workerId, route)?.Travel ?? 1_000_000;

                foreach (var visit in route)
                {
                    placed.Add(visit.Id);
                }
            }

            return cost + _problem.Visits.Where(visit => !visit.IsFixed && !placed.Contains(visit.Id)).Sum(visit => visit.UnassignedPenalty);
        }

        // Walks a route from the worker's home: each visit starts after the trip and the buffer, inside an open span
        // and its window; a fixed one must be reached by its start. Null when the route doesn't work.
        private (int Travel, List<PlannedStop> Stops)? Evaluate(string workerId, List<ProblemVisit> route)
        {
            var worker = _workers[workerId];
            var open = worker.Open.OrderBy(span => span.StartUtc).ToList();

            if (route.Count == 0)
            {
                return (0, []);
            }

            if (open.Count == 0)
            {
                return null;
            }

            var travel = 0;
            var stops = new List<PlannedStop>();
            var cursor = open[0].StartUtc;
            var place = worker.Home;
            var first = true;

            foreach (var visit in route)
            {
                var trip = _problem.TravelMinutes(place, visit.Place);
                var arrive = cursor.AddMinutes((first ? 0 : _problem.BufferMinutes) + trip);
                var duration = TimeSpan.FromMinutes(visit.DurationMinutes);
                DateTime start;

                if (visit.IsFixed)
                {
                    if (arrive > visit.FixedStartUtc.Value.AddMinutes(1))
                    {
                        return null;
                    }

                    start = visit.FixedStartUtc.Value;
                }
                else
                {
                    var earliest = visit.EarliestUtc is { } from && from > arrive ? from : arrive;
                    var fits = open.FirstOrDefault(span => span.EndUtc >= Max(earliest, span.StartUtc) + duration);

                    if (fits == default)
                    {
                        return null;
                    }

                    start = RoundUp(Max(earliest, fits.StartUtc));

                    if (start + duration > fits.EndUtc)
                    {
                        return null;
                    }
                }

                if (visit.LatestUtc is { } latest && start + duration > latest)
                {
                    return null;
                }

                travel += trip;
                stops.Add(new PlannedStop(visit.Id, workerId, start, trip));
                cursor = start + duration;
                place = visit.Place;
                first = false;
            }

            return (travel, stops);
        }

        private static DateTime Max(DateTime first, DateTime second) => first > second ? first : second;

        private static DateTime RoundUp(DateTime value)
        {
            var step = TimeSpan.FromMinutes(5).Ticks;

            return new DateTime((value.Ticks + step - 1) / step * step, value.Kind);
        }
    }
}
