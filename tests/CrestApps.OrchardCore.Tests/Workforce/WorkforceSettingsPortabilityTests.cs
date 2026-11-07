using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Teams.Models;
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
/// Rotations, coverage requirements, time clock kiosks, the payroll CSV columns and labor rule sets travel with the
/// Workforce settings deployment step: they are exported with their references as names, imported into an empty site,
/// matched on a second run instead of duplicated, and a kiosk's token never leaves the site.
/// </summary>
public sealed class WorkforceSettingsPortabilityTests
{
    private const string KioskTokenHash = "kiosk-token-hash-that-must-not-travel";

    [Fact]
    public async Task SettingsDeploymentStep_RoundTripsRotationsCoverageKiosksPayrollColumnsAndRuleSets()
    {
        // Arrange
        JsonObject[] exported;

        await using (var source = await CreateAsync())
        {
            await SeedAsync(source);

            await source.RunAsync(async services =>
            {
                var kiosks = services.GetRequiredService<ITimeClockKioskManager>();
                var kiosk = Assert.Single(await kiosks.GetAllAsync());

                kiosk.TokenHash = KioskTokenHash;
                await kiosks.UpdateAsync(kiosk);
            });

            exported = await ExportAsync(source);
        }

        await using var target = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(target, "Front desk", TeamKind.Team);
        var skillId = await AddSkillAsync(target, "Spanish");

        // Act
        var errors = new List<string>();

        foreach (var step in exported)
        {
            errors.AddRange(await RunAsync(target, step));
        }

        foreach (var step in exported)
        {
            errors.AddRange(await RunAsync(target, step));
        }

        // Assert
        Assert.Equal(["WorkforceShiftPlanning", "WorkforceTimeOff", "WorkforceTimeClock", "WorkforceLaborRules"], exported.Select(step => step["name"]!.GetValue<string>()));
        Assert.DoesNotContain(KioskTokenHash, string.Join(string.Empty, exported.Select(step => step.ToJsonString())));
        Assert.Empty(errors);

        await target.RunAsync(async services =>
        {
            var template = Assert.Single(await services.GetRequiredService<IShiftTemplateManager>().GetAllAsync());
            var pattern = Assert.Single(await services.GetRequiredService<IShiftPatternManager>().GetAllAsync());
            var requirement = Assert.Single(await services.GetRequiredService<ICoverageService>().ListRequirementsAsync(team.ItemId));
            var kiosk = Assert.Single(await services.GetRequiredService<ITimeClockKioskManager>().GetAllAsync());
            var csv = await services.GetRequiredService<IPayrollExportService>().GetCsvSettingsAsync();
            var ruleSet = Assert.Single(await services.GetRequiredService<ILaborRuleSetManager>().GetAllAsync());

            Assert.Equal("Two-week rotation", pattern.Name);
            Assert.Equal(team.ItemId, pattern.TeamId);
            Assert.Equal(2, pattern.WeekCount);
            Assert.Equal([(0, DayOfWeek.Monday, template.ItemId), (1, DayOfWeek.Tuesday, template.ItemId)], pattern.Days.Select(day => (day.Week, day.Day, day.TemplateId)));

            Assert.Equal("Weekday mornings", requirement.Name);
            Assert.Equal(skillId, requirement.SkillId);
            Assert.Equal([(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0), 1, 2)], requirement.Rows.Select(row => (row.Day.Value, row.Start, row.End, row.Minimum, row.Target)));

            Assert.Equal("Lobby", kiosk.Name);
            Assert.Equal(team.ItemId, kiosk.TeamId);
            Assert.Null(kiosk.TokenHash);

            Assert.Equal([("Employee", PayrollCsvField.WorkerExternalId), ("Hours", PayrollCsvField.Hours)], csv.Columns.Select(column => (column.Header, column.Field)));

            Assert.Equal("Hourly staff", ruleSet.Name);
            Assert.Equal([team.ItemId], ruleSet.TeamIds);
            Assert.Equal(["MaxHoursPerWeek"], ruleSet.Rules.Select(rule => rule.RuleKey));
        }, commit: false);
    }

    [Fact]
    public async Task TimeClockStep_KeepsTheTokenOfAKioskItUpdates()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var step = JsonNode.Parse("""
            { "name": "WorkforceTimeClock", "Kiosks": [ { "name": "Lobby", "teamName": "Front desk", "enabled": true } ] }
            """).AsObject();

        Assert.Empty(await RunAsync(harness, step));

        await harness.RunAsync(async services =>
        {
            var kiosks = services.GetRequiredService<ITimeClockKioskManager>();
            var kiosk = Assert.Single(await kiosks.GetAllAsync());

            kiosk.TokenHash = KioskTokenHash;
            await kiosks.UpdateAsync(kiosk);
        });

        step["Kiosks"][0]["enabled"] = false;

        // Act
        var errors = await RunAsync(harness, step);

        // Assert
        Assert.Empty(errors);

        var stored = Assert.Single(await harness.RunAsync(async services => await services.GetRequiredService<ITimeClockKioskManager>().GetAllAsync(), commit: false));

        Assert.False(stored.Enabled);
        Assert.Equal(KioskTokenHash, stored.TokenHash);
    }

    [Fact]
    public async Task ShiftPlanningStep_ReportsARotationTemplateThatDoesNotExist()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var step = JsonNode.Parse("""
            {
              "name": "WorkforceShiftPlanning",
              "Patterns": [ { "name": "Rotation", "teamName": "Front desk", "weekCount": 1, "days": [ { "week": 0, "day": "Monday", "templateName": "Late" } ] } ]
            }
            """).AsObject();

        // Act
        var errors = await RunAsync(harness, step);

        // Assert
        Assert.Equal(["The rotation 'Rotation' in the WorkforceShiftPlanning step names a shift template that doesn't exist: 'Late'."], errors);
    }

    private static async Task SeedAsync(CalendarTestHarness harness)
    {
        await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        await AddSkillAsync(harness, "Spanish");

        var steps = new[]
        {
            """
            {
              "name": "WorkforceShiftPlanning",
              "Templates": [ { "name": "Early", "start": "07:00:00", "lengthMinutes": 480, "teamNames": [ "Front desk" ] } ],
              "Patterns": [
                { "name": "Two-week rotation", "teamName": "Front desk", "weekCount": 2,
                  "days": [ { "week": 0, "day": "Monday", "templateName": "Early" }, { "week": 1, "day": "Tuesday", "templateName": "Early" } ] }
              ],
              "CoverageRequirements": [
                { "name": "Weekday mornings", "teamName": "Front desk", "skillName": "Spanish",
                  "rows": [ { "day": "Monday", "start": "08:00:00", "end": "12:00:00", "minimum": 1, "target": 2 } ] }
              ]
            }
            """,
            """
            {
              "name": "WorkforceTimeClock",
              "Kiosks": [ { "name": "Lobby", "teamName": "Front desk", "enabled": true } ],
              "PayrollCsv": { "columns": [ { "header": "Employee", "field": "WorkerExternalId" }, { "header": "Hours", "field": "Hours" } ] }
            }
            """,
            """
            {
              "name": "WorkforceLaborRules",
              "RuleSets": [
                { "name": "Hourly staff", "teamNames": [ "Front desk" ],
                  "rules": [ { "ruleKey": "MaxHoursPerWeek", "enforcement": "Warn", "parameters": { "Hours": "40" } } ] }
              ]
            }
            """,
        };

        foreach (var step in steps)
        {
            Assert.Empty(await RunAsync(harness, JsonNode.Parse(step).AsObject()));
        }
    }

    private static async Task<JsonObject[]> ExportAsync(CalendarTestHarness harness)
    {
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        await harness.RunAsync(services => ActivatorUtilities.CreateInstance<WorkforceSettingsDeploymentSource>(services)
            .ProcessDeploymentStepAsync(new WorkforceSettingsDeploymentStep(), result), commit: false);

        return [.. result.Steps.Select(step => JsonNode.Parse(step.ToJsonString())!.AsObject())];
    }

    private static Task<string> AddSkillAsync(CalendarTestHarness harness, string name)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();
            var skill = await manager.NewAsync();

            skill.Name = name;
            await manager.CreateAsync(skill);

            return skill.ItemId;
        });
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return WorkforceTests.CreateAsync(services =>
        {
            services.AddWorkforceTimeOffCore();
            services.AddWorkforceTimeClockCore();
            services.AddWorkforceLaborRulesCore();
        });
    }

    private static Task<IList<string>> RunAsync(CalendarTestHarness harness, JsonObject step)
    {
        return harness.RunAsync(async services =>
        {
            var name = step["name"]!.GetValue<string>();
            var context = new RecipeExecutionContext { Name = name, Step = step.DeepClone().AsObject() };
            global::OrchardCore.Recipes.Services.NamedRecipeStepHandler handler = name switch
            {
                ShiftPlanningRecipeStep.ShiftPlanningStepName => ActivatorUtilities.CreateInstance<ShiftPlanningRecipeStep>(services),
                TimeOffRecipeStep.TimeOffStepName => ActivatorUtilities.CreateInstance<TimeOffRecipeStep>(services),
                TimeClockRecipeStep.TimeClockStepName => ActivatorUtilities.CreateInstance<TimeClockRecipeStep>(services),
                LaborRulesRecipeStep.LaborRulesStepName => ActivatorUtilities.CreateInstance<LaborRulesRecipeStep>(services),
                _ => throw new InvalidOperationException(name),
            };

            await handler.ExecuteAsync(context);

            return context.Errors;
        });
    }
}
