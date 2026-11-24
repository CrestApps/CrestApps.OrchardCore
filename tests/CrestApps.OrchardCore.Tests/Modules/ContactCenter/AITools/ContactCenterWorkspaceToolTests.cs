using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// The agent's own workspace tools act on the caller only and keep the memberships an agent already has; the interval
/// statistics tools read the status the page reads and only queue a rebuild; the health tool runs only the dependency
/// checks. Every tool refuses a caller who is not signed in or lacks its permission before anything is read.
/// </summary>
public sealed class ContactCenterWorkspaceToolTests
{
    private const string UserId = AgentToolTestContext.UserId;

    private readonly Mock<IAgentSessionService> _sessions = new();
    private readonly Mock<IAgentProfileManager> _agents = new();
    private readonly Mock<IAgentPresenceManager> _presence = new();
    private readonly Mock<IInteractionManager> _interactions = new();
    private readonly Mock<IActivityQueueManager> _queues = new();
    private readonly List<ActivityQueue> _knownQueues = [];

    public ContactCenterWorkspaceToolTests()
    {
        _queues.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _knownQueues.ToList());
    }

    public static TheoryData<string> ToolNames
        => new()
        {
            GetMyAgentWorkStateTool.TheName,
            SetContactCenterAgentStateTool.TheName,
            GetIntervalStatisticsStatusTool.TheName,
            RebuildIntervalStatisticsTool.TheName,
            GetContactCenterHealthTool.TheName,
        };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        var services = Build(signedIn: false);

        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
        AssertNothingReached();
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName)
    {
        var services = Build(authorize: (_, _) => false);

        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
        AssertNothingReached();
    }

    [Fact]
    public async Task GetMyAgentWorkState_ReturnsTheCallersQueuesAndCampaignsWithoutCampaignQueues()
    {
        AddQueue("q1", "Support");
        _sessions.Setup(service => service.BuildSnapshotAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new AgentDesktopSnapshot
        {
            UserId = UserId,
            HasProfile = true,
            PresenceStatus = nameof(AgentPresenceStatus.Break),
            PresenceReason = "Lunch",
            QueueIds = ["q1", ContactCenterConstants.CampaignQueue.CreateId("c1")],
            CampaignIds = ["c1"],
            IsOnline = false,
        });
        _agents.Setup(manager => manager.FindByUserIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = UserId });

        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetMyAgentWorkStateTool(), Build()));

        Assert.True(result.GetProperty("signedIn").GetBoolean());
        Assert.Equal("Break", result.GetProperty("presence").GetString());
        var queues = result.GetProperty("queues").EnumerateArray().ToList();
        Assert.Single(queues);
        Assert.Equal("Support", queues[0].GetProperty("name").GetString());
        Assert.Single(result.GetProperty("campaigns").EnumerateArray());
        Assert.Contains("no calls are offered", result.GetProperty("note").GetString());
        _sessions.Verify(service => service.BuildSnapshotAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyAgentWorkState_SaysWhenTheCallerHasNoAgentProfile()
    {
        _sessions.Setup(service => service.BuildSnapshotAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new AgentDesktopSnapshot { UserId = UserId });

        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetMyAgentWorkStateTool(), Build()));

        Assert.False(result.GetProperty("hasAgentProfile").GetBoolean());
        Assert.False(result.GetProperty("signedIn").GetBoolean());
    }

    [Fact]
    public async Task SetContactCenterAgentState_Self_SignIn_AddsToTheQueuesOfASignedInAgentWithoutChangingPresence()
    {
        AddQueue("q1", "Support");
        AddQueue("q2", "Sales");
        var profile = new AgentProfile { ItemId = "agent-1", UserId = UserId, PresenceStatus = AgentPresenceStatus.Break, QueueIds = ["q2"] };
        _agents.Setup(manager => manager.FindByUserIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _presence.Setup(manager => manager.UpdateMembershipsAsync(UserId, It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = UserId, PresenceStatus = AgentPresenceStatus.Break, QueueIds = ["q2", "q1"] });
        SetUpSnapshot();

        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), Build(), new Dictionary<string, object>
        {
            ["action"] = "signIn",
            ["queues"] = new[] { "Support" },
        });

        Assert.Contains("presence did not change", result);
        _presence.Verify(manager => manager.UpdateMembershipsAsync(
            UserId,
            It.Is<IEnumerable<string>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { "q1", "q2" })),
            It.Is<IEnumerable<string>>(ids => !ids.Any()),
            It.IsAny<CancellationToken>()), Times.Once);
        _presence.Verify(manager => manager.SignInAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetContactCenterAgentState_Self_SignIn_SignsAnOfflineAgentInToTheQueuesGiven()
    {
        AddQueue("q1", "Support");
        _agents.Setup(manager => manager.FindByUserIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = UserId, PresenceStatus = AgentPresenceStatus.Offline, QueueIds = ["q9"] });
        _presence.Setup(manager => manager.SignInAsync(UserId, It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = UserId, PresenceStatus = AgentPresenceStatus.Available, QueueIds = ["q1"] });
        SetUpSnapshot();

        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), Build(), new Dictionary<string, object>
        {
            ["action"] = "signIn",
            ["queues"] = new[] { "q1" },
        });

        Assert.Contains("You were signed in.", result);
        _presence.Verify(manager => manager.SignInAsync(UserId, It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "q1" })), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetContactCenterAgentState_Self_RefusesAQueueThatDoesNotExist()
    {
        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), Build(), new Dictionary<string, object>
        {
            ["action"] = "signIn",
            ["queues"] = new[] { "Nowhere" },
        });

        Assert.StartsWith("Nothing was changed: no queue or campaign has the id or name 'Nowhere'", result);
        Assert.Empty(_presence.Invocations);
    }

    [Fact]
    public async Task SetContactCenterAgentState_Self_ReportsAnEntitlementRefusal()
    {
        AddQueue("q1", "Support");
        _presence.Setup(manager => manager.SignInAsync(UserId, It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AgentEntitlementDeniedException(UserId));

        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), Build(), new Dictionary<string, object>
        {
            ["action"] = "signIn",
            ["queues"] = new[] { "q1" },
        });

        Assert.StartsWith("Nothing was changed: you are not allowed to sign in", result);
    }

    [Fact]
    public async Task SetContactCenterAgentState_Self_RefusesToLeaveTheLastQueue()
    {
        AddQueue("q1", "Support");
        _agents.Setup(manager => manager.FindByUserIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = UserId, PresenceStatus = AgentPresenceStatus.Available, QueueIds = ["q1"] });

        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), Build(), new Dictionary<string, object>
        {
            ["action"] = "leave",
            ["queues"] = new[] { "q1" },
        });

        Assert.Contains("Use the signOut action", result);
        Assert.Empty(_presence.Invocations);
    }

    [Fact]
    public async Task SetContactCenterAgentState_Self_SetsTheCallersOwnPresenceWithTheReason()
    {
        _presence.Setup(manager => manager.SetPresenceAsync(UserId, AgentPresenceStatus.Break, "lunch", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = UserId, PresenceStatus = AgentPresenceStatus.Break, QueueIds = ["q1"] });
        SetUpSnapshot();

        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), Build(), new Dictionary<string, object>
        {
            ["action"] = "setPresence",
            ["status"] = "Break",
            ["reason"] = "lunch",
        });

        Assert.Contains("Your presence is now Break.", result);
        _presence.Verify(manager => manager.SetPresenceAsync(UserId, AgentPresenceStatus.Break, "lunch", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetContactCenterAgentState_Self_NeedsOnlySignIntoQueues()
    {
        _presence.Setup(manager => manager.SetPresenceAsync(UserId, AgentPresenceStatus.Away, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = UserId, PresenceStatus = AgentPresenceStatus.Away, QueueIds = ["q1"] });
        SetUpSnapshot();
        var services = Build(authorize: (permission, _) => permission.Name == ContactCenterPermissions.SignIntoQueues.Name);

        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), services, new Dictionary<string, object>
        {
            ["action"] = "setPresence",
            ["status"] = "Away",
        });

        Assert.Contains("Your presence is now Away.", result);
    }

    [Fact]
    public async Task SetContactCenterAgentState_Self_RefusesAStateAnAgentCannotChoose()
    {
        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), Build(), new Dictionary<string, object>
        {
            ["action"] = "setPresence",
            ["status"] = "Busy",
        });

        Assert.StartsWith("Nothing was changed: status must be one of", result);
        Assert.Empty(_presence.Invocations);
    }

    [Fact]
    public void IntervalStatisticsStatus_AddsTodaysIntervalsAndSeesAStalledRebuild()
    {
        var now = new DateTime(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);
        var state = new IntervalStatisticsState
        {
            RebuildFromUtc = now.Date.AddDays(-7),
            RebuildToUtc = now.Date,
            RebuildNextUtc = now.Date.AddDays(-3),
            RebuildRequestedUtc = now.AddHours(-2),
            LastProgressUtc = now.AddMinutes(-45),
        };
        IntervalStatisticsDay[] days =
        [
            new() { Date = now.Date, Final = false, InteractionCount = 4 },
            new() { Date = now.Date.AddDays(-1), Final = true },
        ];
        QueueIntervalStatistic[] intervals =
        [
            new() { QueueId = "q1", Offered = 3, Answered = 2 },
            new() { QueueId = "q2", Offered = 1, Answered = 1 },
        ];

        var status = IntervalStatisticsStatus.Build(state, days, intervals, now);

        Assert.Equal(2, status.DaysComputed);
        Assert.Equal(1, status.DaysFinal);
        Assert.Equal(4, status.TodayTotal.Offered);
        Assert.Equal(2, status.QueuesToday);
        Assert.NotNull(status.Today);
        Assert.True(status.Stalled);
    }

    [Fact]
    public async Task RebuildIntervalStatistics_RefusesARangeThatEndsBeforeItStarts()
    {
        var result = await AgentToolTestContext.InvokeAsync(new RebuildIntervalStatisticsTool(), Build(), new Dictionary<string, object>
        {
            ["from"] = "2026-11-10",
            ["to"] = "2026-11-02",
        });

        Assert.Equal("Nothing was queued: the last day must not be before the first day.", result);
    }

    [Fact]
    public async Task RebuildIntervalStatistics_RefusesSomethingThatIsNotADate()
    {
        var result = await AgentToolTestContext.InvokeAsync(new RebuildIntervalStatisticsTool(), Build(), new Dictionary<string, object>
        {
            ["from"] = "last week",
            ["to"] = "2026-11-02",
        });

        Assert.StartsWith("Nothing was queued: give from and to as dates", result);
    }

    [Fact]
    public async Task GetContactCenterHealth_RunsOnlyTheDependencyChecks()
    {
        var services = Build(configure: collection =>
        {
            collection.AddLogging();
            collection.AddHealthChecks()
                .AddCheck("cc-backlog", () => HealthCheckResult.Degraded("Queue backlog is growing."), tags: [ContactCenterConstants.HealthChecks.DependencyTag])
                .AddCheck("cc-ready", () => HealthCheckResult.Unhealthy("not a dependency"), tags: [ContactCenterConstants.HealthChecks.ReadyTag]);
        });

        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetContactCenterHealthTool(), services));

        Assert.Equal("Degraded", result.GetProperty("status").GetString());
        var check = Assert.Single(result.GetProperty("checks").EnumerateArray());
        Assert.Equal("cc-backlog", check.GetProperty("name").GetString());
        Assert.Equal("Queue backlog is growing.", check.GetProperty("description").GetString());
        Assert.Equal(1, result.GetProperty("degraded").GetInt32());
    }

    [Fact]
    public async Task GetContactCenterHealth_SaysWhenHealthChecksAreOff()
    {
        var result = await AgentToolTestContext.InvokeAsync(new GetContactCenterHealthTool(), Build());

        Assert.StartsWith("The health checks are not available", result);
    }

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            GetMyAgentWorkStateTool.TheName => new GetMyAgentWorkStateTool(),
            SetContactCenterAgentStateTool.TheName => new SetContactCenterAgentStateTool(),
            GetIntervalStatisticsStatusTool.TheName => new GetIntervalStatisticsStatusTool(),
            RebuildIntervalStatisticsTool.TheName => new RebuildIntervalStatisticsTool(),
            _ => new GetContactCenterHealthTool(),
        };

    private static Dictionary<string, object> Arguments()
        => new()
        {
            ["action"] = "signIn",
            ["queues"] = new[] { "q1" },
            ["status"] = "Available",
            ["from"] = "2026-11-01",
            ["to"] = "2026-11-02",
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null, Action<IServiceCollection> configure = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(_sessions.Object);
        services.AddSingleton(_agents.Object);
        services.AddSingleton(_presence.Object);
        services.AddSingleton(_interactions.Object);
        services.AddSingleton(_queues.Object);
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private void AddQueue(string id, string name)
    {
        var queue = new ActivityQueue { ItemId = id, Name = name };

        _knownQueues.Add(queue);
        _queues.Setup(manager => manager.FindByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(queue);
    }

    private void SetUpSnapshot()
        => _sessions.Setup(service => service.BuildSnapshotAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new AgentDesktopSnapshot
        {
            UserId = UserId,
            HasProfile = true,
            PresenceStatus = nameof(AgentPresenceStatus.Available),
            QueueIds = ["q1"],
            IsOnline = true,
        });

    private void AssertNothingReached()
    {
        Assert.Empty(_sessions.Invocations);
        Assert.Empty(_agents.Invocations);
        Assert.Empty(_presence.Invocations);
        Assert.Empty(_interactions.Invocations);
        Assert.Empty(_queues.Invocations);
    }
}
