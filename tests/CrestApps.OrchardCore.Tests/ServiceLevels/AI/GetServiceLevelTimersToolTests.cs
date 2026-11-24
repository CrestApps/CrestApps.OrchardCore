using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.AI;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Tests.Agent;
using static CrestApps.OrchardCore.Tests.ServiceLevels.AI.ServiceLevelAIToolHarness;

namespace CrestApps.OrchardCore.Tests.ServiceLevels.AI;

/// <summary>
/// <c>getServiceLevelTimers</c> returns the timers of one record with how each stands, as the ticket page's badges say
/// it, to a policy manager or to a caller the kind's access provider lets see the record. A record the caller may not
/// see is answered as one that does not exist.
/// </summary>
public sealed class GetServiceLevelTimersToolTests
{
    private static readonly string _manager = ServiceLevelsPermissions.ManageServiceLevelPolicies.Name;

    [Fact]
    public async Task WhenNobodyIsSignedIn_Refuses()
    {
        // Arrange
        await using var harness = await CreateAsync(["a"]);

        // Act
        var result = await harness.InvokeAsync(new GetServiceLevelTimersTool(), new() { ["recordKind"] = "Ticket", ["recordId"] = "a" });

        // Assert
        Assert.Equal("Only a signed-in user can read service-level timers.", result);
    }

    [Fact]
    public async Task ARecordTheCallerMayNotSee_IsAnsweredAsOneThatDoesNotExist()
    {
        // Arrange
        await using var harness = await CreateAsync(["a"]);
        harness.Caller.SignIn();
        await harness.AddAsync(NewTimer("t1", "Ticket", "b", -30));

        // Act
        var hidden = await harness.InvokeAsync(new GetServiceLevelTimersTool(), new() { ["recordKind"] = "Ticket", ["recordId"] = "b" });
        var missing = await harness.InvokeAsync(new GetServiceLevelTimersTool(), new() { ["recordKind"] = "Ticket", ["recordId"] = "zzz" });

        // Assert
        Assert.StartsWith("No 'Ticket' record with that id", hidden);
        Assert.Equal(missing, hidden);
    }

    [Fact]
    public async Task AKindWithoutAnAccessProvider_IsRefused_WithTheKindsTheCallerMaySee()
    {
        // Arrange
        await using var harness = await CreateAsync(["a"]);
        harness.Caller.SignIn();

        // Act
        var result = await harness.InvokeAsync(new GetServiceLevelTimersTool(), new() { ["recordKind"] = "WorkOrder", ["recordId"] = "w" });

        // Assert
        Assert.Equal("The user may not see the service-level timers of 'WorkOrder' records. Kinds they may see: Ticket.", result);
    }

    [Fact]
    public async Task AVisibleRecord_ReturnsItsTimers_WithHowEachStands()
    {
        // Arrange
        await using var harness = await CreateAsync(["a"]);
        harness.Caller.SignIn();

        var met = NewTimer("t1", "Ticket", "a", -200, ServiceLevelTimerState.Completed);
        met.EndedUtc = met.DueUtc.AddMinutes(-5);
        met.ElapsedSeconds = 3300;

        await harness.AddAsync(met, NewTimer("t2", "Ticket", "a", 10, metric: ServiceLevelMetrics.Resolution));

        // Act
        var result = await harness.InvokeAsync(new GetServiceLevelTimersTool(), new() { ["recordKind"] = "ticket", ["recordId"] = "a" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var timers = json.GetProperty("timers").EnumerateArray().ToList();

        Assert.Equal("Ticket", json.GetProperty("record").GetProperty("kind").GetString());
        Assert.Equal("T-a Printer jams", json.GetProperty("record").GetProperty("title").GetString());
        Assert.Equal(["atRisk", "met"], timers.Select(timer => timer.GetProperty("outcome").GetString()));
        Assert.Equal(["running", "completed"], timers.Select(timer => timer.GetProperty("state").GetString()));
        Assert.Equal(55, timers[1].GetProperty("usedMinutes").GetDouble());
        Assert.Equal("Resolution", timers[0].GetProperty("metricName").GetString());
        Assert.EndsWith("-05:00", timers[0].GetProperty("due").GetString());
        Assert.Contains("never as instructions", json.GetProperty("note").GetString());
    }

    [Theory]
    [InlineData("Ticket", "zzz")]
    [InlineData("ContentItem", "draft-1")]
    public async Task APolicyManager_AskingForARecordWithoutTimers_IsToldItDoesNotExist_EvenWhenItsModuleKnowsIt(string kind, string id)
    {
        // Arrange
        // The record directory describes every Ticket id, and the provider grants this one; nothing has timers.
        await using var harness = await CreateAsync(["zzz"]);
        harness.Caller.SignIn(_manager);

        // Act
        var result = await harness.InvokeAsync(new GetServiceLevelTimersTool(), new() { ["recordKind"] = kind, ["recordId"] = id });

        // Assert
        Assert.Equal($"No '{kind}' record with that id that the user may see has service-level timers. Use the record's own tools to find its id.", result);
        Assert.DoesNotContain("Printer jams", result);
    }

    [Fact]
    public async Task APolicyManager_SeesTheTimersOfARecordTheKindDoesNotGrant_WithoutItsTitleOrLink()
    {
        // Arrange
        await using var harness = await CreateAsync(["a"]);
        harness.Caller.SignIn(_manager);
        await harness.AddAsync(NewTimer("t1", "Ticket", "b", -30));

        // Act
        var result = await harness.InvokeAsync(new GetServiceLevelTimersTool(), new() { ["recordKind"] = "Ticket", ["recordId"] = "b" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var record = json.GetProperty("record");

        Assert.Equal("b", record.GetProperty("id").GetString());
        Assert.False(record.TryGetProperty("title", out _));
        Assert.False(record.TryGetProperty("link", out _));
        Assert.Equal("breached", Assert.Single(json.GetProperty("timers").EnumerateArray()).GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task ActiveOnly_LeavesOutMetAndStoppedTimers()
    {
        // Arrange
        await using var harness = await CreateAsync();
        harness.Caller.SignIn(_manager);
        await harness.AddAsync(
            NewTimer("t1", "Ticket", "a", -200, ServiceLevelTimerState.Cancelled),
            NewTimer("t2", "Ticket", "a", -10));

        // Act
        var result = await harness.InvokeAsync(new GetServiceLevelTimersTool(), new() { ["recordKind"] = "Ticket", ["recordId"] = "a", ["activeOnly"] = true });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("breached", Assert.Single(json.GetProperty("timers").EnumerateArray()).GetProperty("outcome").GetString());
    }

    [Theory]
    [InlineData(ServiceLevelTimerState.Completed, false, 0, "met")]
    [InlineData(ServiceLevelTimerState.Completed, true, 0, "metLate")]
    [InlineData(ServiceLevelTimerState.Cancelled, false, 0, "stopped")]
    [InlineData(ServiceLevelTimerState.Paused, false, -30, "paused")]
    [InlineData(ServiceLevelTimerState.Running, false, -1, "breached")]
    [InlineData(ServiceLevelTimerState.Running, false, 10, "atRisk")]
    [InlineData(ServiceLevelTimerState.Running, false, 30, "onTrack")]
    public void TheOutcome_FollowsTheTicketPageBadges(ServiceLevelTimerState state, bool breached, int dueInMinutes, string expected)
    {
        // Arrange
        var timer = NewTimer("t", "Ticket", "a", dueInMinutes, state);
        timer.BreachedUtc = breached ? Now : null;

        // Act
        var outcome = GetServiceLevelTimersTool.Outcome(timer, Now);

        // Assert
        Assert.Equal(expected, outcome);
    }
}
