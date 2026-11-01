using System.Text.Json;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Api;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The Workforce API: the caller's own schedule and punches, punches recorded once per Idempotency-Key, and shifts
/// limited to the teams the caller manages.
/// </summary>
public sealed class WorkforceApiTests
{
    private static readonly DateOnly _monday = TimeClockKit.Monday;

    [Fact]
    public async Task APunch_IsRecordedOncePerIdempotencyKey()
    {
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk");

        await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);
        kit.Now = TimeClockKit.Local(_monday, 9);

        var user = CalendarTestHarness.User("user-ana", WorkforcePermissions.PunchTime.Name);
        var first = await kit.Harness.RunAsync(services => WorkforceApiEndpoints.PunchAsync(new WorkforcePunchInput { Kind = TimePunchKind.In }, Context(services, user, "key-1"), services));
        var second = await kit.Harness.RunAsync(services => WorkforceApiEndpoints.PunchAsync(new WorkforcePunchInput { Kind = TimePunchKind.In }, Context(services, user, "key-1"), services));
        var third = await kit.Harness.RunAsync(services => WorkforceApiEndpoints.PunchAsync(new WorkforcePunchInput { Kind = TimePunchKind.In }, Context(services, user, "key-2"), services));

        Assert.Equal(200, Status(first));
        Assert.Equal(Value(first).GetProperty("id").GetString(), Value(second).GetProperty("id").GetString());
        Assert.Equal(400, Status(third));
    }

    [Fact]
    public async Task Shifts_AreLimitedToTheTeamsTheCallerManages()
    {
        await using var harness = await WorkforceTests.CreateAsync();
        var east = await WorkforceTests.AddTeamAsync(harness, "East", TeamKind.Team);
        var west = await WorkforceTests.AddTeamAsync(harness, "West", TeamKind.Team);

        foreach (var (team, name) in new[] { (east, "Ana"), (west, "Ben") })
        {
            var worker = await PlanningKit.AddMemberAsync(harness, name, team);
            var period = await PlanningKit.CreatePeriodAsync(harness, team);

            await PlanningKit.AddDraftAsync(harness, period, worker.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
            await PlanningKit.PublishAsync(harness, period);
        }

        await WorkforceTests.AddWorkerAsync(harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = east.ItemId, Role = TeamMemberRole.Manager });
        });

        var maya = CalendarTestHarness.User("maya");
        var all = await harness.RunAsync(services => WorkforceApiEndpoints.ListShiftsAsync(null, "2026-11-09", "2026-11-10", Context(services, maya), services), commit: false);
        var westOnly = await harness.RunAsync(services => WorkforceApiEndpoints.ListShiftsAsync(west.ItemId, "2026-11-09", "2026-11-10", Context(services, maya), services), commit: false);
        var mine = await harness.RunAsync(services => WorkforceApiEndpoints.MyScheduleAsync("2026-11-09", 2, Context(services, CalendarTestHarness.User("user-ana")), services), commit: false);

        Assert.Equal(["Ana"], Value(all).EnumerateArray().Select(item => item.GetProperty("worker").GetString()));
        Assert.Equal(403, Status(westOnly));
        Assert.Single(Value(mine).EnumerateArray());
    }

    private static DefaultHttpContext Context(IServiceProvider services, System.Security.Claims.ClaimsPrincipal user, string idempotencyKey = null)
    {
        var context = new DefaultHttpContext { User = user, RequestServices = services };

        if (idempotencyKey is not null)
        {
            context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        }

        return context;
    }

    private static int Status(IResult result)
    {
        return result switch
        {
            IStatusCodeHttpResult { StatusCode: { } code } => code,
            ForbidHttpResult => 403,
            _ => 0,
        };
    }

    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    private static JsonElement Value(IResult result)
    {
        return JsonSerializer.SerializeToElement(((IValueHttpResult)result).Value, _web);
    }
}
