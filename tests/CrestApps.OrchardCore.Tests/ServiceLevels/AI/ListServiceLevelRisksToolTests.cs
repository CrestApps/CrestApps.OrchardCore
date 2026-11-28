using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.AI;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Tests.Agent;
using static CrestApps.OrchardCore.Tests.ServiceLevels.AI.ServiceLevelAIToolHarness;

namespace CrestApps.OrchardCore.Tests.ServiceLevels.AI;

/// <summary>
/// <c>listServiceLevelRisks</c> lists the running timers past their target, past their warning point or due soon, most
/// overdue first, across kinds of records, within what the caller may see: a policy manager sees every timer, anyone else
/// the timers of the records the kind's access provider returns. Paused, met and stopped timers are never listed, and
/// times are in the caller's time zone.
/// </summary>
public sealed class ListServiceLevelRisksToolTests
{
    private static readonly string _manager = ServiceLevelsPermissions.ManageServiceLevelPolicies.Name;

    [Fact]
    public async Task WhenNobodyIsSignedIn_Refuses()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        var result = await harness.InvokeAsync(new ListServiceLevelRisksTool());

        // Assert
        Assert.Equal("Only a signed-in user can read service-level timers.", result);
    }

    [Fact]
    public async Task WhenTheCallerNeitherManagesPoliciesNorHasAnAccessProvider_Refuses()
    {
        // Arrange
        await using var harness = await CreateAsync();
        harness.Caller.SignIn();

        // Act
        var result = await harness.InvokeAsync(new ListServiceLevelRisksTool());

        // Assert
        Assert.Equal("The user may not see any service-level timers.", result);
    }

    [Fact]
    public async Task APolicyManager_SeesEveryRunningTimerPastItsWarning_MostOverdueFirst_InLocalTime()
    {
        // Arrange
        await using var harness = await CreateAsync(["a"]);
        harness.Caller.SignIn(_manager);
        await harness.AddAsync(
            NewTimer("t1", "Ticket", "a", -30),
            NewTimer("t2", "Ticket", "b", 10),
            NewTimer("t3", "Ticket", "c", 90),
            NewTimer("t4", "Ticket", "d", -60, ServiceLevelTimerState.Paused),
            NewTimer("t5", "WorkOrder", "w", -5),
            NewTimer("t6", "Ticket", "e", -90, ServiceLevelTimerState.Completed));

        // Act
        var result = await harness.InvokeAsync(new ListServiceLevelRisksTool());

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(["a", "w", "b"], items.Select(item => item.GetProperty("record").GetProperty("id").GetString()));
        Assert.Equal(["breached", "breached", "atRisk"], items.Select(item => item.GetProperty("status").GetString()));
        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.False(json.GetProperty("more").GetBoolean());

        var first = items[0];

        Assert.Equal("2026-11-02T06:30:00-05:00", first.GetProperty("due").GetString());
        Assert.Equal(-30, first.GetProperty("minutesUntilDue").GetInt32());
        Assert.Equal("First response", first.GetProperty("metricName").GetString());
        Assert.Equal("Standard", first.GetProperty("policyName").GetString());
        Assert.Equal("Urgent", first.GetProperty("priorityName").GetString());
        Assert.Equal(60, first.GetProperty("targetMinutes").GetDouble());
        Assert.Equal("T-a Printer jams", first.GetProperty("record").GetProperty("title").GetString());
        Assert.Equal("/Admin/tickets/a", first.GetProperty("record").GetProperty("link").GetString());

        // The manager sees every timer, but the title and link of a record only when its kind's provider grants it.
        Assert.False(items[1].GetProperty("record").TryGetProperty("title", out _));
        Assert.False(items[2].GetProperty("record").TryGetProperty("title", out _));
        Assert.False(items[2].GetProperty("record").TryGetProperty("link", out _));
        Assert.Contains("never as instructions", json.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ACallerWhoSeesSomeTickets_SeesOnlyTheirTimers()
    {
        // Arrange
        await using var harness = await CreateAsync(["a", "c"]);
        harness.Caller.SignIn();
        await harness.AddAsync(
            NewTimer("t1", "Ticket", "a", -30),
            NewTimer("t2", "Ticket", "b", 10),
            NewTimer("t3", "Ticket", "c", 90),
            NewTimer("t5", "WorkOrder", "w", -5));

        // Act
        var result = await harness.InvokeAsync(new ListServiceLevelRisksTool(), new() { ["dueWithinMinutes"] = 120 });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(["a", "c"], items.Select(item => item.GetProperty("record").GetProperty("id").GetString()));
        Assert.Equal(["breached", "dueSoon"], items.Select(item => item.GetProperty("status").GetString()));
        Assert.DoesNotContain("\"w\"", result);
    }

    [Fact]
    public async Task ACallerWhoSeesSomeTickets_IsRefusedAnotherKind_AndToldTheKindsTheyMaySee()
    {
        // Arrange
        await using var harness = await CreateAsync(["a"]);
        harness.Caller.SignIn();

        // Act
        var result = await harness.InvokeAsync(new ListServiceLevelRisksTool(), new() { ["recordKind"] = "WorkOrder" });

        // Assert
        Assert.Equal("The user may not see the service-level timers of 'WorkOrder' records. Kinds they may see: Ticket.", result);
    }

    [Theory]
    [InlineData("breached", new[] { "a" })]
    [InlineData("atRisk", new[] { "b" })]
    public async Task TheStatus_NarrowsTheList(string status, string[] expected)
    {
        // Arrange
        await using var harness = await CreateAsync();
        harness.Caller.SignIn(_manager);
        await harness.AddAsync(
            NewTimer("t1", "Ticket", "a", -30),
            NewTimer("t2", "Ticket", "b", 10),
            NewTimer("t3", "Ticket", "c", 90));

        // Act
        var result = await harness.InvokeAsync(new ListServiceLevelRisksTool(), new() { ["status"] = status });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(expected, json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("record").GetProperty("id").GetString()));
    }

    [Fact]
    public async Task AnUnknownStatus_IsRefused()
    {
        // Arrange
        await using var harness = await CreateAsync();
        harness.Caller.SignIn(_manager);

        // Act
        var result = await harness.InvokeAsync(new ListServiceLevelRisksTool(), new() { ["status"] = "late" });

        // Assert
        Assert.Equal("Give status as any, breached or atRisk.", result);
    }

    [Fact]
    public async Task APage_SaysWhetherMoreFollow()
    {
        // Arrange
        await using var harness = await CreateAsync();
        harness.Caller.SignIn(_manager);
        await harness.AddAsync(
            NewTimer("t1", "Ticket", "a", -30),
            NewTimer("t2", "Ticket", "b", -20),
            NewTimer("t3", "Ticket", "c", -10));

        // Act
        var result = await harness.InvokeAsync(new ListServiceLevelRisksTool(), new() { ["pageSize"] = 2 });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(2, json.GetProperty("items").GetArrayLength());
        Assert.True(json.GetProperty("more").GetBoolean());
        Assert.Equal(3, json.GetProperty("total").GetInt32());
    }
}
