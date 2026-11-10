using System.Diagnostics;
using System.Globalization;
using CrestApps.OrchardCore.OrTools.Telemetry;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Google.OrTools.Sat;

namespace CrestApps.OrchardCore.OrTools.Services;

/// <summary>
/// Schedules shifts with a CP-SAT model: a yes-or-no choice per worker, day and template, the same hard limits as the
/// built-in solver (one shift a day, skills, availability, rest, weekly hours, days in a row), and an objective that
/// covers the minimum first, then the target, without going over the maximum or adding hours nobody needs. It starts
/// from the built-in solver's schedule, so it never does worse, and returns that schedule if it finds nothing.
/// </summary>
public sealed class OrToolsShiftScheduleSolver : IShiftScheduleSolver
{
    // The objective, in whole numbers: an interval at its minimum counts most, then one at its target; going over a
    // maximum, an hour short of a weekly minimum and every interval paid for count against.
    private const int UnderMinimum = 100;
    private const int UnderTarget = 30;
    private const int OverMaximum = 40;
    private const int UnderWeeklyMinimumPerHour = 15;
    private const int PreferredBonus = 5;

    private readonly DefaultShiftScheduleSolver _builtIn = new();

    /// <inheritdoc/>
    public string Name => OrToolsConstants.SolverName;

    /// <inheritdoc/>
    public async Task<ShiftScheduleProposal> SolveAsync(ShiftScheduleProblem problem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        using var telemetry = SolverTelemetry.Start(SolverTelemetry.ShiftScheduleSolver, (long)problem.Workers.Count * Math.Max(0, problem.Days) * problem.Templates.Count);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var start = await _builtIn.SolveAsync(problem, cancellationToken);
            var left = problem.TimeLimit - stopwatch.Elapsed;

            if (problem.Demands.Count == 0 || problem.Workers.Count == 0 || problem.Templates.Count == 0)
            {
                telemetry.Complete(SolverTelemetry.StatusSkipped, usedSolverAnswer: false);

                return Finish(problem, start.Shifts, stopwatch, timedOut: false);
            }

            var model = new Model(problem);

            model.Build();
            model.Hint(start.Shifts);

            var solver = new CpSolver
            {
                StringParameters = string.Create(CultureInfo.InvariantCulture, $"max_time_in_seconds:{Math.Max(1, left.TotalSeconds):0.###} num_workers:{Math.Clamp(Environment.ProcessorCount, 1, 8)} random_seed:{problem.Seed}"),
            };

            CpSolverStatus status;

            using (cancellationToken.Register(solver.StopSearch))
            {
                status = await Task.Run(() => solver.Solve(model.Cp), CancellationToken.None);
            }

            if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
            {
                telemetry.Complete(GetStatus(status, cancellationToken), usedSolverAnswer: false);

                return Finish(problem, start.Shifts, stopwatch, timedOut: true);
            }

            telemetry.Complete(GetStatus(status, cancellationToken), usedSolverAnswer: true);

            return Finish(problem, model.Read(solver), stopwatch, timedOut: status != CpSolverStatus.Optimal);
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }
    }

    private static string GetStatus(CpSolverStatus status, CancellationToken cancellationToken)
    {
        return status switch
        {
            CpSolverStatus.Optimal => SolverTelemetry.StatusOptimal,
            CpSolverStatus.Feasible => SolverTelemetry.StatusFeasible,
            CpSolverStatus.Infeasible or CpSolverStatus.ModelInvalid => SolverTelemetry.StatusInfeasible,
            _ => cancellationToken.IsCancellationRequested ? SolverTelemetry.StatusCancelled : SolverTelemetry.StatusTimedOut,
        };
    }

    private ShiftScheduleProposal Finish(ShiftScheduleProblem problem, IList<ProposedShift> shifts, Stopwatch stopwatch, bool timedOut)
    {
        var (coverage, minimum) = Score(problem, shifts);

        return new ShiftScheduleProposal
        {
            Shifts = [.. shifts.OrderBy(shift => shift.Day).ThenBy(shift => shift.WorkerId, StringComparer.Ordinal)],
            CoverageScore = coverage,
            MinimumScore = minimum,
            Solver = Name,
            Runtime = stopwatch.Elapsed,
            TimedOut = timedOut,
        };
    }

    /// <summary>
    /// Works out the share of the target and of the minimum a schedule covers, as the built-in solver does.
    /// </summary>
    /// <param name="problem">The problem.</param>
    /// <param name="shifts">The proposed shifts.</param>
    /// <returns>The shares.</returns>
    internal static (double Coverage, double Minimum) Score(ShiftScheduleProblem problem, IEnumerable<ProposedShift> shifts)
    {
        var grid = new Grid(problem);
        var templates = problem.Templates.ToDictionary(template => template.Id, StringComparer.Ordinal);
        var workers = problem.Workers.ToDictionary(worker => worker.Id, StringComparer.Ordinal);
        var count = grid.FixedCounts();

        foreach (var shift in shifts)
        {
            if (!templates.TryGetValue(shift.TemplateId, out var template) || !workers.TryGetValue(shift.WorkerId, out var worker))
            {
                continue;
            }

            foreach (var cell in grid.CellsOf(shift.Day, template))
            {
                for (var group = 0; group < grid.Skills.Length; group++)
                {
                    if (grid.Qualifies(worker, group))
                    {
                        count[group][cell]++;
                    }
                }
            }
        }

        double target = 0, covered = 0, minimum = 0, minimumCovered = 0;

        for (var group = 0; group < grid.Skills.Length; group++)
        {
            for (var cell = 0; cell < grid.Cells; cell++)
            {
                target += grid.Target[group][cell];
                covered += Math.Min(count[group][cell], grid.Target[group][cell]);
                minimum += grid.Minimum[group][cell];
                minimumCovered += Math.Min(count[group][cell], grid.Minimum[group][cell]);
            }
        }

        return (target == 0 ? 1 : covered / target, minimum == 0 ? 1 : minimumCovered / minimum);
    }

    // The demand by group (everyone, then each skill) and interval, and the fixed shifts that already cover it.
    private sealed class Grid
    {
        private readonly ShiftScheduleProblem _problem;

        public Grid(ShiftScheduleProblem problem)
        {
            _problem = problem;
            Interval = problem.IntervalMinutes > 0 && 1440 % problem.IntervalMinutes == 0 ? problem.IntervalMinutes : 30;
            PerDay = 1440 / Interval;
            Cells = Math.Max(0, problem.Days) * PerDay;
            Skills = new string[] { null }
                .Concat(problem.Demands.Select(demand => demand.SkillId).Where(skill => !string.IsNullOrEmpty(skill)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                .ToArray();
            Minimum = New(0);
            Target = New(0);
            Maximum = New(-1);

            var groups = Skills.Select((skill, index) => (skill, index)).Where(pair => pair.skill is not null).ToDictionary(pair => pair.skill, pair => pair.index, StringComparer.Ordinal);

            foreach (var demand in problem.Demands.Where(demand => demand.Interval >= 0 && demand.Interval < Cells))
            {
                var group = string.IsNullOrEmpty(demand.SkillId) ? 0 : groups[demand.SkillId];

                Minimum[group][demand.Interval] += Math.Max(0, demand.Minimum);
                Target[group][demand.Interval] += Math.Max(0, demand.Target);

                if (demand.Maximum.HasValue)
                {
                    Maximum[group][demand.Interval] = Math.Max(0, Maximum[group][demand.Interval]) + Math.Max(0, demand.Maximum.Value);
                }
            }
        }

        public int Interval { get; }

        public int PerDay { get; }

        public int Cells { get; }

        public string[] Skills { get; }

        public int[][] Minimum { get; }

        public int[][] Target { get; }

        public int[][] Maximum { get; }

        public bool Qualifies(ShiftScheduleWorker worker, int group)
            => group == 0 || (worker?.SkillIds?.Contains(Skills[group]) ?? false);

        // The intervals a template covers on a day: whole intervals inside the shift, less its away times.
        public IEnumerable<int> CellsOf(int day, ShiftScheduleTemplate template)
        {
            var start = (day * 1440) + template.StartMinute;

            return CellsOf(start, start + Math.Max(0, template.LengthMinutes), template.Away.Select(away => (start + away.Start, start + away.End)));
        }

        public IEnumerable<int> CellsOf(int start, int end, IEnumerable<(int Start, int End)> away)
        {
            var gaps = away.ToArray();

            for (var cell = Math.Max(0, (start + Interval - 1) / Interval); (cell + 1) * Interval <= end && cell < Cells; cell++)
            {
                var from = cell * Interval;

                if (!gaps.Any(gap => gap.Start < from + Interval && gap.End > from))
                {
                    yield return cell;
                }
            }
        }

        public int[][] FixedCounts()
        {
            var count = New(0);
            var workers = _problem.Workers.ToDictionary(worker => worker.Id, StringComparer.Ordinal);

            foreach (var shift in _problem.FixedShifts.Where(shift => shift.CountsTowardCoverage))
            {
                var worker = shift.WorkerId is not null && workers.TryGetValue(shift.WorkerId, out var known) ? known : null;

                foreach (var cell in CellsOf(shift.Start, shift.End, shift.Away.Select(away => (away.Start, away.End))))
                {
                    for (var group = 0; group < Skills.Length; group++)
                    {
                        if (worker is null ? group == 0 : Qualifies(worker, group))
                        {
                            count[group][cell]++;
                        }
                    }
                }
            }

            return count;
        }

        private int[][] New(int value)
            => Skills.Select(_ => Enumerable.Repeat(value, Cells).ToArray()).ToArray();
    }

    private sealed record Choice(int Worker, int Day, int Template, BoolVar Var);

    private sealed class Model
    {
        private readonly ShiftScheduleProblem _problem;
        private readonly Grid _grid;
        private readonly List<Choice> _choices = [];
        private readonly Dictionary<(int Worker, int Day, int Template), BoolVar> _byKey = [];

        public Model(ShiftScheduleProblem problem)
        {
            _problem = problem;
            _grid = new Grid(problem);
        }

        public CpModel Cp { get; } = new();

        public void Build()
        {
            var templates = _problem.Templates;
            var objective = new List<(BoolVar Var, long Weight)>();
            var objectiveInts = new List<(IntVar Var, long Weight)>();
            var covering = new List<BoolVar>[_grid.Skills.Length][];

            for (var group = 0; group < covering.Length; group++)
            {
                covering[group] = new List<BoolVar>[_grid.Cells];
            }

            for (var w = 0; w < _problem.Workers.Count; w++)
            {
                var worker = _problem.Workers[w];
                var occupied = _problem.FixedShifts.Where(shift => shift.WorkerId == worker.Id).ToArray();
                var taken = occupied.Select(shift => shift.Start / 1440).ToHashSet();
                var minRest = Math.Max(0, worker.MinRestMinutes ?? _problem.MinRestMinutes);
                var maxConsecutive = Math.Max(1, worker.MaxConsecutiveDays ?? _problem.MaxConsecutiveDays);
                var windows = worker.Windows ?? [];
                var unavailable = windows.Where(window => window.Preference == AvailabilityPreference.Unavailable).ToArray();
                var available = windows.Where(window => window.Preference != AvailabilityPreference.Unavailable).GroupBy(window => window.Start / 1440).ToDictionary(group => group.Key, group => group.ToArray());
                var preferred = windows.Where(window => window.Preference == AvailabilityPreference.Preferred).ToArray();
                var dayVars = new List<BoolVar>[Math.Max(0, _problem.Days)];

                foreach (var day in (worker.Days ?? new HashSet<int>()).Where(day => day >= 0 && day < _problem.Days && !taken.Contains(day)))
                {
                    dayVars[day] = [];

                    for (var t = 0; t < templates.Count; t++)
                    {
                        var template = templates[t];
                        var start = (day * 1440) + template.StartMinute;
                        var end = start + template.LengthMinutes;

                        if (!template.RequiredSkillIds.All(skill => worker.SkillIds?.Contains(skill) ?? false)
                            || (worker.MaxMinutesPerDay > 0 && template.PaidMinutes > worker.MaxMinutesPerDay)
                            || unavailable.Any(window => window.Start < end && window.End > start)
                            || (available.TryGetValue(day, out var allowed) && !allowed.Any(window => window.Start <= start && window.End >= end))
                            || occupied.Any(shift => start < shift.End + minRest && shift.Start < end + minRest))
                        {
                            continue;
                        }

                        var choice = Cp.NewBoolVar($"x_{w}_{day}_{t}");

                        _choices.Add(new Choice(w, day, t, choice));
                        _byKey[(w, day, t)] = choice;
                        dayVars[day].Add(choice);

                        // Every interval paid for counts a little against, so nobody is scheduled for nothing.
                        objective.Add((choice, -Math.Max(1, template.PaidMinutes / _grid.Interval)));

                        if (preferred.Any(window => window.Start <= start && window.End >= end))
                        {
                            objective.Add((choice, PreferredBonus));
                        }

                        foreach (var cell in _grid.CellsOf(day, template))
                        {
                            for (var group = 0; group < _grid.Skills.Length; group++)
                            {
                                if (_grid.Qualifies(worker, group))
                                {
                                    (covering[group][cell] ??= []).Add(choice);
                                }
                            }
                        }
                    }

                    // One shift a day.
                    if (dayVars[day].Count > 1)
                    {
                        Cp.AddAtMostOne(dayVars[day]);
                    }
                }

                // Rest between shifts on nearby days.
                foreach (var first in _choices.Where(choice => choice.Worker == w))
                {
                    var firstEnd = (first.Day * 1440) + templates[first.Template].StartMinute + templates[first.Template].LengthMinutes;

                    foreach (var second in _choices.Where(choice => choice.Worker == w && choice.Day > first.Day && choice.Day <= first.Day + 2))
                    {
                        var secondStart = (second.Day * 1440) + templates[second.Template].StartMinute;

                        if (secondStart - firstEnd < minRest)
                        {
                            Cp.AddBoolOr([first.Var.Not(), second.Var.Not()]);
                        }
                    }
                }

                // Weekly hours: at most the maximum, and as near the minimum as can be.
                var weeks = Math.Max(1, (_problem.Days + 6) / 7);

                for (var week = 0; week < weeks; week++)
                {
                    var own = _choices.Where(choice => choice.Worker == w && choice.Day / 7 == week).ToArray();
                    var fixedPaid = occupied.Where(shift => shift.Start / 1440 / 7 == week).Sum(shift => shift.PaidMinutes);
                    var paid = LinearExpr.WeightedSum(own.Select(choice => choice.Var), own.Select(choice => (long)templates[choice.Template].PaidMinutes));

                    if (own.Length == 0)
                    {
                        continue;
                    }

                    if (worker.MaxMinutesPerWeek > 0)
                    {
                        Cp.Add(paid <= Math.Max(0, worker.MaxMinutesPerWeek - fixedPaid));
                    }

                    var needed = Math.Max(0, worker.MinMinutesPerWeek - fixedPaid);

                    if (needed > 0)
                    {
                        var shortHours = Cp.NewIntVar(0, (needed + 59) / 60, $"short_{w}_{week}");

                        Cp.Add((shortHours * 60) + paid >= needed);
                        objectiveInts.Add((shortHours, -UnderWeeklyMinimumPerHour));
                    }
                }

                // Days in a row.
                var works = Enumerable.Range(0, Math.Max(0, _problem.Days))
                    .Select(day => taken.Contains(day) ? LinearExpr.Constant(1) : dayVars[day] is { Count: > 0 } vars ? LinearExpr.Sum(vars) : LinearExpr.Constant(0))
                    .ToArray();

                for (var first = 0; first + maxConsecutive < works.Length; first++)
                {
                    var window = works.Skip(first).Take(maxConsecutive + 1).ToArray();

                    Cp.Add(LinearExpr.Sum(window) <= maxConsecutive);
                }
            }

            // Coverage: each interval counts up to its minimum and its target, and going over its maximum costs.
            var fixedCount = _grid.FixedCounts();

            for (var group = 0; group < _grid.Skills.Length; group++)
            {
                for (var cell = 0; cell < _grid.Cells; cell++)
                {
                    var vars = covering[group][cell];
                    var minimum = _grid.Minimum[group][cell];
                    var target = _grid.Target[group][cell];
                    var maximum = _grid.Maximum[group][cell];
                    var have = fixedCount[group][cell];

                    if (vars is null || vars.Count == 0)
                    {
                        continue;
                    }

                    var count = LinearExpr.Sum(vars) + have;

                    if (minimum > have)
                    {
                        var met = Cp.NewIntVar(0, minimum - have, $"m_{group}_{cell}");

                        Cp.Add(met + have <= count);
                        objectiveInts.Add((met, UnderMinimum));
                    }

                    if (target > have)
                    {
                        var met = Cp.NewIntVar(0, target - have, $"u_{group}_{cell}");

                        Cp.Add(met + have <= count);
                        objectiveInts.Add((met, UnderTarget));
                    }

                    if (maximum >= 0 && have + vars.Count > maximum)
                    {
                        var over = Cp.NewIntVar(0, have + vars.Count, $"o_{group}_{cell}");

                        Cp.Add(over >= count - maximum);
                        objectiveInts.Add((over, -OverMaximum));
                    }
                }
            }

            Cp.Maximize(LinearExpr.WeightedSum(objective.Select(item => item.Var), objective.Select(item => item.Weight))
                + LinearExpr.WeightedSum(objectiveInts.Select(item => item.Var), objectiveInts.Select(item => item.Weight)));
        }

        // Starts the search from a schedule, such as the built-in solver's.
        public void Hint(IEnumerable<ProposedShift> shifts)
        {
            var workers = _problem.Workers.Select((worker, index) => (worker.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);
            var templates = _problem.Templates.Select((template, index) => (template.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);
            var chosen = new HashSet<(int, int, int)>();

            foreach (var shift in shifts)
            {
                if (workers.TryGetValue(shift.WorkerId, out var worker) && templates.TryGetValue(shift.TemplateId, out var template))
                {
                    chosen.Add((worker, shift.Day, template));
                }
            }

            foreach (var choice in _choices)
            {
                Cp.AddHint(choice.Var, chosen.Contains((choice.Worker, choice.Day, choice.Template)) ? 1 : 0);
            }
        }

        public List<ProposedShift> Read(CpSolver solver)
            => [.. _choices
                .Where(choice => solver.BooleanValue(choice.Var))
                .Select(choice => new ProposedShift(_problem.Workers[choice.Worker].Id, choice.Day, _problem.Templates[choice.Template].Id))];
    }
}
