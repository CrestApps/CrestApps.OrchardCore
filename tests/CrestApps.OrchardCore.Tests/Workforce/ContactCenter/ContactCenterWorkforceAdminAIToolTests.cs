using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.ContactCenter;
using CrestApps.OrchardCore.Workforce.ContactCenter.AI;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;
using CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// The contact center workforce AI tools (adherence, intraday, forecast reads, agents' workers and Time Tracker stages):
/// they refuse a caller who is not signed in or lacks the permission, keep to the workers the caller manages, and act
/// through the same services as the admin pages. The forecasting and adherence record kinds are read-only.
/// </summary>
public sealed class ContactCenterWorkforceAdminAIToolTests
{
    internal static readonly (string Name, Type Startup, bool RequiresApproval)[] NewTools =
    [
        (CreateWorkersForAgentsTool.ToolName, typeof(ContactCenterWorkersAIToolsStartup), false),
        (GetContactCenterTimeStagesTool.ToolName, typeof(ContactCenterTimeStagesAIToolsStartup), false),
        (SaveContactCenterTimeStagesTool.ToolName, typeof(ContactCenterTimeStagesAIToolsStartup), false),
        (GetAdherenceDayTool.ToolName, typeof(AdherenceAdminAIToolsStartup), false),
        (ListAdherenceExceptionsTool.ToolName, typeof(AdherenceAdminAIToolsStartup), false),
        (AddAdherenceExceptionTool.ToolName, typeof(AdherenceAdminAIToolsStartup), false),
        (DeleteAdherenceExceptionTool.ToolName, typeof(AdherenceAdminAIToolsStartup), false),
        (GetIntradayStatusTool.ToolName, typeof(IntradayAIToolsStartup), false),
        (ReforecastIntradayTool.ToolName, typeof(IntradayAIToolsStartup), false),
        (PostIntradayOfferTool.ToolName, typeof(IntradayAIToolsStartup), true),
        (CloseIntradayOfferTool.ToolName, typeof(IntradayAIToolsStartup), false),
        (ListForecastsTool.ToolName, typeof(ForecastingAdminAIToolsStartup), false),
        (GetForecastTool.ToolName, typeof(ForecastingAdminAIToolsStartup), false),
        (GetCapacityPlanProjectionTool.ToolName, typeof(ForecastingAdminAIToolsStartup), false),
    ];

    private static readonly DateOnly _monday = PlanningKit.Monday;

    public static TheoryData<string> ToolNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var (name, _, _) in NewTools)
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        await using var setup = await SetupAsync();

        var result = await InvokeAsync(setup, toolName, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["workerId"] = "x" });

        Assert.Equal(ContactCenterWorkforceToolBase.SignInRequired, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName)
    {
        await using var setup = await SetupAsync();

        var result = await InvokeAsync(setup, toolName, CalendarTestHarness.User("someone", WorkforcePermissions.UseWorkforcePortal.Name), new() { ["workerId"] = "x" });

        Assert.StartsWith("The user isn't allowed to do this", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdherenceExceptions_AreAddedListedAndDeleted_ForAWorkerTheCallerManages()
    {
        await using var setup = await SetupAsync();
        var manager = await AddManagerAsync(setup);

        var added = await InvokeAsync(setup, AddAdherenceExceptionTool.ToolName, manager, new()
        {
            ["workerId"] = setup.Ana.ItemId,
            ["date"] = "2026-11-09",
            ["start"] = "10:00",
            ["end"] = "10:45",
            ["reason"] = "Coaching",
            ["note"] = "One to one",
        });
        var addedRoot = JsonElement.Parse(added);
        var id = addedRoot.GetProperty("id").GetString();

        Assert.True(addedRoot.GetProperty("recorded").GetBoolean());
        Assert.Equal("2026-11-09T10:00", addedRoot.GetProperty("start").GetString());
        Assert.Equal("2026-11-09T10:45", addedRoot.GetProperty("end").GetString());

        var listed = JsonElement.Parse(await InvokeAsync(setup, ListAdherenceExceptionsTool.ToolName, manager, new()));
        var item = Assert.Single(listed.GetProperty("items").EnumerateArray());

        Assert.Equal(id, item.GetProperty("id").GetString());
        Assert.Equal("Ana", item.GetProperty("worker").GetString());
        Assert.Equal("Coaching", item.GetProperty("reason").GetString());

        var deleted = JsonElement.Parse(await InvokeAsync(setup, DeleteAdherenceExceptionTool.ToolName, manager, new() { ["exceptionId"] = id }));
        var after = JsonElement.Parse(await InvokeAsync(setup, ListAdherenceExceptionsTool.ToolName, manager, new()));
        var missing = await InvokeAsync(setup, DeleteAdherenceExceptionTool.ToolName, manager, new() { ["exceptionId"] = id });

        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.Equal(0, after.GetProperty("total").GetInt32());
        Assert.Equal($"No adherence exception has the ID '{id}'.", missing);
    }

    [Fact]
    public async Task AddAdherenceException_RefusesAWorkerOfATeamTheCallerDoesNotManage_AndABadReason()
    {
        await using var setup = await SetupAsync();
        var manager = await AddManagerAsync(setup);

        var other = await InvokeAsync(setup, AddAdherenceExceptionTool.ToolName, manager, new()
        {
            ["workerId"] = setup.Ben.ItemId,
            ["date"] = "2026-11-09",
            ["start"] = "10:00",
            ["end"] = "10:30",
            ["reason"] = "Coaching",
        });
        var badReason = await InvokeAsync(setup, AddAdherenceExceptionTool.ToolName, manager, new()
        {
            ["workerId"] = setup.Ana.ItemId,
            ["date"] = "2026-11-09",
            ["start"] = "10:00",
            ["end"] = "10:30",
            ["reason"] = "1",
        });

        Assert.Contains("doesn't manage", other, StringComparison.Ordinal);
        Assert.StartsWith("The reason must be", badReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAdherenceDay_SaysWhenTheWorkerIsNotAnAgent_AndRefusesAWorkerOutOfScope()
    {
        await using var setup = await SetupAsync();
        var manager = await AddManagerAsync(setup);

        var notAgent = await InvokeAsync(setup, GetAdherenceDayTool.ToolName, manager, new() { ["workerId"] = setup.Ana.ItemId, ["date"] = "2026-11-09" });
        var outOfScope = await InvokeAsync(setup, GetAdherenceDayTool.ToolName, manager, new() { ["workerId"] = setup.Ben.ItemId });

        Assert.Equal("Ana is not a contact center agent, so there is no adherence to show.", notAgent);
        Assert.Contains("doesn't manage", outOfScope, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntradayOffers_ArePostedListedAndClosed()
    {
        await using var setup = await SetupAsync();
        var planner = Planner();

        var posted = JsonElement.Parse(await InvokeAsync(setup, PostIntradayOfferTool.ToolName, planner, new()
        {
            ["kind"] = "VoluntaryTimeOff",
            ["date"] = "2026-11-09",
            ["start"] = "14:00",
            ["end"] = "16:00",
            ["places"] = 3,
        }));
        var id = posted.GetProperty("id").GetString();
        var status = JsonElement.Parse(await InvokeAsync(setup, GetIntradayStatusTool.ToolName, planner, new() { ["planningGroupName"] = "sales voice", ["date"] = "2026-11-09" }));
        var offer = Assert.Single(status.GetProperty("offers").EnumerateArray());
        var closed = JsonElement.Parse(await InvokeAsync(setup, CloseIntradayOfferTool.ToolName, planner, new() { ["offerId"] = id }));
        var again = JsonElement.Parse(await InvokeAsync(setup, CloseIntradayOfferTool.ToolName, planner, new() { ["offerId"] = id }));

        Assert.True(posted.GetProperty("offered").GetBoolean());
        Assert.Equal("2026-11-09T14:00", posted.GetProperty("start").GetString());
        Assert.Equal(id, offer.GetProperty("id").GetString());
        Assert.Equal(3, offer.GetProperty("places").GetInt32());
        Assert.True(closed.GetProperty("closed").GetBoolean());
        Assert.False(again.GetProperty("closed").GetBoolean());
    }

    [Fact]
    public async Task IntradayTools_RefuseAnUnknownPlanningGroup_AndBadPlaces()
    {
        await using var setup = await SetupAsync();
        var planner = Planner();

        var unknown = await InvokeAsync(setup, GetIntradayStatusTool.ToolName, planner, new() { ["planningGroupName"] = "Billing chat" });
        var noPlaces = await InvokeAsync(setup, PostIntradayOfferTool.ToolName, planner, new()
        {
            ["kind"] = "VoluntaryOvertime",
            ["date"] = "2026-11-09",
            ["start"] = "17:00",
            ["end"] = "18:00",
            ["places"] = 0,
        });

        Assert.StartsWith("No planning group is called 'Billing chat'.", unknown, StringComparison.Ordinal);
        Assert.Equal("Offer at least one place (places).", noPlaces);
    }

    [Fact]
    public async Task ForecastReads_ListAndGetAForecastDay_AndRefuseAnUnknownForecastOrPlan()
    {
        await using var setup = await SetupAsync();
        var planner = Planner();

        await setup.Harness.RunAsync(services => services.GetRequiredService<ISession>().SaveAsync(new Forecast
        {
            ItemId = "forecast",
            PlanningGroupId = setup.Group.ItemId,
            FromDate = _monday,
            ToDate = _monday,
            TimeZoneId = "America/New_York",
            Status = ForecastStatus.Active,
            Intervals =
            [
                new ForecastInterval { Date = _monday, StartUtc = PlanningKit.Utc(_monday, new TimeOnly(9, 0)), Volume = 10, AverageHandleSeconds = 300, RequiredAgents = 3, RequiredScheduled = 4 },
                new ForecastInterval { Date = _monday, StartUtc = PlanningKit.Utc(_monday, new TimeOnly(9, 15)), Volume = 30, AverageHandleSeconds = 200, RequiredAgents = 6, RequiredScheduled = 8 },
            ],
        }, collection: WorkforceConstants.CollectionName));

        var list = JsonElement.Parse(await InvokeAsync(setup, ListForecastsTool.ToolName, planner, new() { ["planningGroupName"] = "Sales voice" }));
        var forecast = JsonElement.Parse(await InvokeAsync(setup, GetForecastTool.ToolName, planner, new() { ["forecastId"] = "forecast" }));
        var unknown = await InvokeAsync(setup, GetForecastTool.ToolName, planner, new() { ["forecastId"] = "nope" });
        var outside = await InvokeAsync(setup, GetForecastTool.ToolName, planner, new() { ["forecastId"] = "forecast", ["day"] = "2026-12-01" });
        var plan = await InvokeAsync(setup, GetCapacityPlanProjectionTool.ToolName, planner, new() { ["capacityPlanName"] = "Next year" });

        Assert.Equal("Active", Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("status").GetString());
        Assert.Equal(40, forecast.GetProperty("day").GetProperty("contacts").GetDouble());
        Assert.Equal(2, forecast.GetProperty("day").GetProperty("intervals").GetArrayLength());
        Assert.Equal(8, forecast.GetProperty("day").GetProperty("busiest").GetProperty("agentsToSchedule").GetInt32());
        Assert.Equal("No forecast has the ID 'nope'.", unknown);
        Assert.StartsWith("The forecast covers 2026-11-09 to 2026-11-09", outside, StringComparison.Ordinal);
        Assert.StartsWith("No capacity plan is called 'Next year'.", plan, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateWorkersForAgents_SaysWhenEveryAgentHasAWorker_AndRefusesAnUnknownTeam()
    {
        await using var setup = await SetupAsync();
        var admin = CalendarTestHarness.User("admin", WorkforcePermissions.ManageWorkers.Name);

        var none = JsonElement.Parse(await InvokeAsync(setup, CreateWorkersForAgentsTool.ToolName, admin, new()));
        var unknown = await InvokeAsync(setup, CreateWorkersForAgentsTool.ToolName, admin, new() { ["teamName"] = "Nowhere" });

        Assert.Equal(0, none.GetProperty("created").GetInt32());
        Assert.Equal("No team is called 'Nowhere'. Nothing was created.", unknown);
    }

    [Fact]
    public async Task ContactCenterTimeStages_AreReadAndSet_AndAnUnknownStageSavesNothing()
    {
        await using var setup = await SetupAsync();
        var admin = CalendarTestHarness.User("admin", TimeTrackerPermissions.ManageTimeTrackerSettings.Name);

        var refused = await InvokeAsync(setup, SaveContactCenterTimeStagesTool.ToolName, admin, new()
        {
            ["states"] = new[] { new { state = "Busy", stage = "Nap" } },
        });

        Assert.StartsWith("Nothing was saved: No Time Tracker stage is called 'Nap'.", refused, StringComparison.Ordinal);
        Assert.Empty(setup.Site.GetOrCreate<ContactCenterTimeStages>().States);

        var saved = JsonElement.Parse(await InvokeAsync(setup, SaveContactCenterTimeStagesTool.ToolName, admin, new()
        {
            ["states"] = new[] { new { state = "Busy", stage = "on a call" } },
            ["reasons"] = new[] { new { reason = "lunch", stage = "Break" } },
        }));
        var read = JsonElement.Parse(await InvokeAsync(setup, GetContactCenterTimeStagesTool.ToolName, admin, new()));
        var stored = setup.Site.GetOrCreate<ContactCenterTimeStages>();

        Assert.True(saved.GetProperty("saved").GetBoolean());
        Assert.Equal("stage-call", stored.States["Busy"]);
        Assert.Equal("stage-break", stored.Reasons["Lunch"]);
        Assert.Equal("On a call", read.GetProperty("states").EnumerateArray().Single(state => state.GetProperty("state").GetString() == "Busy").GetProperty("stage").GetProperty("name").GetString());
        Assert.Equal("Break", Assert.Single(read.GetProperty("reasons").EnumerateArray()).GetProperty("stage").GetProperty("name").GetString());
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName)
    {
        var (_, startup, requiresApproval) = NewTools.Single(tool => tool.Name == toolName);
        var tools = GetTools(startup);

        Assert.True(tools.TryGetValue(toolName, out var tool), $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Workforce", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
        Assert.False(string.IsNullOrWhiteSpace(Create(toolName).Description));
        Assert.Equal(toolName, Create(toolName).Name);
    }

    [Fact]
    public void Startups_RequireTheAIFeature_RegisterOnlyTheNewTools_AndOneApproval()
    {
        var startups = NewTools.Select(tool => tool.Startup).Distinct().ToArray();
        var registered = startups.SelectMany(startup => GetTools(startup).Keys).ToArray();

        foreach (var startup in startups)
        {
            var required = startup.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();

            Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
            Assert.Single(startup.GetCustomAttributes(typeof(FeatureAttribute), inherit: false));
        }

        Assert.Equal(NewTools.Select(tool => tool.Name).Order(StringComparer.Ordinal), registered.Order(StringComparer.Ordinal));
        Assert.Single(NewTools, tool => tool.RequiresApproval);
        Assert.DoesNotContain("createForecast", registered);
        Assert.DoesNotContain("deleteForecast", registered);
        Assert.DoesNotContain(ForecastSummaryTool.ToolName, registered);
        Assert.DoesNotContain(OutOfAdherenceTool.ToolName, registered);
    }

    [Fact]
    public void RecordKinds_AreReadOnly_WithThePagesPermissions()
    {
        var kinds = GetKinds(typeof(ForecastingAdminAIToolsStartup)).Concat(GetKinds(typeof(AdherenceAdminAIToolsStartup))).ToDictionary(kind => kind.Name, StringComparer.Ordinal);

        Assert.Equal(["adherenceRuleSet", "capacityPlan", "forecastEvent", "planningGroup"], kinds.Keys.Order(StringComparer.Ordinal));
        Assert.All(kinds.Values, kind => Assert.False(kind.CanWrite));
        Assert.Equal(WorkforcePermissions.ManageAdherence.Name, kinds["adherenceRuleSet"].Permission.Name);
        Assert.Equal(WorkforcePermissions.ManageForecasts.Name, kinds["planningGroup"].Permission.Name);
        Assert.Equal(WorkforcePermissions.ManageForecasts.Name, kinds["forecastEvent"].Permission.Name);
        Assert.Equal(WorkforcePermissions.ManageForecasts.Name, kinds["capacityPlan"].Permission.Name);
    }

    [Fact]
    public async Task PlanningGroupKind_ListsByName_WithItsProjection()
    {
        await using var setup = await SetupAsync();
        var kind = (AIRecordKind<PlanningGroup>)GetKinds(typeof(ForecastingAdminAIToolsStartup)).Single(item => item.Name == "planningGroup");

        var page = await setup.Harness.RunAsync(async services => await kind.ListAsync(services, new AIRecordQuery { Text = "sales" }, CancellationToken.None), commit: false);
        var none = await setup.Harness.RunAsync(async services => await kind.ListAsync(services, new AIRecordQuery { Text = "billing" }, CancellationToken.None), commit: false);
        var projected = JsonSerializer.SerializeToElement(kind.Projection(Assert.Single(page.Entries)));

        Assert.Equal(1, page.Count);
        Assert.Equal(0, none.Count);
        Assert.Equal("Sales voice", projected.GetProperty("name").GetString());
        Assert.Equal(setup.Team.ItemId, projected.GetProperty("teamId").GetString());
    }

    internal static AIFunction Create(string toolName)
        => toolName switch
        {
            CreateWorkersForAgentsTool.ToolName => new CreateWorkersForAgentsTool(),
            GetContactCenterTimeStagesTool.ToolName => new GetContactCenterTimeStagesTool(),
            SaveContactCenterTimeStagesTool.ToolName => new SaveContactCenterTimeStagesTool(),
            GetAdherenceDayTool.ToolName => new GetAdherenceDayTool(),
            ListAdherenceExceptionsTool.ToolName => new ListAdherenceExceptionsTool(),
            AddAdherenceExceptionTool.ToolName => new AddAdherenceExceptionTool(),
            DeleteAdherenceExceptionTool.ToolName => new DeleteAdherenceExceptionTool(),
            GetIntradayStatusTool.ToolName => new GetIntradayStatusTool(),
            ReforecastIntradayTool.ToolName => new ReforecastIntradayTool(),
            PostIntradayOfferTool.ToolName => new PostIntradayOfferTool(),
            CloseIntradayOfferTool.ToolName => new CloseIntradayOfferTool(),
            ListForecastsTool.ToolName => new ListForecastsTool(),
            GetForecastTool.ToolName => new GetForecastTool(),
            GetCapacityPlanProjectionTool.ToolName => new GetCapacityPlanProjectionTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType)
        => WorkforceTimeOffAdminAIToolTests.GetTools(startupType);

    private static IReadOnlyList<AIRecordKind> GetKinds(Type startupType)
    {
        var services = new ServiceCollection();
        var localizer = Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(startupType));
        var startup = (StartupBase)Activator.CreateInstance(startupType, localizer);

        startup.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return [.. provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds.Values];
    }

    private static Task<string> InvokeAsync(Setup setup, string toolName, ClaimsPrincipal user, Dictionary<string, object> arguments)
        => WorkforceTimeOffAdminAIToolTests.InvokeAsync(setup.Harness, Create(toolName), user, arguments);

    private static ClaimsPrincipal Planner()
        => CalendarTestHarness.User("planner", WorkforcePermissions.ManageForecasts.Name);

    private static async Task<ClaimsPrincipal> AddManagerAsync(Setup setup)
    {
        await WorkforceTests.AddWorkerAsync(setup.Harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = setup.Team.ItemId, Role = TeamMemberRole.Manager });
        });

        return CalendarTestHarness.User("maya", WorkforcePermissions.ViewAdherence.Name, WorkforcePermissions.ManageAdherence.Name);
    }

    private static async Task<Setup> SetupAsync()
    {
        var setup = new Setup();
        var agents = new Mock<IAgentProfileManager>();

        agents.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<AgentProfile>());
        agents.Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((AgentProfile)null);

        var stages = new List<TimeStage>
        {
            new() { ItemId = "stage-call", Name = "On a call", Position = 10 },
            new() { ItemId = "stage-break", Name = "Break", Position = 20 },
        };
        var stageManager = new Mock<ITimeStageManager>();

        stageManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stages);

        var reasonManager = new Mock<IAgentStateReasonCodeManager>();

        reasonManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new AgentStateReasonCode { ItemId = "lunch", Name = "Lunch", AppliesTo = AgentPresenceStatus.Break },
        });

        var reader = new Mock<IIntervalStatisticsReader>();

        reader.Setup(r => r.GetQueueIntervalsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<QueueIntervalStatistic>());
        reader.Setup(r => r.GetAgentIntervalsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentIntervalStatistic>());

        var siteService = new Mock<ISiteService>();

        // One settings document serves both reads and writes here, so it must not cache what it read, as a loaded
        // (writable) document does not.
        setup.Site.TimeZoneId = "America/New_York";
        setup.Site.IsReadOnly = false;
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(() => setup.Site);
        siteService.Setup(service => service.LoadSiteSettingsAsync()).ReturnsAsync(() => setup.Site);
        siteService.Setup(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>())).Returns(Task.CompletedTask);

        setup.Harness = await WorkforceTests.CreateAsync(
            services =>
            {
                services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                services.AddSingleton(WorkforceTimeOffAdminAIToolTests.LocalClock(() => CalendarTestHarness.Now));
                services.AddSingleton(agents.Object);
                services.AddSingleton(new Mock<IAgentStateHistory>().Object);
                services.AddSingleton(reader.Object);
                services.AddSingleton(stageManager.Object);
                services.AddSingleton(reasonManager.Object);
                services.AddSingleton(siteService.Object);
                services.AddScoped<AgentWorkerService>();
                services.AddScoped<AdherenceRuleSetStore>();
                services.AddScoped<AdherenceRuleSetManager>();
                services.AddScoped<ICatalogEntryHandler<AdherenceRuleSet>, AdherenceRuleSetHandler>();
                services.AddScoped<AdherenceService>();
                services.AddScoped<PlanningGroupStore>();
                services.AddScoped<PlanningGroupManager>();
                services.AddScoped<ICatalogEntryHandler<PlanningGroup>, PlanningGroupHandler>();
                services.AddScoped<ForecastEventStore>();
                services.AddScoped<ForecastEventManager>();
                services.AddScoped<ForecastService>();
                services.AddScoped<CapacityPlanStore>();
                services.AddScoped<CapacityPlanManager>();
                services.AddScoped<CapacityPlanService>();
                services.AddScoped<IntradayService>();
            },
            async schemaBuilder =>
            {
                await AdherenceIndexSchema.CreateAsync(schemaBuilder);
                await ForecastingIndexSchema.CreateAsync(schemaBuilder);
                await CapacityPlanIndexSchema.CreateAsync(schemaBuilder);
                await IntradayIndexSchema.CreateAsync(schemaBuilder);
            },
            [
                new AdherenceRuleSetIndexProvider(),
                new AdherenceExceptionIndexProvider(),
                new AdherenceDayIndexProvider(),
                new AdherenceAlertIndexProvider(),
                new PlanningGroupIndexProvider(),
                new ForecastEventIndexProvider(),
                new ForecastIndexProvider(),
                new CapacityPlanIndexProvider(),
                new IntradayOfferIndexProvider(),
            ]);

        setup.Team = await WorkforceTests.AddTeamAsync(setup.Harness, "Sales", TeamKind.Team);
        setup.Other = await WorkforceTests.AddTeamAsync(setup.Harness, "Billing", TeamKind.Team);
        setup.Ana = await PlanningKit.AddMemberAsync(setup.Harness, "Ana", setup.Team);
        setup.Ben = await PlanningKit.AddMemberAsync(setup.Harness, "Ben", setup.Other);
        setup.Group = await setup.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<PlanningGroupManager>();
            var group = await manager.NewAsync();

            group.Name = "Sales voice";
            group.QueueIds = ["sales"];
            group.TeamId = setup.Team.ItemId;
            await manager.CreateAsync(group);

            return group;
        });

        return setup;
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; set; }

        public SiteSettings Site { get; } = new();

        public Team Team { get; set; }

        public Team Other { get; set; }

        public StaffMember Ana { get; set; }

        public StaffMember Ben { get; set; }

        public PlanningGroup Group { get; set; }

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private sealed class PassThroughLocalizer<T> : Microsoft.Extensions.Localization.IStringLocalizer<T>
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name]
            => new(name, name);

        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
