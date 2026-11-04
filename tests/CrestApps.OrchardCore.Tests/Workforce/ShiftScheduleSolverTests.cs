using System.Diagnostics;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The built-in shift schedule solver on problems built by hand: the clinic sample it must cover to 95% of the target
/// within the time limit, and the hard limits it must never break (one shift a day, rest, weekly hours, days in a row,
/// skills, availability and maximums). Coverage and the limits are checked here independently of the solver.
/// </summary>
public sealed class ShiftScheduleSolverTests
{
    private const string Lead = "lead";

    [Fact]
    public async Task Solve_TheClinicSample_CoversAtLeast95PercentOfTheTarget_WithinTheTimeLimit()
    {
        // Arrange
        var problem = ClinicSample(weeks: 2);
        var solver = new DefaultShiftScheduleSolver();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var proposal = await solver.SolveAsync(problem, TestContext.Current.CancellationToken);

        stopwatch.Stop();

        // Assert
        var (coverage, minimum) = Measure(problem, proposal);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"The solver took {stopwatch.Elapsed}.");
        Assert.True(coverage >= 0.95, $"The schedule covers {coverage:P1} of the target.");
        Assert.True(minimum >= 0.99, $"The schedule covers {minimum:P1} of the minimum.");
        Assert.Equal(coverage, proposal.CoverageScore, 6);
        Assert.Equal(minimum, proposal.MinimumScore, 6);
        Assert.Equal(WorkforceConstants.DefaultSolver, proposal.Solver);
        AssertWithinLimits(problem, proposal);
    }

    [Fact]
    public async Task Solve_FourWeeksOfTheClinicSample_StaysWithinTheTimeLimit_AndTheLimits()
    {
        // Arrange
        var problem = ClinicSample(weeks: 4);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var proposal = await new DefaultShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);

        stopwatch.Stop();

        // Assert
        var (coverage, _) = Measure(problem, proposal);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"The solver took {stopwatch.Elapsed}.");
        Assert.True(coverage >= 0.95, $"The schedule covers {coverage:P1} of the target.");
        AssertWithinLimits(problem, proposal);
    }

    [Fact]
    public async Task Solve_TheClinicSample_CoversTheLeadRequirementWithLeads()
    {
        // Arrange
        var problem = ClinicSample(weeks: 2);

        // Act
        var proposal = await new DefaultShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);

        // Assert (only w1, w2 and w3 hold the lead skill)
        var (coverage, _) = Measure(problem, proposal, Lead);

        Assert.True(coverage >= 0.9, $"The schedule covers {coverage:P1} of the lead target.");
    }

    [Fact]
    public async Task Solve_WithTheSameSeed_ProposesTheSameSchedule()
    {
        // Arrange
        var solver = new DefaultShiftScheduleSolver();

        // Act
        var first = await solver.SolveAsync(ClinicSample(weeks: 1), TestContext.Current.CancellationToken);
        var second = await solver.SolveAsync(ClinicSample(weeks: 1), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public async Task Solve_GivesASkilledShift_OnlyToAWorkerWhoHoldsTheSkill_AndWorksThatDay()
    {
        // Arrange (one forklift shift on day 0; a lacks the skill, c holds it but is off that day)
        var problem = new ShiftScheduleProblem
        {
            Days = 1,
            Templates = [Template("forklift", 9 * 60, 8 * 60, skills: ["forklift"])],
            Workers =
            [
                Worker("a", days: [0]),
                Worker("b", days: [0], skills: ["forklift"]),
                Worker("c", days: [], skills: ["forklift"]),
            ],
            Demands = Demand(problem: null, day: 0, from: 9 * 60, to: 17 * 60, minimum: 1, target: 1),
        };

        // Act
        var proposal = await new DefaultShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new ProposedShift("b", 0, "forklift"), Assert.Single(proposal.Shifts));
        Assert.Equal(1, proposal.CoverageScore, 6);
    }

    [Fact]
    public async Task Solve_KeepsShiftsOutOfUnavailableTimes_AndInsideAvailableWindows()
    {
        // Arrange (a can't work before noon; b is available 06:00-14:30 only; demand 09:00-17:00 needs one)
        var problem = new ShiftScheduleProblem
        {
            Days = 1,
            Templates = [Template("day", 9 * 60, 8 * 60), Template("early", 6 * 60, 8 * 60)],
            Workers =
            [
                Worker("a", days: [0], windows: [new ShiftScheduleWindow(0, 12 * 60, AvailabilityPreference.Unavailable)]),
                Worker("b", days: [0], windows: [new ShiftScheduleWindow(6 * 60, (14 * 60) + 30)]),
            ],
            Demands = Demand(problem: null, day: 0, from: 9 * 60, to: 17 * 60, minimum: 1, target: 1),
        };

        // Act
        var proposal = await new DefaultShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);

        // Assert (only b on the early shift fits: 06:00-14:00)
        Assert.Equal(new ProposedShift("b", 0, "early"), Assert.Single(proposal.Shifts));
        AssertWithinLimits(problem, proposal);
    }

    [Fact]
    public async Task Solve_CountsTheFixedShifts_AndAddsNothingWhenTheyCover()
    {
        // Arrange
        var problem = new ShiftScheduleProblem
        {
            Days = 1,
            Templates = [Template("day", 9 * 60, 8 * 60)],
            Workers = [Worker("a", days: [0]), Worker("b", days: [0])],
            FixedShifts = [new ShiftScheduleFixedShift { WorkerId = "a", Start = 9 * 60, End = 17 * 60, PaidMinutes = 8 * 60 }],
            Demands = Demand(problem: null, day: 0, from: 9 * 60, to: 17 * 60, minimum: 1, target: 1),
        };

        // Act
        var proposal = await new DefaultShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(proposal.Shifts);
        Assert.Equal(1, proposal.CoverageScore, 6);
    }

    [Fact]
    public async Task Solve_StaysWithinTheMaximum_AndTheWorkersWeeklyHours()
    {
        // Arrange (5 days need one person and allow one; a may work 16 hours a week, b none on day 0)
        var problem = new ShiftScheduleProblem
        {
            Days = 5,
            Templates = [Template("day", 9 * 60, 8 * 60)],
            Workers =
            [
                Worker("a", days: [0, 1, 2, 3, 4], maxMinutesPerWeek: 16 * 60),
                Worker("b", days: [0, 1, 2, 3, 4]),
            ],
        };

        for (var day = 0; day < 5; day++)
        {
            foreach (var demand in Demand(problem, day, 9 * 60, 17 * 60, minimum: 1, target: 1, maximum: 1))
            {
                problem.Demands.Add(demand);
            }
        }

        // Act
        var proposal = await new DefaultShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);

        // Assert (one shift a day, a works at most two of them)
        Assert.Equal([0, 1, 2, 3, 4], proposal.Shifts.Select(shift => shift.Day).Order());
        Assert.True(proposal.Shifts.Count(shift => shift.WorkerId == "a") <= 2);
        AssertWithinLimits(problem, proposal);
    }

    [Fact]
    public async Task Solve_WithNoDemand_ProposesNothing()
    {
        // Arrange
        var problem = ClinicSample(weeks: 1);

        problem.Demands.Clear();

        // Act
        var proposal = await new DefaultShiftScheduleSolver().SolveAsync(problem, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(proposal.Shifts);
        Assert.Equal(1, proposal.CoverageScore, 6);
    }

    /// <summary>
    /// Twelve workers (w1 to w3 are leads, w11 works only mornings on Mondays, w12 never weekends), four 8-hour
    /// templates (early, day, swing and late) with an unpaid 30-minute meal four hours in that doesn't count toward
    /// coverage, three people wanted 06:00-22:00 on weekdays (two at least), two 08:00-20:00 at weekends (one at
    /// least), and a lead 09:00-17:00 on weekdays.
    /// </summary>
    internal static ShiftScheduleProblem ClinicSample(int weeks)
    {
        var days = weeks * 7;
        var problem = new ShiftScheduleProblem
        {
            Days = days,
            IntervalMinutes = 30,
            MinRestMinutes = 600,
            MaxConsecutiveDays = 6,
            TimeLimit = TimeSpan.FromSeconds(30),
            Templates =
            [
                Template("early", 6 * 60, 8 * 60, mealOffset: 4 * 60),
                Template("day", 9 * 60, 8 * 60, mealOffset: 4 * 60),
                Template("swing", 12 * 60, 8 * 60, mealOffset: 4 * 60),
                Template("late", 14 * 60, 8 * 60, mealOffset: 4 * 60),
            ],
        };

        var everyDay = Enumerable.Range(0, days).ToArray();

        for (var number = 1; number <= 12; number++)
        {
            var worker = Worker("w" + number, number == 12 ? everyDay.Where(day => day % 7 < 5).ToArray() : everyDay, skills: number <= 3 ? [Lead] : []);

            if (number == 11)
            {
                for (var day = 0; day < days; day += 7)
                {
                    worker.Windows.Add(new ShiftScheduleWindow((day * 1440) + (6 * 60), (day * 1440) + (14 * 60)));
                }
            }

            problem.Workers.Add(worker);
        }

        for (var day = 0; day < days; day++)
        {
            var weekday = day % 7 < 5;
            var demands = weekday
                ? Demand(problem, day, 6 * 60, 22 * 60, minimum: 2, target: 3).Concat(Demand(problem, day, 9 * 60, 17 * 60, minimum: 1, target: 1, skill: Lead))
                : Demand(problem, day, 8 * 60, 20 * 60, minimum: 1, target: 2);

            foreach (var demand in demands)
            {
                problem.Demands.Add(demand);
            }
        }

        return problem;
    }

    internal static ShiftScheduleTemplate Template(string id, int start, int length, int? mealOffset = null, string[] skills = null)
    {
        var template = new ShiftScheduleTemplate
        {
            Id = id,
            StartMinute = start,
            LengthMinutes = length,
            PaidMinutes = mealOffset.HasValue ? length - 30 : length,
            RequiredSkillIds = new HashSet<string>(skills ?? [], StringComparer.Ordinal),
        };

        if (mealOffset.HasValue)
        {
            template.Away.Add(new ShiftScheduleWindow(mealOffset.Value, mealOffset.Value + 30));
        }

        return template;
    }

    internal static ShiftScheduleWorker Worker(string id, int[] days, string[] skills = null, ShiftScheduleWindow[] windows = null, int maxMinutesPerWeek = 40 * 60)
    {
        return new ShiftScheduleWorker
        {
            Id = id,
            Days = new HashSet<int>(days),
            SkillIds = new HashSet<string>(skills ?? [], StringComparer.Ordinal),
            Windows = [.. windows ?? []],
            MaxMinutesPerWeek = maxMinutesPerWeek,
        };
    }

    internal static List<ShiftScheduleDemand> Demand(ShiftScheduleProblem problem, int day, int from, int to, int minimum, int target, int? maximum = null, string skill = null)
    {
        var interval = problem?.IntervalMinutes ?? 30;
        var perDay = 1440 / interval;

        return Enumerable.Range(from / interval, (to - from) / interval)
            .Select(slot => new ShiftScheduleDemand(skill, (day * perDay) + slot, minimum, target, maximum))
            .ToList();
    }

    /// <summary>
    /// Works out, independently of the solver, the share of the target and of the minimum the fixed and proposed
    /// shifts cover, for every demand or only for those of a skill.
    /// </summary>
    internal static (double Coverage, double Minimum) Measure(ShiftScheduleProblem problem, ShiftScheduleProposal proposal, string skill = null)
    {
        var spans = Spans(problem, proposal);
        var workers = problem.Workers.ToDictionary(worker => worker.Id, StringComparer.Ordinal);
        var demands = problem.Demands.Where(demand => skill is null || demand.SkillId == skill).ToArray();
        var target = 0.0;
        var covered = 0.0;
        var minimum = 0.0;
        var minimumCovered = 0.0;

        foreach (var demand in demands)
        {
            var from = demand.Interval * problem.IntervalMinutes;
            var to = from + problem.IntervalMinutes;
            var count = spans.Count(span => span.Start <= from
                && span.End >= to
                && !span.Away.Any(away => away.Start < to && away.End > from)
                && (demand.SkillId is null || (workers.TryGetValue(span.WorkerId, out var worker) && worker.SkillIds.Contains(demand.SkillId))));

            target += demand.Target;
            covered += Math.Min(count, demand.Target);
            minimum += demand.Minimum;
            minimumCovered += Math.Min(count, demand.Minimum);
        }

        return (target == 0 ? 1 : covered / target, minimum == 0 ? 1 : minimumCovered / minimum);
    }

    /// <summary>
    /// Checks every hard limit the proposal must keep.
    /// </summary>
    internal static void AssertWithinLimits(ShiftScheduleProblem problem, ShiftScheduleProposal proposal)
    {
        var templates = problem.Templates.ToDictionary(template => template.Id, StringComparer.Ordinal);
        var spans = Spans(problem, proposal);

        foreach (var shift in proposal.Shifts)
        {
            var worker = problem.Workers.Single(worker => worker.Id == shift.WorkerId);
            var template = templates[shift.TemplateId];
            var start = (shift.Day * 1440) + template.StartMinute;
            var end = start + template.LengthMinutes;

            Assert.Contains(shift.Day, worker.Days);
            Assert.True(template.RequiredSkillIds.All(worker.SkillIds.Contains), $"{worker.Id} lacks a skill of {template.Id}.");
            Assert.DoesNotContain(worker.Windows, window => window.Preference == AvailabilityPreference.Unavailable && window.Start < end && window.End > start);

            var allowed = worker.Windows.Where(window => window.Preference != AvailabilityPreference.Unavailable && window.Start / 1440 == shift.Day).ToArray();

            Assert.True(allowed.Length == 0 || allowed.Any(window => window.Start <= start && window.End >= end), $"{worker.Id}'s shift on day {shift.Day} is outside their available hours.");
        }

        foreach (var worker in problem.Workers)
        {
            var own = spans.Where(span => span.WorkerId == worker.Id).OrderBy(span => span.Start).ToArray();

            Assert.Equal(own.Length, own.Select(span => span.Start / 1440).Distinct().Count());

            for (var index = 1; index < own.Length; index++)
            {
                Assert.True(own[index].Start - own[index - 1].End >= problem.MinRestMinutes, $"{worker.Id} rests less than {problem.MinRestMinutes} minutes before day {own[index].Start / 1440}.");
            }

            foreach (var week in own.GroupBy(span => span.Start / 1440 / 7))
            {
                Assert.True(week.Sum(span => span.Paid) <= worker.MaxMinutesPerWeek, $"{worker.Id} works more than allowed in week {week.Key}.");
            }

            var run = 0;
            var previous = int.MinValue;

            foreach (var day in own.Select(span => span.Start / 1440).Distinct().Order())
            {
                run = day == previous + 1 ? run + 1 : 1;
                previous = day;

                Assert.True(run <= problem.MaxConsecutiveDays, $"{worker.Id} works more than {problem.MaxConsecutiveDays} days in a row.");
            }
        }

        foreach (var demand in problem.Demands.Where(demand => demand.Maximum.HasValue))
        {
            var from = demand.Interval * problem.IntervalMinutes;
            var to = from + problem.IntervalMinutes;

            Assert.True(spans.Count(span => span.Start <= from && span.End >= to) <= demand.Maximum, $"Interval {demand.Interval} is over its maximum.");
        }
    }

    private static List<Span> Spans(ShiftScheduleProblem problem, ShiftScheduleProposal proposal)
    {
        var templates = problem.Templates.ToDictionary(template => template.Id, StringComparer.Ordinal);
        var spans = problem.FixedShifts
            .Where(shift => shift.WorkerId is not null)
            .Select(shift => new Span(shift.WorkerId, shift.Start, shift.End, shift.PaidMinutes, shift.Away.ToArray()))
            .ToList();

        foreach (var shift in proposal.Shifts)
        {
            var template = templates[shift.TemplateId];
            var start = (shift.Day * 1440) + template.StartMinute;

            spans.Add(new Span(shift.WorkerId, start, start + template.LengthMinutes, template.PaidMinutes, template.Away.Select(away => new ShiftScheduleWindow(start + away.Start, start + away.End)).ToArray()));
        }

        return spans;
    }

    private static string Describe(ShiftScheduleProposal proposal)
    {
        return string.Join(";", proposal.Shifts.OrderBy(shift => shift.Day).ThenBy(shift => shift.WorkerId, StringComparer.Ordinal).Select(shift => $"{shift.Day}:{shift.WorkerId}:{shift.TemplateId}"));
    }

    private sealed record Span(string WorkerId, int Start, int End, int Paid, ShiftScheduleWindow[] Away);
}
