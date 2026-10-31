using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Recipes;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The Workforce recipe step: teams in tree order by name, workers matched by employee number with their teams by name,
/// activity codes by name, and a second run that updates instead of adding.
/// </summary>
public sealed class WorkforceRecipeTests
{
    [Fact]
    public async Task WorkforceStep_CreatesTheTreeAndWorkers_ThenUpdatesInPlace()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var step = JsonNode.Parse("""
            {
              "name": "Workforce",
              "ActivityCodes": [ { "name": "Break", "code": "brk", "kind": "Break", "isPaid": true, "countsTowardCoverage": false } ],
              "Teams": [
                { "name": "Boston", "kind": "Site", "parentName": "East", "timeZoneId": "America/New_York" },
                { "name": "East", "kind": "Region" }
              ],
              "Workers": [
                { "displayName": "Ana Lopez", "employeeNumber": "E100", "employmentType": "PartTime",
                  "teamMemberships": [ { "teamName": "Boston", "role": "Manager" } ] }
              ]
            }
            """).AsObject();

        // Act
        var firstErrors = await RunAsync(harness, step);

        step["Workers"][0]["displayName"] = "Ana López";
        step["Teams"][1]["kind"] = "Organization";

        var secondErrors = await RunAsync(harness, step);

        // Assert
        Assert.Empty(firstErrors);
        Assert.Empty(secondErrors);

        await harness.RunAsync(async services =>
        {
            var tree = await services.GetRequiredService<ITeamManager>().ListTreeAsync();
            var (count, workers) = await services.GetRequiredService<IWorkerManager>().SearchAsync(new WorkerQuery(), 0, 10);
            var codes = await services.GetRequiredService<IActivityCodeManager>().GetEnabledAsync();
            var boston = tree.Single(item => item.Team.Name == "Boston").Team;
            var calendar = await services.GetRequiredService<ICalendarService>().FindBySourceAsync(WorkforceConstants.Source, boston.ItemId);

            Assert.Equal(["East:0", "Boston:1"], tree.Select(item => $"{item.Team.Name}:{item.Depth}"));
            Assert.Equal(TeamKind.Organization, tree[0].Team.Kind);
            Assert.Equal(1, count);
            var worker = Assert.Single(workers);
            Assert.Equal("Ana López", worker.DisplayName);
            Assert.Equal(EmploymentType.PartTime, worker.EmploymentType);
            var membership = Assert.Single(worker.TeamMemberships);
            Assert.Equal(boston.ItemId, membership.TeamId);
            Assert.Equal(TeamMemberRole.Manager, membership.Role);
            Assert.NotNull(calendar);
            Assert.Equal("BRK", Assert.Single(codes, code => code.Name == "Break").Code);
        }, commit: false);
    }

    [Fact]
    public async Task WorkforceStep_ReportsAWorkerWhoseTeamDoesNotExist()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var step = JsonNode.Parse("""
            {
              "name": "Workforce",
              "Workers": [ { "displayName": "Lost", "teamMemberships": [ { "teamName": "Nowhere" } ] } ]
            }
            """).AsObject();

        // Act
        var errors = await RunAsync(harness, step);

        // Assert
        Assert.Single(errors);
    }

    private static Task<IList<string>> RunAsync(CalendarTestHarness harness, JsonObject step)
    {
        return harness.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = WorkforceConstants.RecipeStepName, Step = step.DeepClone().AsObject() };

            await ActivatorUtilities.CreateInstance<WorkforceRecipeStep>(services).ExecuteAsync(context);

            return context.Errors;
        });
    }
}
