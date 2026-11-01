using System.Globalization;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Labor rules on a real SQLite store: each built-in rule, which rule set applies to a worker, publishing stopped by a
/// rule that blocks, gone ahead with a reason for one that needs an override, or with a warning, the check before
/// publishing, predictability pay for late changes, and claims checked by the worker's rule set.
/// </summary>
public sealed class LaborRuleTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task MaxHoursPerDay_AndMaxHoursPerWeek_ReportTheDayAndTheWeek()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.MaxHoursPerDay, "Hours", "8"), (LaborRuleKeys.MaxHoursPerWeek, "Hours", "16"));

        var violations = await EvaluateAsync(harness, setup, Shift(_monday, 9, 19), Shift(_monday.AddDays(1), 9, 17));

        Assert.Equal([LaborRuleKeys.MaxHoursPerDay, LaborRuleKeys.MaxHoursPerWeek], violations.Select(violation => violation.RuleKey).Order());
        Assert.Equal(_monday, violations.Single(violation => violation.RuleKey == LaborRuleKeys.MaxHoursPerDay).Date);
    }

    [Fact]
    public async Task MinRest_ReportsShiftsTooCloseTogether()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.MinRest, "Hours", "11"));

        var close = await EvaluateAsync(harness, setup, Shift(_monday, 9, 17), Shift(_monday.AddDays(1), 3, 11));
        var apart = await EvaluateAsync(harness, setup, Shift(_monday, 9, 17), Shift(_monday.AddDays(1), 9, 17));

        var violation = Assert.Single(close);

        Assert.Equal(2, violation.ShiftKeys.Count);
        Assert.Empty(apart);
    }

    [Fact]
    public async Task MaxConsecutiveDays_AndDaysOffPerWeek_CountTheDaysWorked()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.MaxConsecutiveDays, "Days", "3"), (LaborRuleKeys.DaysOffPerWeek, "Days", "2"));

        var violations = await EvaluateAsync(harness, setup, Enumerable.Range(0, 6).Select(day => Shift(_monday.AddDays(day), 9, 13)).ToArray());

        Assert.Contains(violations, violation => violation.RuleKey == LaborRuleKeys.MaxConsecutiveDays);
        Assert.Contains(violations, violation => violation.RuleKey == LaborRuleKeys.DaysOffPerWeek);
    }

    [Fact]
    public async Task ShiftLength_Breaks_AndOvertime_LookAtEachShiftAndWeek()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(
            harness,
            (LaborRuleKeys.MaxShiftLength, "Hours", "10"),
            (LaborRuleKeys.MinShiftLength, "Hours", "4"),
            (LaborRuleKeys.Breaks, "ShiftHours", "6"),
            (LaborRuleKeys.Overtime, "WeeklyHours", "20"));
        var withBreak = Shift(_monday.AddDays(2), 9, 17);

        withBreak.BreakMinutes = 30;

        var violations = await EvaluateAsync(harness, setup, Shift(_monday, 6, 18), Shift(_monday.AddDays(1), 9, 12), withBreak);

        Assert.Single(violations, violation => violation.RuleKey == LaborRuleKeys.MaxShiftLength && violation.Date == _monday);
        Assert.Single(violations, violation => violation.RuleKey == LaborRuleKeys.MinShiftLength && violation.Date == _monday.AddDays(1));
        Assert.Single(violations, violation => violation.RuleKey == LaborRuleKeys.Breaks && violation.Date == _monday);
        Assert.Single(violations, violation => violation.RuleKey == LaborRuleKeys.Overtime);
    }

    [Fact]
    public async Task MinHoursPerWeek_WarnsWhenTheWeekIsShort()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.MinHoursPerWeek, "Hours", "30"));

        var violations = await EvaluateAsync(harness, setup, Shift(_monday, 9, 17), Shift(_monday.AddDays(1), 9, 17));

        Assert.Equal(LaborRuleKeys.MinHoursPerWeek, Assert.Single(violations).RuleKey);
    }

    [Fact]
    public async Task MinorLimits_ApplyOnlyToYoungWorkers()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.MinorLimits, "LatestEnd", "22:00"));
        var minor = await PlanningKit.AddMemberAsync(harness, "Teo", setup.Team, worker => worker.DateOfBirth = new DateOnly(2010, 5, 1));

        var young = await EvaluateAsync(harness, setup with { Worker = minor }, Shift(_monday, 15, 23));
        var adult = await EvaluateAsync(harness, setup, Shift(_monday, 15, 23));

        Assert.Equal(LaborRuleKeys.MinorLimits, Assert.Single(young).RuleKey);
        Assert.Empty(adult);
    }

    [Fact]
    public async Task ScheduleNotice_ReportsTheEmployersLateChanges_NotTheWorkersOwn()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.ScheduleNotice, "Days", "14"));
        var shift = Shift(new DateOnly(2026, 11, 5), 9, 17);

        var late = await EvaluateAsync(harness, setup, [shift], new LaborRuleChange { Kind = ScheduleChangeKind.Added, ShiftKey = shift.ShiftKey, AfterStartUtc = shift.StartUtc });
        var asked = await EvaluateAsync(harness, setup, [shift], new LaborRuleChange { Kind = ScheduleChangeKind.Added, ShiftKey = shift.ShiftKey, AfterStartUtc = shift.StartUtc, InitiatedBy = ScheduleChangeInitiator.Worker });

        Assert.Equal(LaborRuleKeys.ScheduleNotice, Assert.Single(late).RuleKey);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task SkillsRequired_AndNoWorkDuringTimeOff_LookAtTheWorkersRecords()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.SkillsRequired, null, null), (LaborRuleKeys.NoWorkDuringTimeOff, null, null));
        var skill = await PlanningKit.AddSkillAsync(harness, "Forklift");
        var vacation = await TimeOffKit.AddTypeAsync(harness, "Vacation", type => type.RequiresBalance = false);
        var monday = Shift(_monday, 9, 17);

        monday.RequiredSkillIds = [skill.ItemId];

        var request = await TimeOffKit.SubmitAsync(harness, setup.Worker, vacation, _monday.AddDays(1));

        await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(request.Value.ItemId, "manager"));

        var violations = await EvaluateAsync(harness, setup, monday, Shift(_monday.AddDays(1), 9, 17));

        await PlanningKit.GrantSkillAsync(harness, setup.Worker.ItemId, skill.ItemId);

        var afterGrant = await EvaluateAsync(harness, setup, monday);

        Assert.Single(violations, violation => violation.RuleKey == LaborRuleKeys.SkillsRequired && violation.Date == _monday);
        Assert.Single(violations, violation => violation.RuleKey == LaborRuleKeys.NoWorkDuringTimeOff && violation.Date == _monday.AddDays(1));
        Assert.Empty(afterGrant);
    }

    [Fact]
    public async Task RuleSet_ForTheWorkerAlone_BeatsTheNearestTeamsSet_ForTheirEmploymentType()
    {
        await using var harness = await CreateAsync();
        var region = await WorkforceTests.AddTeamAsync(harness, "East", TeamKind.Region);
        var site = await WorkforceTests.AddTeamAsync(harness, "Boston", TeamKind.Site, region.ItemId);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", site);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", site, worker => worker.EmploymentType = EmploymentType.PartTime);
        var cy = await PlanningKit.AddMemberAsync(harness, "Cy", site);
        var regional = await AddRuleSetAsync(harness, "Regional", set => set.TeamIds.Add(region.ItemId));
        var partTime = await AddRuleSetAsync(harness, "Boston part-time", set =>
        {
            set.TeamIds.Add(site.ItemId);
            set.EmploymentTypes.Add(EmploymentType.PartTime);
        });
        var personal = await AddRuleSetAsync(harness, "Cy's", set => set.WorkerIds.Add(cy.ItemId));

        var resolved = await harness.RunAsync(async services =>
        {
            var evaluator = services.GetRequiredService<ILaborRuleEvaluator>();

            return new[]
            {
                (await evaluator.ResolveRuleSetAsync(ana, site.ItemId))?.ItemId,
                (await evaluator.ResolveRuleSetAsync(ben, site.ItemId))?.ItemId,
                (await evaluator.ResolveRuleSetAsync(cy, site.ItemId))?.ItemId,
            };
        }, commit: false);

        Assert.Equal([regional.ItemId, partTime.ItemId, personal.ItemId], resolved);
    }

    [Fact]
    public async Task Publish_IsStopped_ByARuleThatBlocks()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, LaborRuleEnforcement.Block, (LaborRuleKeys.MaxShiftLength, "Hours", "8"));
        var period = await PlanningKit.CreatePeriodAsync(harness, setup.Team);

        await PlanningKit.AddDraftAsync(harness, period, setup.Worker.ItemId, _monday, new TimeOnly(8, 0), new TimeOnly(19, 0));

        var result = await PublishAsync(harness, period, null);
        var shifts = await PlanningKit.ListShiftsAsync(harness, setup.Team);
        var stored = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().FindPeriodAsync(period.ItemId), commit: false);

        Assert.False(result.Succeeded);
        Assert.Equal(LaborRuleKeys.MaxShiftLength, Assert.Single(result.Violations).RuleKey);
        Assert.Empty(shifts);
        Assert.Equal(0, stored.PublishedVersion);
    }

    [Fact]
    public async Task Publish_NeedsAReason_ForARuleThatNeedsAnOverride_AndRecordsIt()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, LaborRuleEnforcement.RequireOverride, (LaborRuleKeys.MinRest, "Hours", "11"));
        var period = await PlanningKit.CreatePeriodAsync(harness, setup.Team);

        await PlanningKit.AddDraftAsync(harness, period, setup.Worker.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, period, setup.Worker.ItemId, _monday.AddDays(1), new TimeOnly(3, 0), new TimeOnly(11, 0));

        var refused = await PublishAsync(harness, period, null);
        var published = await PublishAsync(harness, period, "Inventory night");
        var overrides = await harness.RunAsync(services => services.GetRequiredService<ILaborRuleOverrideService>().PageAsync(null, 0, 10), commit: false);

        Assert.False(refused.Succeeded);
        Assert.True(refused.NeedsOverride);
        Assert.True(published.Succeeded, string.Join(" ", published.Errors));
        Assert.Equal(2, published.Added);

        var recorded = Assert.Single(overrides.Items);

        Assert.Equal("Inventory night", recorded.Reason);
        Assert.Equal(LaborRuleKeys.MinRest, recorded.RuleKey);
        Assert.Equal(period.ItemId, recorded.PeriodId);
        Assert.Equal(setup.Worker.ItemId, recorded.WorkerId);
    }

    [Fact]
    public async Task Publish_GoesAhead_WithAWarning_AndCheckListsItFirst()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.MaxHoursPerDay, "Hours", "8"));
        var period = await PlanningKit.CreatePeriodAsync(harness, setup.Team);

        await PlanningKit.AddDraftAsync(harness, period, setup.Worker.ItemId, _monday, new TimeOnly(8, 0), new TimeOnly(19, 0));

        var checkedFirst = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().CheckPublishAsync(period.ItemId), commit: false);
        var result = await PublishAsync(harness, period, null);

        Assert.Equal(LaborRuleKeys.MaxHoursPerDay, Assert.Single(checkedFirst).RuleKey);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(LaborRuleKeys.MaxHoursPerDay, Assert.Single(result.Violations).RuleKey);
        Assert.Single(await PlanningKit.ListShiftsAsync(harness, setup.Team));
    }

    [Fact]
    public async Task LateChanges_InsideTheNoticeWindow_RecordPredictabilityPay_ExceptTheWorkersOwn()
    {
        // Arrange (14 days' notice; 1 hour for an added or moved shift, half the hours of a removed one)
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.PredictabilityPay, "NoticeDays", "14"));
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", setup.Team);
        var period = await PlanningKit.CreatePeriodAsync(harness, setup.Team);
        var monday = await PlanningKit.AddDraftAsync(harness, period, setup.Worker.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        await PlanningKit.AddDraftAsync(harness, period, setup.Worker.ItemId, _monday.AddDays(1), new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, period, ben.ItemId, _monday.AddDays(2), new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, period, ben.ItemId, _monday.AddDays(3), new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, period);

        // Act: move Monday and remove Tuesday, then give Ben's Wednesday to Ana, and let Ben swap away Thursday.
        await harness.RunAsync(async services =>
        {
            var planning = services.GetRequiredService<ISchedulePlanningService>();
            var drafts = await planning.ListDraftShiftsAsync(period.PrimaryScenarioId);
            var moved = drafts.Single(draft => draft.ShiftKey == monday.ShiftKey);

            moved.Start = _monday.ToDateTime(new TimeOnly(10, 0));
            moved.End = _monday.ToDateTime(new TimeOnly(18, 0));
            await planning.SaveDraftShiftAsync(moved, "manager");
            await planning.DeleteDraftShiftAsync(drafts.Single(draft => draft.Date == _monday.AddDays(1)).ItemId, "manager");
        });
        await PlanningKit.PublishAsync(harness, period);

        var shifts = await PlanningKit.ListShiftsAsync(harness, setup.Team);
        var wednesday = shifts.Single(shift => shift.StartUtc == PlanningKit.Utc(_monday.AddDays(2), new TimeOnly(9, 0)));
        var thursday = shifts.Single(shift => shift.StartUtc == PlanningKit.Utc(_monday.AddDays(3), new TimeOnly(9, 0)));

        await harness.RunAsync(services => services.GetRequiredService<IShiftService>().ReassignAsync(wednesday.ItemId, setup.Worker.ItemId, ScheduleChangeInitiator.Employer, "Ben needed at the other site", "manager"));
        await harness.RunAsync(services => services.GetRequiredService<IShiftService>().ReassignAsync(thursday.ItemId, setup.Worker.ItemId, ScheduleChangeInitiator.Worker, "Ben asked", "manager"));

        var entries = (await harness.RunAsync(services => services.GetRequiredService<IPredictabilityPayService>().PageAsync(new PredictabilityPayQuery(), 0, 20), commit: false)).Items;

        // Assert
        var names = new Dictionary<string, string> { [setup.Worker.ItemId] = "Ana", [ben.ItemId] = "Ben" };

        Assert.Equal(
            ["Ana Added 1", "Ana Moved 1", "Ana Removed 4", "Ben Removed 4"],
            entries.Select(entry => string.Create(CultureInfo.InvariantCulture, $"{names[entry.WorkerId]} {entry.ChangeKind} {entry.Hours:0.##}")).Order(StringComparer.Ordinal));
        Assert.All(entries, entry => Assert.Equal(PredictabilityPayStatus.Pending, entry.Status));

        // Payroll approves one and waives another.
        var approved = await harness.RunAsync(services => services.GetRequiredService<IPredictabilityPayService>().ApproveAsync(entries[0].ItemId, "payroll"));
        var waived = await harness.RunAsync(services => services.GetRequiredService<IPredictabilityPayService>().WaiveAsync(entries[1].ItemId, "Emergency closure", "payroll"));
        var again = await harness.RunAsync(services => services.GetRequiredService<IPredictabilityPayService>().ApproveAsync(entries[1].ItemId, "payroll"));

        Assert.Equal(PredictabilityPayStatus.Approved, approved.Value.Status);
        Assert.Equal(PredictabilityPayStatus.Waived, waived.Value.Status);
        Assert.Equal("Emergency closure", waived.Value.Note);
        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task Claims_FollowTheWorkersRuleSet_InPlaceOfTheDefaultLimits()
    {
        // Arrange (the default rest is 10 hours; the team's rule set asks for 8, and blocks over 9 hours a day)
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, LaborRuleEnforcement.Block, (LaborRuleKeys.MinRest, "Hours", "8"), (LaborRuleKeys.MaxHoursPerDay, "Hours", "9"));
        var period = await PlanningKit.CreatePeriodAsync(harness, setup.Team);

        await PlanningKit.AddDraftAsync(harness, period, setup.Worker.ItemId, _monday, new TimeOnly(8, 0), new TimeOnly(16, 0));
        await PlanningKit.AddDraftAsync(harness, period, null, _monday.AddDays(1), new TimeOnly(1, 0), new TimeOnly(9, 0));
        await PlanningKit.AddDraftAsync(harness, period, null, _monday.AddDays(2), new TimeOnly(8, 0), new TimeOnly(18, 0));
        await PlanningKit.UpdateTeamAsync(harness, setup.Team, team => team.OpenShiftClaimPolicy = OpenShiftClaimPolicy.FirstCome);
        await PlanningKit.PublishAsync(harness, period);

        var open = (await harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().PageAsync(new OpenShiftQuery(), 0, 10), commit: false)).Items;

        // Act
        var restsNine = await harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().ClaimAsync(open.Single(shift => shift.Date == _monday.AddDays(1)).ItemId, setup.Worker.ItemId, "Ana"));
        var tenHours = await harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().ClaimAsync(open.Single(shift => shift.Date == _monday.AddDays(2)).ItemId, setup.Worker.ItemId, "Ana"));

        // Assert
        Assert.True(restsNine.Succeeded, string.Join(" ", restsNine.Errors));
        Assert.False(tenHours.Succeeded);
        Assert.Contains(tenHours.Errors, error => error.Contains('9'));
    }

    [Fact]
    public async Task Limits_ComeFromTheRuleSet_WhenOneApplies()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness, (LaborRuleKeys.MinRest, "Hours", "12"), (LaborRuleKeys.MaxHoursPerWeek, "Hours", "30"), (LaborRuleKeys.MaxConsecutiveDays, "Days", "4"));
        var other = await PlanningKit.AddMemberAsync(harness, "Dee", await WorkforceTests.AddTeamAsync(harness, "Lab", TeamKind.Team), worker => worker.MaxHoursPerWeek = 20);

        var limits = await harness.RunAsync(services => services.GetRequiredService<IWorkerScheduleLimitsProvider>().GetAsync(setup.Worker, setup.Team.ItemId), commit: false);
        var defaults = await harness.RunAsync(services => services.GetRequiredService<IWorkerScheduleLimitsProvider>().GetAsync(other, other.TeamMemberships[0].TeamId), commit: false);

        Assert.True(limits.FromRuleSet);
        Assert.Equal((12m, 4, 30m), (limits.MinRestHours, limits.MaxConsecutiveDays, limits.MaxHoursPerWeek.Value));
        Assert.False(defaults.FromRuleSet);
        Assert.Equal((10m, 6, 20m), (defaults.MinRestHours, defaults.MaxConsecutiveDays, defaults.MaxHoursPerWeek.Value));
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return WorkforceTests.CreateAsync(services =>
        {
            services.AddWorkforceTimeOffCore();
            services.AddWorkforceLaborRulesCore();
        });
    }

    private static Task<Setup> SetUpAsync(CalendarTestHarness harness, params (string Key, string Parameter, string Value)[] rules)
    {
        return SetUpAsync(harness, LaborRuleEnforcement.Warn, rules);
    }

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness, LaborRuleEnforcement enforcement, params (string Key, string Parameter, string Value)[] rules)
    {
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var worker = await PlanningKit.AddMemberAsync(harness, "Ana", team);

        await AddRuleSetAsync(harness, "Front desk rules", set =>
        {
            set.TeamIds.Add(team.ItemId);

            foreach (var (key, parameter, value) in rules)
            {
                var rule = set.Rules.FirstOrDefault(item => item.RuleKey == key);

                if (rule is null)
                {
                    rule = new LaborRuleInstance { Id = key, RuleKey = key, Enforcement = enforcement };
                    set.Rules.Add(rule);
                }

                if (parameter is not null)
                {
                    rule.Parameters[parameter] = value;
                }
            }
        });

        return new Setup(team, worker);
    }

    private static Task<LaborRuleSet> AddRuleSetAsync(CalendarTestHarness harness, string name, Action<LaborRuleSet> configure)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ILaborRuleSetManager>();
            var set = await manager.NewAsync();

            set.Name = name;
            configure(set);

            var validation = await manager.ValidateAsync(set);

            Assert.True(validation.Succeeded, string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(set);

            return set;
        });
    }

    private static Task<IList<LaborRuleViolation>> EvaluateAsync(CalendarTestHarness harness, Setup setup, params LaborRuleShift[] shifts)
    {
        return EvaluateAsync(harness, setup, shifts, []);
    }

    private static async Task<IList<LaborRuleViolation>> EvaluateAsync(CalendarTestHarness harness, Setup setup, LaborRuleShift[] shifts, params LaborRuleChange[] changes)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<ILaborRuleEvaluator>().EvaluateAsync(new LaborRuleRequest
        {
            Worker = setup.Worker,
            TeamId = setup.Team.ItemId,
            Candidates = shifts.Select(shift =>
            {
                shift.TeamId = setup.Team.ItemId;

                return shift;
            }).ToList(),
            Changes = changes,
        }), commit: false);

        return result.Violations;
    }

    private static Task<PlanningPublishResult> PublishAsync(CalendarTestHarness harness, SchedulePeriod period, string reason)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().PublishAsync(period.ItemId, "manager", reason));
    }

    private static LaborRuleShift Shift(DateOnly date, int startHour, int endHour)
    {
        var start = PlanningKit.Utc(date, new TimeOnly(startHour, 0));
        var end = endHour > startHour ? PlanningKit.Utc(date, new TimeOnly(endHour % 24, 0)) : PlanningKit.Utc(date.AddDays(1), new TimeOnly(endHour % 24, 0));

        if (endHour >= 24)
        {
            end = PlanningKit.Utc(date.AddDays(1), new TimeOnly(endHour - 24, 0));
        }

        return new LaborRuleShift
        {
            ShiftKey = $"{date:yyyyMMdd}-{startHour}",
            Date = date,
            StartUtc = start,
            EndUtc = end,
            PaidMinutes = (int)(end - start).TotalMinutes,
            IsCandidate = true,
        };
    }

    private sealed record Setup(WorkforceTeam Team, WorkerProfile Worker);
}
