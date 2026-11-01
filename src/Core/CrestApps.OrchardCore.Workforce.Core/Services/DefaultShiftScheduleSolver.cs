using System.Diagnostics;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The built-in <see cref="IShiftScheduleSolver"/>. A greedy construction fills the interval furthest below its need
/// with the shift that lowers the schedule's cost most; a local search then removes, retimes or hands over each shift
/// while that lowers the cost, and fills again. Both stop at the time limit. The cost weighs a person short of the
/// minimum far above one short of the target, then people over the maximum, people with nothing to cover, workers
/// below their weekly minimum and an uneven load, less a little for shifts inside a preferred window. Hard limits are
/// never broken: one shift a day, the worker's days, availability and skills, rest, weekly and daily hours, and days
/// in a row. Workers are tried in an order drawn from the seed, so a run is repeatable.
/// </summary>
internal sealed class DefaultShiftScheduleSolver : IShiftScheduleSolver
{
    public string Name => WorkforceConstants.DefaultSolver;

    public Task<ShiftScheduleProposal> SolveAsync(ShiftScheduleProblem problem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var stopwatch = Stopwatch.StartNew();
        var search = new Search(problem, stopwatch, cancellationToken);

        search.Run();
        stopwatch.Stop();

        var (coverage, minimum) = search.Score();

        return Task.FromResult(new ShiftScheduleProposal
        {
            Shifts = search.Proposed(),
            CoverageScore = coverage,
            MinimumScore = minimum,
            Solver = Name,
            Runtime = stopwatch.Elapsed,
            TimedOut = search.TimedOut,
        });
    }

    private sealed class Search
    {
        private const double UnderMinimum = 10;
        private const double UnderTarget = 3;
        private const double OverMaximum = 4;
        private const double Idle = 0.2;
        private const double UnderWeeklyMinimumPerHour = 0.5;
        private const double Load = 2;
        private const double Preferred = 0.5;
        private const double Epsilon = 1e-9;
        private const int MaxPasses = 50;

        private readonly ShiftScheduleProblem _problem;
        private readonly Stopwatch _stopwatch;
        private readonly CancellationToken _cancellationToken;
        private readonly int _interval;
        private readonly int _perDay;
        private readonly int _cells;
        private readonly int _weeks;

        // Groups: 0 is everyone, then one per skill with demand.
        private readonly string[] _skills;
        private readonly int[][] _minimum;
        private readonly int[][] _target;
        private readonly int[][] _maximum;
        private readonly int[][] _count;

        private readonly WorkerState[] _workers;
        private readonly TemplateState[] _templates;
        private readonly int[] _order;
        private readonly List<Assignment> _assignments = [];

        public Search(ShiftScheduleProblem problem, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            _problem = problem;
            _stopwatch = stopwatch;
            _cancellationToken = cancellationToken;
            _interval = problem.IntervalMinutes > 0 && 1440 % problem.IntervalMinutes == 0 ? problem.IntervalMinutes : 30;
            _perDay = 1440 / _interval;
            _cells = Math.Max(0, problem.Days) * _perDay;
            _weeks = Math.Max(1, (problem.Days + 6) / 7);

            _skills = new string[] { null }
                .Concat(problem.Demands.Select(demand => demand.SkillId).Where(skill => !string.IsNullOrEmpty(skill)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                .ToArray();
            _minimum = NewGrid(0);
            _target = NewGrid(0);
            _maximum = NewGrid(-1);
            _count = NewGrid(0);

            var groups = _skills.Select((skill, index) => (skill, index)).Where(pair => pair.skill is not null).ToDictionary(pair => pair.skill, pair => pair.index, StringComparer.Ordinal);

            foreach (var demand in problem.Demands)
            {
                if (demand.Interval < 0 || demand.Interval >= _cells)
                {
                    continue;
                }

                var group = string.IsNullOrEmpty(demand.SkillId) ? 0 : groups[demand.SkillId];

                _minimum[group][demand.Interval] += Math.Max(0, demand.Minimum);
                _target[group][demand.Interval] += Math.Max(0, demand.Target);

                if (demand.Maximum.HasValue)
                {
                    _maximum[group][demand.Interval] = Math.Max(0, _maximum[group][demand.Interval]) + Math.Max(0, demand.Maximum.Value);
                }
            }

            _templates = problem.Templates.Select(template => new TemplateState(template, _interval)).ToArray();
            _workers = problem.Workers.Select(worker => new WorkerState(worker, problem, _skills, _templates, _weeks)).ToArray();

            var random = new Random(problem.Seed);

            _order = Enumerable.Range(0, _workers.Length).OrderBy(_ => random.Next()).ToArray();

            var byId = Enumerable.Range(0, _workers.Length).ToDictionary(index => _workers[index].Id, StringComparer.Ordinal);

            foreach (var shift in problem.FixedShifts)
            {
                var worker = shift.WorkerId is not null && byId.TryGetValue(shift.WorkerId, out var index) ? _workers[index] : null;
                var day = shift.Start / 1440;

                for (var cell = FirstCell(shift.Start); shift.CountsTowardCoverage && (cell + 1) * _interval <= shift.End && cell < _cells; cell++)
                {
                    var from = cell * _interval;

                    if (cell < 0 || shift.Away.Any(away => away.Start < from + _interval && away.End > from))
                    {
                        continue;
                    }

                    for (var group = 0; group < _skills.Length; group++)
                    {
                        if (worker is null ? group == 0 : worker.Qualifies[group])
                        {
                            _count[group][cell]++;
                        }
                    }
                }

                if (worker is not null)
                {
                    worker.Occupied.Add((shift.Start, shift.End));
                    worker.AddPaid(day, shift.PaidMinutes);

                    if (day >= 0 && day < worker.Taken.Length)
                    {
                        worker.Taken[day] = true;
                    }
                }
            }
        }

        public bool TimedOut { get; private set; }

        public void Run()
        {
            if (_templates.Length == 0 || _workers.Length == 0)
            {
                return;
            }

            Construct();

            for (var pass = 0; pass < MaxPasses && !Expired(); pass++)
            {
                var changed = Improve();

                changed |= Construct();

                if (!changed)
                {
                    break;
                }
            }
        }

        public List<ProposedShift> Proposed()
        {
            return _assignments
                .Where(assignment => assignment.Active)
                .OrderBy(assignment => assignment.Day)
                .ThenBy(assignment => _workers[assignment.Worker].Id, StringComparer.Ordinal)
                .Select(assignment => new ProposedShift(_workers[assignment.Worker].Id, assignment.Day, _templates[assignment.Template].Id))
                .ToList();
        }

        public (double Coverage, double Minimum) Score()
        {
            double target = 0, covered = 0, minimum = 0, minimumCovered = 0;

            for (var group = 0; group < _skills.Length; group++)
            {
                for (var cell = 0; cell < _cells; cell++)
                {
                    target += _target[group][cell];
                    covered += Math.Min(_count[group][cell], _target[group][cell]);
                    minimum += _minimum[group][cell];
                    minimumCovered += Math.Min(_count[group][cell], _minimum[group][cell]);
                }
            }

            return (target == 0 ? 1 : covered / target, minimum == 0 ? 1 : minimumCovered / minimum);
        }

        /// <summary>
        /// Adds shifts while some interval is below its need and a shift lowers the cost. Returns whether it added any.
        /// </summary>
        private bool Construct()
        {
            var blocked = new HashSet<(int Group, int Cell)>();
            var added = false;

            while (!Expired())
            {
                var (group, cell) = MostNeeded(blocked);

                if (cell < 0)
                {
                    break;
                }

                var best = BestCovering(group, cell);

                if (best is null)
                {
                    blocked.Add((group, cell));

                    continue;
                }

                Add(best.Value.Worker, best.Value.Day, best.Value.Template);
                added = true;
            }

            return added;
        }

        /// <summary>
        /// Tries, for each shift, removing it, giving it another template, or giving it to another worker on the same
        /// day, and keeps the cheapest change that lowers the cost. Returns whether it changed anything.
        /// </summary>
        private bool Improve()
        {
            var changed = false;
            var random = new Random(_problem.Seed + _assignments.Count);
            var snapshot = _assignments.Where(assignment => assignment.Active).OrderBy(_ => random.Next()).ToArray();

            foreach (var assignment in snapshot)
            {
                if (Expired())
                {
                    break;
                }

                if (!assignment.Active)
                {
                    continue;
                }

                var (worker, day, template) = (assignment.Worker, assignment.Day, assignment.Template);
                var removal = Delta(worker, day, template, -1);

                Remove(assignment);

                var bestDelta = removal;
                (int Worker, int Template)? best = null;

                foreach (var other in _order)
                {
                    for (var candidate = 0; candidate < _templates.Length; candidate++)
                    {
                        if ((other == worker && candidate == template) || !Feasible(other, day, candidate))
                        {
                            continue;
                        }

                        var delta = removal + Delta(other, day, candidate, 1);

                        if (delta < bestDelta - Epsilon)
                        {
                            bestDelta = delta;
                            best = (other, candidate);
                        }
                    }
                }

                if (bestDelta >= -Epsilon)
                {
                    // Nothing beats keeping the shift as it was.
                    Add(worker, day, template);

                    continue;
                }

                if (best.HasValue)
                {
                    Add(best.Value.Worker, day, best.Value.Template);
                }

                changed = true;
            }

            return changed;
        }

        private (int Group, int Cell) MostNeeded(HashSet<(int Group, int Cell)> blocked)
        {
            var bestNeed = 0;
            var result = (Group: -1, Cell: -1);

            for (var group = 0; group < _skills.Length; group++)
            {
                for (var cell = 0; cell < _cells; cell++)
                {
                    var count = _count[group][cell];
                    var need = (Math.Max(0, _minimum[group][cell] - count) * 10) + (Math.Max(0, _target[group][cell] - count) * 3);

                    if (need > bestNeed && !blocked.Contains((group, cell)))
                    {
                        bestNeed = need;
                        result = (group, cell);
                    }
                }
            }

            return result;
        }

        private (int Worker, int Day, int Template)? BestCovering(int group, int cell)
        {
            var bestDelta = -Epsilon;
            (int Worker, int Day, int Template)? best = null;
            var cellDay = cell / _perDay;
            var slot = cell % _perDay;

            // A shift covering the interval starts that day or, past midnight, the day before.
            for (var day = cellDay; day >= Math.Max(0, cellDay - 1); day--)
            {
                var relative = slot + ((cellDay - day) * _perDay);

                for (var template = 0; template < _templates.Length; template++)
                {
                    if (!_templates[template].CoversRelative(relative))
                    {
                        continue;
                    }

                    foreach (var worker in _order)
                    {
                        if (!_workers[worker].Qualifies[group] || !Feasible(worker, day, template))
                        {
                            continue;
                        }

                        var delta = Delta(worker, day, template, 1);

                        if (delta < bestDelta)
                        {
                            bestDelta = delta;
                            best = (worker, day, template);
                        }
                    }
                }
            }

            return best;
        }

        private bool Feasible(int workerIndex, int day, int templateIndex)
        {
            var worker = _workers[workerIndex];
            var template = _templates[templateIndex];

            if (day < 0 || day >= _problem.Days || !worker.Allowed[templateIndex] || !worker.Days.Contains(day) || worker.Taken[day])
            {
                return false;
            }

            var start = (day * 1440) + template.Start;
            var end = start + template.Length;

            if (worker.Unavailable.Any(window => window.Start < end && window.End > start))
            {
                return false;
            }

            if (worker.Windows.TryGetValue(day, out var windows) && !windows.Any(window => window.Start <= start && window.End >= end))
            {
                return false;
            }

            foreach (var (otherStart, otherEnd) in worker.Occupied)
            {
                if (otherStart < end && otherEnd > start)
                {
                    return false;
                }

                if ((otherEnd <= start && start - otherEnd < worker.MinRest) || (otherStart >= end && otherStart - end < worker.MinRest))
                {
                    return false;
                }
            }

            if (worker.WeekPaid(day) + template.Paid > worker.MaxPerWeek || (worker.MaxPerDay > 0 && template.Paid > worker.MaxPerDay))
            {
                return false;
            }

            var run = 1;

            for (var before = day - 1; before >= 0 && worker.Taken[before]; before--)
            {
                run++;
            }

            for (var after = day + 1; after < worker.Taken.Length && worker.Taken[after]; after++)
            {
                run++;
            }

            return run <= worker.MaxConsecutive;
        }

        /// <summary>
        /// Returns how the cost changes when a shift is added (sign 1) or removed (sign -1).
        /// </summary>
        private double Delta(int workerIndex, int day, int templateIndex, int sign)
        {
            var worker = _workers[workerIndex];
            var template = _templates[templateIndex];
            var delta = 0.0;

            foreach (var relative in template.Covered)
            {
                var cell = (day * _perDay) + relative;

                if (cell >= _cells)
                {
                    break;
                }

                for (var group = 0; group < _skills.Length; group++)
                {
                    if (worker.Qualifies[group])
                    {
                        var count = _count[group][cell];

                        delta += CellCost(group, cell, count + sign) - CellCost(group, cell, count);
                    }
                }
            }

            var weekPaid = worker.WeekPaid(day);
            var under = Math.Max(0, worker.MinPerWeek - weekPaid);
            var underAfter = Math.Max(0, worker.MinPerWeek - (weekPaid + (sign * template.Paid)));

            delta += (underAfter - under) / 60.0 * UnderWeeklyMinimumPerHour;

            var paid = (double)worker.TotalPaid;
            var capacity = (double)Math.Max(1, worker.MaxPerWeek * _weeks);

            delta += Load * ((Math.Pow(paid + (sign * template.Paid), 2) - Math.Pow(paid, 2)) / (capacity * capacity));

            if (worker.Prefers(day, template))
            {
                delta -= sign * Preferred;
            }

            return delta;
        }

        private double CellCost(int group, int cell, int count)
        {
            var minimum = _minimum[group][cell];
            var target = _target[group][cell];
            var maximum = _maximum[group][cell];
            var cost = (Math.Max(0, minimum - count) * UnderMinimum) + (Math.Max(0, target - count) * UnderTarget);

            if (maximum >= 0 && count > maximum)
            {
                cost += (count - maximum) * OverMaximum;
            }

            if (group == 0 && count > target)
            {
                cost += (count - target) * Idle;
            }

            return cost;
        }

        private void Add(int workerIndex, int day, int templateIndex)
        {
            Apply(workerIndex, day, templateIndex, 1);
            _assignments.Add(new Assignment(workerIndex, day, templateIndex));
        }

        private void Remove(Assignment assignment)
        {
            Apply(assignment.Worker, assignment.Day, assignment.Template, -1);
            assignment.Active = false;
        }

        private void Apply(int workerIndex, int day, int templateIndex, int sign)
        {
            var worker = _workers[workerIndex];
            var template = _templates[templateIndex];
            var start = (day * 1440) + template.Start;

            foreach (var relative in template.Covered)
            {
                var cell = (day * _perDay) + relative;

                if (cell >= _cells)
                {
                    break;
                }

                for (var group = 0; group < _skills.Length; group++)
                {
                    if (worker.Qualifies[group])
                    {
                        _count[group][cell] += sign;
                    }
                }
            }

            worker.AddPaid(day, sign * template.Paid);
            worker.Taken[day] = sign > 0;

            if (sign > 0)
            {
                worker.Occupied.Add((start, start + template.Length));
            }
            else
            {
                worker.Occupied.Remove((start, start + template.Length));
            }
        }

        private bool Expired()
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (_stopwatch.Elapsed >= _problem.TimeLimit)
            {
                TimedOut = true;
            }

            return TimedOut;
        }

        private int FirstCell(int minute)
            => minute <= 0 ? 0 : (minute + _interval - 1) / _interval;

        private int[][] NewGrid(int value)
        {
            var grid = new int[_skills.Length][];

            for (var group = 0; group < grid.Length; group++)
            {
                grid[group] = new int[_cells];

                if (value != 0)
                {
                    Array.Fill(grid[group], value);
                }
            }

            return grid;
        }
    }

    private sealed class Assignment
    {
        public Assignment(int worker, int day, int template)
        {
            Worker = worker;
            Day = day;
            Template = template;
        }

        public int Worker { get; }

        public int Day { get; }

        public int Template { get; }

        public bool Active { get; set; } = true;
    }

    private sealed class TemplateState
    {
        private readonly bool[] _covers;

        public TemplateState(ShiftScheduleTemplate template, int interval)
        {
            Id = template.Id;
            Start = template.StartMinute;
            Length = Math.Max(0, template.LengthMinutes);
            Paid = Math.Max(0, template.PaidMinutes);
            Skills = template.RequiredSkillIds ?? new HashSet<string>(StringComparer.Ordinal);

            var covered = new List<int>();
            var end = Start + Length;

            for (var relative = (Start + interval - 1) / interval; (relative + 1) * interval <= end; relative++)
            {
                var from = (relative * interval) - Start;

                if (!template.Away.Any(away => away.Start < from + interval && away.End > from))
                {
                    covered.Add(relative);
                }
            }

            Covered = covered.ToArray();
            _covers = new bool[(end / interval) + 2];

            foreach (var relative in Covered)
            {
                _covers[relative] = true;
            }
        }

        public string Id { get; }

        public int Start { get; }

        public int Length { get; }

        public int Paid { get; }

        public ISet<string> Skills { get; }

        public int[] Covered { get; }

        public bool CoversRelative(int relative)
            => relative >= 0 && relative < _covers.Length && _covers[relative];
    }

    private sealed class WorkerState
    {
        private readonly int[] _weekPaid;
        private readonly ShiftScheduleWindow[] _preferred;

        public WorkerState(ShiftScheduleWorker worker, ShiftScheduleProblem problem, string[] skills, TemplateState[] templates, int weeks)
        {
            Id = worker.Id;
            Days = worker.Days ?? new HashSet<int>();
            Qualifies = skills.Select(skill => skill is null || (worker.SkillIds?.Contains(skill) ?? false)).ToArray();
            Allowed = templates.Select(template => template.Skills.All(skill => worker.SkillIds?.Contains(skill) ?? false)).ToArray();
            Taken = new bool[Math.Max(0, problem.Days)];
            MaxPerWeek = worker.MaxMinutesPerWeek > 0 ? worker.MaxMinutesPerWeek : int.MaxValue / 4;
            MinPerWeek = Math.Max(0, worker.MinMinutesPerWeek);
            MaxPerDay = Math.Max(0, worker.MaxMinutesPerDay);
            MinRest = Math.Max(0, worker.MinRestMinutes ?? problem.MinRestMinutes);
            MaxConsecutive = Math.Max(1, worker.MaxConsecutiveDays ?? problem.MaxConsecutiveDays);
            _weekPaid = new int[weeks + 1];

            var windows = worker.Windows ?? [];

            Unavailable = windows.Where(window => window.Preference == AvailabilityPreference.Unavailable).ToArray();
            Windows = windows
                .Where(window => window.Preference != AvailabilityPreference.Unavailable)
                .GroupBy(window => window.Start / 1440)
                .ToDictionary(group => group.Key, group => group.ToArray());
            _preferred = windows.Where(window => window.Preference == AvailabilityPreference.Preferred).ToArray();
        }

        public string Id { get; }

        public ISet<int> Days { get; }

        public bool[] Qualifies { get; }

        public bool[] Allowed { get; }

        public bool[] Taken { get; }

        public List<(int Start, int End)> Occupied { get; } = [];

        public ShiftScheduleWindow[] Unavailable { get; }

        public Dictionary<int, ShiftScheduleWindow[]> Windows { get; }

        public int MaxPerWeek { get; }

        public int MinPerWeek { get; }

        public int MaxPerDay { get; }

        public int MinRest { get; }

        public int MaxConsecutive { get; }

        public int TotalPaid { get; private set; }

        public int WeekPaid(int day)
            => day < 0 ? 0 : _weekPaid[Math.Min(day / 7, _weekPaid.Length - 1)];

        public void AddPaid(int day, int minutes)
        {
            if (day >= 0)
            {
                _weekPaid[Math.Min(day / 7, _weekPaid.Length - 1)] += minutes;
            }

            TotalPaid += minutes;
        }

        public bool Prefers(int day, TemplateState template)
        {
            if (_preferred.Length == 0)
            {
                return false;
            }

            var start = (day * 1440) + template.Start;
            var end = start + template.Length;

            return _preferred.Any(window => window.Start <= start && window.End >= end);
        }
    }
}
