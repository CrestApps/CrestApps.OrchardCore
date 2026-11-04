using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.OrTools;
using CrestApps.OrchardCore.OrTools.Services;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Tests.OrTools;

/// <summary>
/// The OR-Tools solvers on the built-in solvers' fixtures: at least as good, never breaking a hard limit. The limits and
/// plans are checked here independently of the solvers.
/// </summary>
public sealed class OrToolsSolverTests
{
    private static readonly GeoPoint _west = new(42.36, -71.30);
    private static readonly GeoPoint _east = new(42.36, -70.90);

    [Fact]
    public async Task Shifts_TheClinicSample_CoversAtLeastAsMuchAsTheBuiltIn_WithinTheLimits()
    {
        var problem = ShiftScheduleSolverTests.ClinicSample(weeks: 2);

        problem.TimeLimit = TimeSpan.FromSeconds(10);

        var builtIn = await new DefaultShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);
        var proposal = await new OrToolsShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);
        var (coverage, minimum) = ShiftScheduleSolverTests.Measure(problem, proposal);
        var (builtInCoverage, builtInMinimum) = ShiftScheduleSolverTests.Measure(problem, builtIn);

        TestContext.Current.TestOutputHelper?.WriteLine($"Target {builtInCoverage:P2} -> {coverage:P2}, minimum {builtInMinimum:P2} -> {minimum:P2}, proved {!proposal.TimedOut}, {proposal.Runtime.TotalSeconds:0.0}s");
        Assert.Equal(OrToolsConstants.SolverName, proposal.Solver);
        Assert.True(minimum >= builtInMinimum, $"OR-Tools covers {minimum:P2} of the minimum, the built-in {builtInMinimum:P2}.");
        Assert.True(coverage >= builtInCoverage, $"OR-Tools covers {coverage:P2} of the target, the built-in {builtInCoverage:P2}.");
        Assert.Equal(coverage, proposal.CoverageScore, 6);
        Assert.Equal(minimum, proposal.MinimumScore, 6);
        Assert.True(proposal.Runtime < TimeSpan.FromSeconds(15), $"The solver took {proposal.Runtime}.");
        ShiftScheduleSolverTests.AssertWithinLimits(problem, proposal);
    }

    [Fact]
    public async Task Shifts_KeepTheBuiltInsRules_OnSkillsAvailabilityFixedShiftsAndMaximums()
    {
        var solver = new OrToolsShiftScheduleSolver();
        var skilled = new ShiftScheduleProblem
        {
            Days = 1,
            Templates = [ShiftScheduleSolverTests.Template("forklift", 9 * 60, 8 * 60, skills: ["forklift"])],
            Workers =
            [
                ShiftScheduleSolverTests.Worker("a", days: [0]),
                ShiftScheduleSolverTests.Worker("b", days: [0], skills: ["forklift"]),
                ShiftScheduleSolverTests.Worker("c", days: [], skills: ["forklift"]),
            ],
            Demands = ShiftScheduleSolverTests.Demand(problem: null, day: 0, from: 9 * 60, to: 17 * 60, minimum: 1, target: 1),
        };
        var available = new ShiftScheduleProblem
        {
            Days = 1,
            Templates = [ShiftScheduleSolverTests.Template("day", 9 * 60, 8 * 60), ShiftScheduleSolverTests.Template("early", 6 * 60, 8 * 60)],
            Workers =
            [
                ShiftScheduleSolverTests.Worker("a", days: [0], windows: [new ShiftScheduleWindow(0, 12 * 60, AvailabilityPreference.Unavailable)]),
                ShiftScheduleSolverTests.Worker("b", days: [0], windows: [new ShiftScheduleWindow(6 * 60, (14 * 60) + 30)]),
            ],
            Demands = ShiftScheduleSolverTests.Demand(problem: null, day: 0, from: 9 * 60, to: 17 * 60, minimum: 1, target: 1),
        };
        var covered = new ShiftScheduleProblem
        {
            Days = 1,
            Templates = [ShiftScheduleSolverTests.Template("day", 9 * 60, 8 * 60)],
            Workers = [ShiftScheduleSolverTests.Worker("a", days: [0]), ShiftScheduleSolverTests.Worker("b", days: [0])],
            FixedShifts = [new ShiftScheduleFixedShift { WorkerId = "a", Start = 9 * 60, End = 17 * 60, PaidMinutes = 8 * 60 }],
            Demands = ShiftScheduleSolverTests.Demand(problem: null, day: 0, from: 9 * 60, to: 17 * 60, minimum: 1, target: 1),
        };
        var capped = new ShiftScheduleProblem
        {
            Days = 5,
            Templates = [ShiftScheduleSolverTests.Template("day", 9 * 60, 8 * 60)],
            Workers =
            [
                ShiftScheduleSolverTests.Worker("a", days: [0, 1, 2, 3, 4], maxMinutesPerWeek: 16 * 60),
                ShiftScheduleSolverTests.Worker("b", days: [0, 1, 2, 3, 4]),
            ],
        };

        for (var day = 0; day < 5; day++)
        {
            foreach (var demand in ShiftScheduleSolverTests.Demand(capped, day, 9 * 60, 17 * 60, minimum: 1, target: 1, maximum: 1))
            {
                capped.Demands.Add(demand);
            }
        }

        var token = TestContext.Current.CancellationToken;
        var skilledShifts = await solver.SolveAsync(skilled, token);
        var availableShifts = await solver.SolveAsync(available, token);
        var coveredShifts = await solver.SolveAsync(covered, token);
        var cappedShifts = await solver.SolveAsync(capped, token);

        Assert.Equal(new ProposedShift("b", 0, "forklift"), Assert.Single(skilledShifts.Shifts));
        Assert.Equal(new ProposedShift("b", 0, "early"), Assert.Single(availableShifts.Shifts));
        Assert.Empty(coveredShifts.Shifts);
        Assert.Equal(1, coveredShifts.CoverageScore, 6);
        Assert.Equal([0, 1, 2, 3, 4], cappedShifts.Shifts.Select(shift => shift.Day).Order());
        Assert.True(cappedShifts.Shifts.Count(shift => shift.WorkerId == "a") <= 2);

        foreach (var (problem, proposal) in new[] { (skilled, skilledShifts), (available, availableShifts), (covered, coveredShifts), (capped, cappedShifts) })
        {
            ShiftScheduleSolverTests.AssertWithinLimits(problem, proposal);
        }
    }

    [Fact]
    public async Task Dispatch_TheNaivePlan_TravelsNoMoreThanTheBuiltIn_AndKeepsFixedVisits()
    {
        var day = new DateTime(2026, 11, 9, 13, 0, 0, DateTimeKind.Utc);
        var open = new List<(DateTime, DateTime)> { (day, day.AddHours(8)) };
        var problem = new DispatchProblem
        {
            BufferMinutes = 5,
            TimeLimit = TimeSpan.FromSeconds(2),
            TravelMinutes = (from, to) => from is { } a && to is { } b ? (int)Math.Round(Math.Abs(a.Longitude - b.Longitude) * 100) : 0,
            Workers =
            [
                new ProblemWorker { Id = "west", Home = _west, Open = open },
                new ProblemWorker { Id = "east", Home = _east, Open = open },
            ],
            Visits =
            [
                new ProblemVisit { Id = "w1", Place = _west, DurationMinutes = 60, CurrentWorkerId = "east", CurrentStartUtc = day },
                new ProblemVisit { Id = "w2", Place = _west, DurationMinutes = 60, CurrentWorkerId = "east", CurrentStartUtc = day.AddHours(2) },
                new ProblemVisit { Id = "e1", Place = _east, DurationMinutes = 60, CurrentWorkerId = "west", CurrentStartUtc = day },
                new ProblemVisit { Id = "e2", Place = _east, DurationMinutes = 60, CurrentWorkerId = "west", CurrentStartUtc = day.AddHours(2) },
                new ProblemVisit { Id = "pinned", Place = _east, DurationMinutes = 30, FixedWorkerId = "west", FixedStartUtc = day.AddHours(6) },
                new ProblemVisit { Id = "new", Place = _west, DurationMinutes = 30 },
            ],
        };

        var builtIn = await new DefaultDispatchOptimizer().SolveAsync(problem, TestContext.Current.CancellationToken);
        var plan = await new OrToolsDispatchOptimizer().SolveAsync(problem, TestContext.Current.CancellationToken);
        var stops = plan.Stops.ToDictionary(stop => stop.VisitId);

        TestContext.Current.TestOutputHelper?.WriteLine($"Travel {builtIn.TravelMinutes} -> {plan.TravelMinutes}");
        Assert.Equal(OrToolsConstants.SolverName, plan.Solver);
        Assert.True(plan.TravelMinutes <= builtIn.TravelMinutes, $"OR-Tools travels {plan.TravelMinutes}, the built-in {builtIn.TravelMinutes}.");
        Assert.Empty(plan.Unassigned);
        Assert.Equal("west", stops["pinned"].WorkerId);
        Assert.Equal(day.AddHours(6), stops["pinned"].StartUtc);
        AssertFeasible(problem, plan);
    }

    [Fact]
    public async Task Dispatch_ABusyDay_CostsNoMoreThanTheBuiltIn_AndEveryRouteWorks()
    {
        var problem = BusyDay(seed: 7);

        var builtIn = await new DefaultDispatchOptimizer().SolveAsync(problem, TestContext.Current.CancellationToken);
        var plan = await new OrToolsDispatchOptimizer().SolveAsync(problem, TestContext.Current.CancellationToken);

        TestContext.Current.TestOutputHelper?.WriteLine($"Cost {Cost(problem, builtIn)} -> {Cost(problem, plan)}, left out {builtIn.Unassigned.Count} -> {plan.Unassigned.Count}, {plan.Runtime.TotalSeconds:0.0}s");
        Assert.True(Cost(problem, plan) <= Cost(problem, builtIn), $"OR-Tools costs {Cost(problem, plan)}, the built-in {Cost(problem, builtIn)}.");
        AssertFeasible(problem, plan);
        AssertFeasible(problem, builtIn);
    }

    // Three technicians with a lunch break, twenty visits around town, a third with an arrival window and some only for
    // the technician with the skill.
    private static DispatchProblem BusyDay(int seed)
    {
        var random = new Random(seed);
        var day = new DateTime(2026, 11, 9, 13, 0, 0, DateTimeKind.Utc);
        var open = new List<(DateTime, DateTime)> { (day, day.AddHours(4)), (day.AddHours(4.5), day.AddHours(8.5)) };
        var problem = new DispatchProblem
        {
            BufferMinutes = 10,
            TimeLimit = TimeSpan.FromSeconds(3),
            TravelMinutes = (from, to) => from is { } a && to is { } b ? (int)Math.Round((Math.Abs(a.Longitude - b.Longitude) + Math.Abs(a.Latitude - b.Latitude)) * 120) : 0,
            Workers =
            [
                new ProblemWorker { Id = "ana", Home = new GeoPoint(42.36, -71.06), Open = open },
                new ProblemWorker { Id = "ben", Home = new GeoPoint(42.25, -71.00), Open = open },
                new ProblemWorker { Id = "cy", Home = new GeoPoint(42.45, -71.20), Open = open },
            ],
        };

        for (var i = 0; i < 20; i++)
        {
            var visit = new ProblemVisit
            {
                Id = "v" + i,
                Place = new GeoPoint(42.2 + (random.NextDouble() * 0.3), -71.3 + (random.NextDouble() * 0.4)),
                DurationMinutes = 30 + (random.Next(4) * 15),
            };

            if (i % 3 == 0)
            {
                var from = day.AddHours(random.Next(6));

                visit.EarliestUtc = from;
                visit.LatestUtc = from.AddHours(3);
            }

            if (i % 5 == 0)
            {
                visit.EligibleWorkerIds = ["ben"];
            }

            problem.Visits.Add(visit);
        }

        return problem;
    }

    private static long Cost(DispatchProblem problem, DispatchPlan plan)
        => plan.TravelMinutes + problem.Visits.Where(visit => plan.Unassigned.Contains(visit.Id)).Sum(visit => (long)visit.UnassignedPenalty);

    // Walks each route: every visit is done by someone allowed, inside one open stretch and its window, after the trip
    // and the buffer from the stop before; fixed visits are where they were; the travel adds up.
    private static void AssertFeasible(DispatchProblem problem, DispatchPlan plan)
    {
        var visits = problem.Visits.ToDictionary(visit => visit.Id);
        var travel = 0;

        foreach (var worker in problem.Workers)
        {
            var place = worker.Home;
            DateTime? free = null;

            foreach (var stop in plan.Stops.Where(stop => stop.WorkerId == worker.Id).OrderBy(stop => stop.StartUtc))
            {
                var visit = visits[stop.VisitId];
                var end = stop.StartUtc.AddMinutes(visit.DurationMinutes);
                var trip = problem.TravelMinutes(place, visit.Place);

                Assert.True(visit.EligibleWorkerIds.Count == 0 || visit.EligibleWorkerIds.Contains(worker.Id), $"{worker.Id} can't do {visit.Id}.");
                Assert.Contains(worker.Open, span => span.StartUtc <= stop.StartUtc && span.EndUtc >= end);
                Assert.True(visit.EarliestUtc is null || stop.StartUtc >= visit.EarliestUtc, $"{visit.Id} starts before its window.");
                Assert.True(visit.LatestUtc is null || end <= visit.LatestUtc, $"{visit.Id} ends after its window.");
                Assert.True(free is null || stop.StartUtc >= free.Value.AddMinutes(problem.BufferMinutes + trip), $"{worker.Id} can't reach {visit.Id} in time.");

                if (visit.IsFixed)
                {
                    Assert.Equal(visit.FixedWorkerId, worker.Id);
                    Assert.Equal(visit.FixedStartUtc, stop.StartUtc);
                }

                Assert.Equal(trip, stop.TravelMinutes);
                travel += trip;
                place = visit.Place;
                free = end;
            }
        }

        Assert.Equal(travel, plan.TravelMinutes);
        Assert.Equal(problem.Visits.Count(visit => !visit.IsFixed), plan.Stops.Count(stop => !visits[stop.VisitId].IsFixed) + plan.Unassigned.Count);
    }
}
