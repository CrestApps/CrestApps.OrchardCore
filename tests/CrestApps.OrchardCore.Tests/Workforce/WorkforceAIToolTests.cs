using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.AI;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The Workforce AI tools, run as the signed-in user: who is working limited to the teams they manage, their own
/// schedule, asking for time off, and finding cover for a shift.
/// </summary>
public sealed class WorkforceAIToolTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task WhoIsWorking_ListsOnlyTheTeamsTheUserManages()
    {
        await using var harness = await CreateAsync();
        var (east, west) = await PublishTwoTeamsAsync(harness);

        await WorkforceTests.AddWorkerAsync(harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = east.ItemId, Role = TeamMemberRole.Manager });
        });

        var manager = await InvokeAsync<WhoIsWorkingTool>(harness, CalendarTestHarness.User("maya"), new() { ["at"] = "2026-11-09T12:00" });
        var outsider = await InvokeAsync<WhoIsWorkingTool>(harness, CalendarTestHarness.User("someone"), new() { ["at"] = "2026-11-09T12:00" });
        var westOnly = await InvokeAsync<WhoIsWorkingTool>(harness, CalendarTestHarness.User("maya"), new() { ["at"] = "2026-11-09T12:00", ["teamName"] = west.Name });

        Assert.Equal(["Ana"], Workers(manager));
        Assert.Contains("don't manage", outsider, StringComparison.Ordinal);
        Assert.Contains("don't manage", westOnly, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MySchedule_ListsTheUsersOwnShifts()
    {
        await using var harness = await CreateAsync();

        await PublishTwoTeamsAsync(harness);

        var result = await InvokeAsync<MyScheduleTool>(harness, CalendarTestHarness.User("user-ana"), new() { ["from"] = "2026-11-09", ["days"] = 3 });
        var shifts = JsonDocument.Parse(result).RootElement.GetProperty("shifts").EnumerateArray().ToArray();

        Assert.Single(shifts);
        Assert.Equal("East", shifts[0].GetProperty("team").GetString());
        Assert.StartsWith("2026-11-09T09:00", shifts[0].GetProperty("start").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestTimeOff_AsksForTheUsersOwnTimeOff()
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "East", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);

        await TimeOffKit.AddTypeAsync(harness, "Jury duty", type => type.RequiresBalance = false);

        var result = await InvokeAsync<RequestTimeOffTool>(harness, CalendarTestHarness.User("user-ana", WorkforcePermissions.RequestTimeOff.Name), new()
        {
            ["typeName"] = "Jury duty",
            ["startDate"] = "2026-11-16",
            ["endDate"] = "2026-11-17",
            ["reason"] = "Summons",
        });
        var requests = await harness.RunAsync(async services => (await services.GetRequiredService<CrestApps.OrchardCore.Workforce.Services.ITimeOffRequestService>().PageAsync(new CrestApps.OrchardCore.Workforce.Services.TimeOffRequestQuery { WorkerId = ana.ItemId }, 0, 10)).Items, commit: false);

        var request = Assert.Single(requests);
        Assert.Equal(new DateOnly(2026, 11, 16), request.StartDate);
        Assert.Contains("Jury duty", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindCover_ListsColleaguesWhoCanTakeTheShift()
    {
        await using var harness = await CreateAsync();
        var region = await WorkforceTests.AddTeamAsync(harness, "Region", TeamKind.Region);
        var team = await WorkforceTests.AddTeamAsync(harness, "East", TeamKind.Team, region.ItemId);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var cara = await PlanningKit.AddMemberAsync(harness, "Cara", team);
        var dan = await PlanningKit.AddMemberAsync(harness, "Dan", team);
        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, period, dan.ItemId, _monday, new TimeOnly(8, 0), new TimeOnly(16, 0));
        await PlanningKit.PublishAsync(harness, period);
        await WorkforceTests.AddWorkerAsync(harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = region.ItemId, Role = TeamMemberRole.Manager });
        });

        var result = await InvokeAsync<FindCoverTool>(harness, CalendarTestHarness.User("maya"), new() { ["workerName"] = "Ana", ["date"] = "2026-11-09" });
        var root = JsonDocument.Parse(result).RootElement;

        Assert.Equal(["Cara"], root.GetProperty("canCover").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.Contains("Dan", root.GetProperty("cannotCover").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.NotNull(cara);
    }

    private static async Task<(WorkforceTeam East, WorkforceTeam West)> PublishTwoTeamsAsync(CalendarTestHarness harness)
    {
        var east = await WorkforceTests.AddTeamAsync(harness, "East", TeamKind.Team);
        var west = await WorkforceTests.AddTeamAsync(harness, "West", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", east);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", west);

        foreach (var (team, worker) in new[] { (east, ana), (west, ben) })
        {
            var period = await PlanningKit.CreatePeriodAsync(harness, team);

            await PlanningKit.AddDraftAsync(harness, period, worker.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
            await PlanningKit.PublishAsync(harness, period);
        }

        return (east, west);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return WorkforceTests.CreateAsync(services =>
        {
            services.AddWorkforceTimeOffCore();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        });
    }

    private static Task<string> InvokeAsync<TTool>(CalendarTestHarness harness, ClaimsPrincipal user, Dictionary<string, object> arguments)
        where TTool : AIFunction
    {
        return harness.RunAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            var tool = ActivatorUtilities.CreateInstance<TTool>(services);
            var result = await tool.InvokeAsync(new AIFunctionArguments(arguments.ToDictionary(pair => pair.Key, pair => (object)JsonSerializer.SerializeToElement(pair.Value))) { Services = services });

            return result?.ToString();
        });
    }

    private static string[] Workers(string result)
    {
        return [.. JsonDocument.Parse(result).RootElement.GetProperty("working").EnumerateArray().Select(item => item.GetProperty("name").GetString())];
    }
}
