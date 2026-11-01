using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Deployments;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Recipes;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The shift planning, time off and time clock recipe steps: records matched by name so a second run updates them,
/// references written as names, unknown names reported, and the settings deployment step that writes them back out.
/// </summary>
public sealed class WorkforceSettingsRecipeTests
{
    [Fact]
    public async Task TimeOffStep_CreatesTypesPoliciesAllotmentsAndBlackouts_ThenUpdatesInPlace()
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var step = JsonNode.Parse("""
            {
              "name": "WorkforceTimeOff",
              "Types": [ { "name": "Vacation", "unit": "Hours", "hoursPerDay": 8, "payCode": "PTO" } ],
              "Policies": [ { "name": "Vacation full time", "typeName": "Vacation", "accrualMethod": "None", "teamNames": [ "Front desk" ] } ],
              "Allotments": [ { "name": "Front desk", "teamName": "Front desk", "typeNames": [ "Vacation" ], "weekdaySlots": [ 2, 2, 2, 2, 2, 2, 2 ] } ],
              "BlackoutPeriods": [ { "name": "Year end", "teamName": "Front desk", "startDate": "2026-12-20", "endDate": "2026-12-31" } ]
            }
            """).AsObject();

        var first = await RunAsync<TimeOffRecipeStep>(harness, step);

        step["Types"][0]["hoursPerDay"] = 7.5;

        var second = await RunAsync<TimeOffRecipeStep>(harness, step);

        Assert.Empty(first);
        Assert.Empty(second);

        await harness.RunAsync(async services =>
        {
            var type = Assert.Single(await services.GetRequiredService<ITimeOffTypeManager>().GetAllAsync());
            var policy = Assert.Single(await services.GetRequiredService<ITimeOffPolicyManager>().GetAllAsync());
            var allotment = Assert.Single(await services.GetRequiredService<ITimeOffAllotmentManager>().GetAllAsync());
            var blackout = Assert.Single(await services.GetRequiredService<IBlackoutPeriodManager>().GetAllAsync());

            Assert.Equal(7.5m, type.HoursPerDay);
            Assert.Equal(type.ItemId, policy.TypeId);
            Assert.Equal([team.ItemId], policy.Assignments.Select(assignment => assignment.TeamId));
            Assert.Equal(team.ItemId, allotment.TeamId);
            Assert.Equal([type.ItemId], allotment.TypeIds);
            Assert.Equal([2, 2, 2, 2, 2, 2, 2], allotment.WeekdaySlots);
            Assert.Equal(team.ItemId, blackout.TeamId);
            Assert.Equal(new DateOnly(2026, 12, 20), blackout.StartDate);
        }, commit: false);
    }

    [Fact]
    public async Task TimeOffStep_ReportsANameThatDoesNotExist()
    {
        await using var harness = await CreateAsync();
        var step = JsonNode.Parse("""
            { "name": "WorkforceTimeOff", "Policies": [ { "name": "Sick", "typeName": "Sick time", "accrualMethod": "None" } ] }
            """).AsObject();

        var errors = await RunAsync<TimeOffRecipeStep>(harness, step);

        Assert.Equal(["The policy 'Sick' in the WorkforceTimeOff step names a time-off type that doesn't exist: 'Sick time'."], errors);
    }

    [Fact]
    public async Task TimeClockStep_CreatesPayPeriodsCodesAndPolicies_AndGivesTeamsTheirPolicy()
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Warehouse", TeamKind.Team);
        var step = JsonNode.Parse("""
            {
              "name": "WorkforceTimeClock",
              "PayPeriods": [ { "name": "Every two weeks", "frequency": "Biweekly", "anchorDate": "2026-10-25", "weekStart": "Sunday" } ],
              "PayCodes": [
                { "code": "REG", "name": "Regular", "kind": "Regular", "multiplier": 1 },
                { "code": "NIGHT", "name": "Night premium", "kind": "ShiftDifferential", "windowStart": "22:00:00", "windowEnd": "06:00:00", "premiumPerHour": 2 }
              ],
              "Policies": [ { "name": "Hourly staff", "payPeriodsName": "Every two weeks", "roundingMinutes": 5, "teamNames": [ "Warehouse" ] } ]
            }
            """).AsObject();

        var first = await RunAsync<TimeClockRecipeStep>(harness, step);
        var second = await RunAsync<TimeClockRecipeStep>(harness, step);

        Assert.Empty(first);
        Assert.Empty(second);

        await harness.RunAsync(async services =>
        {
            var calendar = Assert.Single(await services.GetRequiredService<IPayPeriodCalendarManager>().GetAllAsync());
            var codes = await services.GetRequiredService<IPayCodeManager>().GetAllAsync();
            var policy = Assert.Single(await services.GetRequiredService<ITimeClockPolicyManager>().GetAllAsync());
            var stored = await services.GetRequiredService<ITeamManager>().FindByIdAsync(team.ItemId);

            Assert.Equal(PayPeriodFrequency.Biweekly, calendar.Frequency);
            Assert.Equal(["NIGHT", "REG"], codes.Select(code => code.Code).Order(StringComparer.Ordinal));
            Assert.Equal(new TimeOnly(22, 0), codes.Single(code => code.Code == "NIGHT").WindowStart);
            Assert.Equal(calendar.ItemId, policy.PayPeriodCalendarId);
            Assert.Equal(5, policy.RoundingMinutes);
            Assert.Equal(policy.ItemId, stored.TimeClockPolicyId);
        }, commit: false);
    }

    [Fact]
    public async Task ShiftPlanningStep_CreatesTemplatesWithTheirActivitiesAndTeamsByName()
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Clinic", TeamKind.Team);
        var breakCode = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IActivityCodeManager>();
            var code = await manager.NewAsync();

            code.Name = "Lunch";
            code.Code = "LUNCH";
            code.Kind = ActivityKind.Meal;
            await manager.CreateAsync(code);

            return code;
        });
        var step = JsonNode.Parse("""
            {
              "name": "WorkforceShiftPlanning",
              "Templates": [
                { "name": "Early", "start": "07:00:00", "lengthMinutes": 480, "teamNames": [ "Clinic" ],
                  "segments": [ { "activityName": "Lunch", "offsetMinutes": 240, "lengthMinutes": 30 } ] }
              ]
            }
            """).AsObject();

        var errors = await RunAsync<ShiftPlanningRecipeStep>(harness, step);

        Assert.Empty(errors);

        var template = Assert.Single(await harness.RunAsync(async services => await services.GetRequiredService<IShiftTemplateManager>().GetAllAsync(), commit: false));

        Assert.Equal(new TimeOnly(7, 0), template.Start);
        Assert.Equal([team.ItemId], template.TeamIds);
        Assert.Equal(breakCode.ItemId, Assert.Single(template.Segments).ActivityCodeId);
    }

    [Fact]
    public async Task TheSettingsDeploymentStep_WritesStepsThatImportIntoAnotherSite()
    {
        JsonObject[] exported;

        await using (var source = await CreateAsync())
        {
            await WorkforceTests.AddTeamAsync(source, "Front desk", TeamKind.Team);
            await RunAsync<TimeOffRecipeStep>(source, JsonNode.Parse("""
                {
                  "name": "WorkforceTimeOff",
                  "Types": [ { "name": "Vacation" } ],
                  "Policies": [ { "name": "Vacation full time", "typeName": "Vacation", "accrualMethod": "None", "teamNames": [ "Front desk" ] } ]
                }
                """).AsObject());
            await RunAsync<TimeClockRecipeStep>(source, JsonNode.Parse("""
                {
                  "name": "WorkforceTimeClock",
                  "PayPeriods": [ { "name": "Weekly", "frequency": "Weekly" } ],
                  "Policies": [ { "name": "Hourly staff", "payPeriodsName": "Weekly", "teamNames": [ "Front desk" ] } ]
                }
                """).AsObject());

            var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

            await source.RunAsync(services => ActivatorUtilities.CreateInstance<WorkforceSettingsDeploymentSource>(services)
                .ProcessDeploymentStepAsync(new WorkforceSettingsDeploymentStep(), result), commit: false);

            exported = [.. result.Steps.Select(step => JsonNode.Parse(step.ToJsonString())!.AsObject())];
        }

        await using var target = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(target, "Front desk", TeamKind.Team);

        Assert.Equal(["WorkforceShiftPlanning", "WorkforceTimeOff", "WorkforceTimeClock"], exported.Select(step => step["name"]!.GetValue<string>()));
        Assert.Empty(await RunAsync<ShiftPlanningRecipeStep>(target, exported[0]));
        Assert.Empty(await RunAsync<TimeOffRecipeStep>(target, exported[1]));
        Assert.Empty(await RunAsync<TimeClockRecipeStep>(target, exported[2]));

        await target.RunAsync(async services =>
        {
            var type = Assert.Single(await services.GetRequiredService<ITimeOffTypeManager>().GetAllAsync());
            var policy = Assert.Single(await services.GetRequiredService<ITimeOffPolicyManager>().GetAllAsync());
            var clock = Assert.Single(await services.GetRequiredService<ITimeClockPolicyManager>().GetAllAsync());
            var stored = await services.GetRequiredService<ITeamManager>().FindByIdAsync(team.ItemId);

            Assert.Equal(type.ItemId, policy.TypeId);
            Assert.Equal([team.ItemId], policy.Assignments.Select(assignment => assignment.TeamId));
            Assert.Equal(clock.ItemId, stored.TimeClockPolicyId);
        }, commit: false);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return WorkforceTests.CreateAsync(services =>
        {
            services.AddWorkforceTimeOffCore();
            services.AddWorkforceTimeClockCore();
        });
    }

    private static Task<IList<string>> RunAsync<TStep>(CalendarTestHarness harness, JsonObject step)
        where TStep : global::OrchardCore.Recipes.Services.NamedRecipeStepHandler
    {
        return harness.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = step["name"]!.GetValue<string>(), Step = step.DeepClone().AsObject() };

            await ActivatorUtilities.CreateInstance<TStep>(services).ExecuteAsync(context);

            return context.Errors;
        });
    }
}
