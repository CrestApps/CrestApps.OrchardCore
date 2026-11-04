using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// Builds a day's routing problem from the visits as they are, asks the site's optimizer for a plan, and turns the
/// plan into moves; applying them goes through the dispatch service, so every move is locked and checked again.
/// </summary>
public sealed class DispatchOptimizationService : IDispatchOptimizationService
{
    private readonly IDispatchService _dispatch;
    private readonly IDispatchCandidateFinder _finder;
    private readonly DispatchResources _resources;
    private readonly WorkOrderStore _workOrders;
    private readonly IEnumerable<IDispatchOptimizer> _optimizers;
    private readonly IEnumerable<IGeoService> _geo;
    private readonly IDispatchSettingsProvider _settingsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchOptimizationService"/> class.
    /// </summary>
    public DispatchOptimizationService(
        IDispatchService dispatch,
        IDispatchCandidateFinder finder,
        DispatchResources resources,
        WorkOrderStore workOrders,
        IEnumerable<IDispatchOptimizer> optimizers,
        IEnumerable<IGeoService> geo,
        IDispatchSettingsProvider settingsProvider)
    {
        _dispatch = dispatch;
        _finder = finder;
        _resources = resources;
        _workOrders = workOrders;
        _optimizers = optimizers;
        _geo = geo;
        _settingsProvider = settingsProvider;
    }

    /// <inheritdoc/>
    public async Task<DispatchProposal> ProposeAsync(DateOnly day, IReadOnlyCollection<string> workerIds, bool includeUnscheduled, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsProvider.GetAsync();
        var zone = await _settingsProvider.GetTimeZoneIdAsync();

        zone = CalendarTime.IsValidZone(zone) ? zone : "UTC";

        var fromUtc = CalendarTime.ToUtc(day.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var problem = new DispatchProblem { BufferMinutes = settings.BufferMinutes, TimeLimit = TimeSpan.FromSeconds(Math.Clamp(settings.OptimizerSeconds, 1, 120)) };
        var workers = (await _resources.ListWorkersAsync(cancellationToken)).Where(worker => workerIds is null || workerIds.Contains(worker.ItemId)).ToList();
        var places = new Dictionary<string, GeoPoint?>(StringComparer.Ordinal);
        var current = new Dictionary<string, (string WorkerId, DateTime StartUtc)>(StringComparer.Ordinal);
        var visits = new Dictionary<string, ServiceVisit>(StringComparer.Ordinal);

        foreach (var worker in workers)
        {
            var availability = await _resources.GetAvailabilityAsync(worker, fromUtc, toUtc, null, cancellationToken);

            if (!availability.HasShift)
            {
                continue;
            }

            problem.Workers.Add(new ProblemWorker
            {
                Id = worker.ItemId,
                Name = worker.DisplayName,
                Home = availability.Home,
                Open = [.. availability.Open.Select(span => (span.StartUtc, span.EndUtc))],
            });
        }

        var ids = problem.Workers.Select(worker => worker.Id).ToHashSet(StringComparer.Ordinal);
        var scheduled = await _dispatch.ListScheduledAsync(fromUtc, toUtc, ids, cancellationToken);

        foreach (var visit in scheduled)
        {
            visits[visit.ItemId] = visit;

            var movable = VisitStatusRules.CanAssign(visit.Status) && !visit.Pinned && visit.WorkerIds.Count == 1 && ids.Contains(visit.LeadWorkerId);

            if (!movable)
            {
                // A crew, pinned or started visit stays; it fills the time of each of its workers here.
                foreach (var workerId in visit.WorkerIds.Where(ids.Contains))
                {
                    problem.Visits.Add(new ProblemVisit { Id = visit.ItemId + "#" + workerId, Place = await PlaceAsync(visit, places, cancellationToken), DurationMinutes = visit.DurationMinutes, FixedWorkerId = workerId, FixedStartUtc = visit.StartUtc });
                }

                continue;
            }

            current[visit.ItemId] = (visit.LeadWorkerId, visit.StartUtc.Value);
            problem.Visits.Add(await ToProblemAsync(visit, day, ids, places, settings, cancellationToken));
        }

        if (includeUnscheduled)
        {
            foreach (var visit in await _dispatch.ListUnscheduledAsync(200, cancellationToken))
            {
                var workOrder = await _workOrders.FindByIdAsync(visit.WorkOrderId, cancellationToken);

                if (visit.CrewSize > 1 || (workOrder?.LatestUtc is { } latest && latest <= fromUtc) || (workOrder?.EarliestUtc is { } earliest && earliest >= toUtc))
                {
                    continue;
                }

                visits[visit.ItemId] = visit;
                problem.Visits.Add(await ToProblemAsync(visit, day, ids, places, settings, cancellationToken));
            }
        }

        problem.TravelMinutes = await TravelTableAsync(problem, fromUtc, cancellationToken);

        var optimizer = _optimizers.LastOrDefault() ?? new DefaultDispatchOptimizer();
        var plan = await optimizer.SolveAsync(problem, cancellationToken);
        var proposal = new DispatchProposal
        {
            Solver = optimizer.Name,
            TravelBefore = DefaultDispatchOptimizer.TravelOf(problem, current),
            TravelAfter = plan.TravelMinutes,
            Unassigned = [.. plan.Unassigned],
        };

        foreach (var stop in plan.Stops.Where(stop => visits.ContainsKey(stop.VisitId)).OrderBy(stop => stop.StartUtc))
        {
            var visit = visits[stop.VisitId];
            var same = current.TryGetValue(stop.VisitId, out var now) && now.WorkerId == stop.WorkerId && Math.Abs((now.StartUtc - stop.StartUtc).TotalMinutes) < 5;

            if (same)
            {
                continue;
            }

            proposal.Moves.Add(new ProposalMove
            {
                VisitId = visit.ItemId,
                Number = visit.Number,
                Summary = visit.Summary,
                FromWorkerId = visit.LeadWorkerId,
                FromStartUtc = visit.StartUtc,
                ToWorkerId = stop.WorkerId,
                ToStartUtc = stop.StartUtc,
            });
        }

        return proposal;
    }

    /// <inheritdoc/>
    public async Task<DispatchApplyResult> ApplyAsync(IReadOnlyList<ProposalMove> moves, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var result = new DispatchApplyResult();
        var pending = moves.Where(move => !string.IsNullOrEmpty(move.VisitId)).OrderBy(move => move.ToStartUtc).ToList();

        // A move can wait for another to free its time; when nothing moves in a pass, one visit is unscheduled first
        // to break a swap, then tried again.
        while (pending.Count > 0)
        {
            var progress = false;

            foreach (var move in pending.ToList())
            {
                var assigned = await _dispatch.AssignAsync(move.VisitId, new VisitAssignment { WorkerIds = [move.ToWorkerId], StartUtc = move.ToStartUtc }, actor, cancellationToken);

                if (assigned.Succeeded)
                {
                    result.Applied.Add(move.VisitId);
                    result.Errors.Remove(move.VisitId);
                    pending.Remove(move);
                    progress = true;
                }
                else
                {
                    result.Errors[move.VisitId] = string.Join(" ", assigned.Errors);
                }
            }

            if (progress)
            {
                continue;
            }

            var stuck = pending.FirstOrDefault(move => move.FromStartUtc is not null && !result.Errors.ContainsKey("unscheduled:" + move.VisitId));

            if (stuck is null)
            {
                break;
            }

            result.Errors["unscheduled:" + stuck.VisitId] = string.Empty;

            if (!(await _dispatch.UnassignAsync(stuck.VisitId, actor, cancellationToken)).Succeeded)
            {
                break;
            }
        }

        foreach (var key in result.Errors.Keys.Where(key => key.StartsWith("unscheduled:", StringComparison.Ordinal)).ToList())
        {
            result.Errors.Remove(key);
        }

        return result;
    }

    private async Task<ProblemVisit> ToProblemAsync(ServiceVisit visit, DateOnly day, IReadOnlySet<string> workerIds, Dictionary<string, GeoPoint?> places, DispatchSettings settings, CancellationToken cancellationToken)
    {
        var workOrder = await _workOrders.FindByIdAsync(visit.WorkOrderId, cancellationToken);
        var eligible = new List<string>();

        foreach (var workerId in workerIds)
        {
            if (await _finder.CheckWorkerAsync(visit, workerId, day, cancellationToken) is null)
            {
                eligible.Add(workerId);
            }
        }

        return new ProblemVisit
        {
            Id = visit.ItemId,
            Place = await PlaceAsync(visit, places, cancellationToken),
            DurationMinutes = visit.DurationMinutes,
            EarliestUtc = workOrder?.EarliestUtc,
            LatestUtc = workOrder?.LatestUtc,
            EligibleWorkerIds = eligible.Count == 0 ? ["-"] : eligible,
            CurrentWorkerId = visit.StartUtc is null ? null : visit.LeadWorkerId,
            CurrentStartUtc = visit.StartUtc,

            // A visit already on someone's day must stay on one; urgent work is left out last.
            UnassignedPenalty = visit.StartUtc is not null ? 100_000 : 1000 * (1 + (int)visit.Priority),
        };
    }

    private async Task<GeoPoint?> PlaceAsync(ServiceVisit visit, Dictionary<string, GeoPoint?> places, CancellationToken cancellationToken)
    {
        if (!places.TryGetValue(visit.WorkOrderId, out var place))
        {
            place = await _resources.PlaceOfAsync(visit.WorkOrderId, cancellationToken);
            places[visit.WorkOrderId] = place;
        }

        return place;
    }

    // One matrix for every place in the problem, read back by pair.
    private async Task<Func<GeoPoint?, GeoPoint?, int>> TravelTableAsync(DispatchProblem problem, DateTime departureUtc, CancellationToken cancellationToken)
    {
        var points = problem.Workers.Select(worker => worker.Home)
            .Concat(problem.Visits.Select(visit => visit.Place))
            .Where(point => point is not null)
            .Select(point => point.Value)
            .Distinct()
            .ToList();
        var geo = _geo.FirstOrDefault();
        var table = new Dictionary<(GeoPoint, GeoPoint), int>();

        if (geo is not null && points.Count > 1)
        {
            var matrix = await geo.GetMatrixAsync(points, points, departureUtc, null, cancellationToken);

            for (var i = 0; i < points.Count; i++)
            {
                for (var j = 0; j < points.Count; j++)
                {
                    table[(points[i], points[j])] = (int)Math.Ceiling(matrix[i, j].Duration.TotalMinutes);
                }
            }
        }

        return (from, to) => from is { } a && to is { } b && table.TryGetValue((a, b), out var minutes) ? minutes : 0;
    }
}
