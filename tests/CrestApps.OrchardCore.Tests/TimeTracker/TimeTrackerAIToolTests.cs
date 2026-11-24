using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.AI;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.TimeTracker;

/// <summary>
/// The Time Tracker AI tools: they refuse a caller who is not signed in or lacks the page's permission, keep a person's
/// clock to their own time, punch on the Workforce time clock when it keeps the time, and act through the Time Tracker
/// service as the pages do. Stages and categories are read-only record kinds.
/// </summary>
public sealed class TimeTrackerAIToolTests
{
    // Sunday 1 November 2026, 9:00 in New York (the harness's site time zone); the harness's now is 2 November, 7:00 there.
    private static readonly DateTime _sundayNine = new(2026, 11, 1, 14, 0, 0, DateTimeKind.Utc);

    internal static readonly (string Name, bool RequiresApproval, bool SelfService)[] NewTools =
    [
        (GetMyTimeStatusTool.ToolName, false, true),
        (ClockMyTimeTool.ToolName, false, true),
        (ListTimeEntriesTool.ToolName, false, false),
        (GetTimeTotalsTool.ToolName, false, false),
        (SaveTimeEntryTool.ToolName, false, false),
        (DeleteTimeEntryTool.ToolName, true, false),
    ];

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
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["entryId"] = "x" });
        var selfService = NewTools.Single(tool => tool.Name == toolName).SelfService;

        Assert.Equal(selfService ? TimeTrackerToolBase.SelfSignInRequired : TimeTrackerToolBase.SignInRequired, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName)
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, CalendarTestHarness.User("ana", "SomethingElse"), new() { ["entryId"] = "x" });

        Assert.StartsWith("The user isn't allowed to do this", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClockMyTime_ClocksIn_GetMyTimeStatusShowsIt_ThenClocksOut()
    {
        await using var harness = await CreateAsync();
        var payable = await AddCategoryAsync(harness, "Payable");
        await AddStageAsync(harness, "Work", selfService: true, payable);
        await AddStageAsync(harness, "Overtime", selfService: false, payable);
        var ana = Ana();

        var clockedIn = await InvokeAsync(harness, ClockMyTimeTool.ToolName, ana, new() { ["stage"] = "work" });
        var status = JsonElement.Parse(await InvokeAsync(harness, GetMyTimeStatusTool.ToolName, ana, new()));
        var refused = await InvokeAsync(harness, ClockMyTimeTool.ToolName, ana, new() { ["stage"] = "Overtime" });
        var clockedOut = await InvokeAsync(harness, ClockMyTimeTool.ToolName, ana, new() { ["clockOut"] = true });

        Assert.True(JsonElement.Parse(clockedIn).GetProperty("clockedIn").GetBoolean());
        Assert.Equal("2026-11-02T07:00", JsonElement.Parse(clockedIn).GetProperty("since").GetString());
        Assert.True(status.GetProperty("clockedIn").GetBoolean());
        Assert.Equal("Work", status.GetProperty("stage").GetString());
        Assert.Equal(["Work"], status.GetProperty("stagesYouCanSwitchTo").EnumerateArray().Select(stage => stage.GetProperty("name").GetString()));
        Assert.Equal("America/New_York", status.GetProperty("timeZone").GetString());
        Assert.Equal("Choose one of the user's stages: Work.", refused);
        Assert.True(JsonElement.Parse(clockedOut).GetProperty("clockedOut").GetBoolean());
    }

    [Fact]
    public async Task ClockMyTime_PunchesOnTheClockThatKeepsTheTime()
    {
        var punches = new Mock<IExternalTimeClockPunches>();

        punches.SetupGet(clock => clock.ClockId).Returns(ClockId);
        punches.Setup(clock => clock.PunchAsync(It.IsAny<ClaimsPrincipal>(), "In", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalTimeClockPunchResult { Punch = "In", Utc = CalendarTestHarness.Now, TimeZoneId = "America/Chicago", WithinGeofence = true });
        punches.Setup(clock => clock.GetStateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalTimeClockPunchState { State = "Out", NextPunches = ["In"] });

        await using var harness = await CreateAsync(keepsAna: true, punches: punches.Object);
        await AddStageAsync(harness, "Work", selfService: true);

        var asked = await InvokeAsync(harness, ClockMyTimeTool.ToolName, Ana(), new() { ["stage"] = "Work" });
        var punched = JsonElement.Parse(await InvokeAsync(harness, ClockMyTimeTool.ToolName, Ana(), new() { ["punch"] = "In" }));

        Assert.Equal("The user's time is kept by the Time Clock, so give the punch to make: In.", asked);
        Assert.Equal("In", punched.GetProperty("punched").GetString());
        Assert.Equal("2026-11-02T06:00", punched.GetProperty("at").GetString());
        Assert.Equal("America/Chicago", punched.GetProperty("timeZone").GetString());
        punches.Verify(clock => clock.PunchAsync(It.Is<ClaimsPrincipal>(user => user.FindFirstValue(ClaimTypes.NameIdentifier) == "ana"), "In", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClockMyTime_LetsAWorkerOnThePunchClockPunchWithoutTheTimeTrackerPermission()
    {
        var punches = new Mock<IExternalTimeClockPunches>();

        punches.SetupGet(clock => clock.ClockId).Returns(ClockId);
        punches.Setup(clock => clock.PunchAsync(It.IsAny<ClaimsPrincipal>(), "Out", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalTimeClockPunchResult { Punch = "Out", Utc = CalendarTestHarness.Now, TimeZoneId = "America/Chicago" });

        await using var harness = await CreateAsync(keepsAna: true, punches: punches.Object);

        // Only the time clock's own permission, as the Authenticated role has it by default; the clock checks it.
        var worker = CalendarTestHarness.User("ana", "PunchTime");
        var punched = JsonElement.Parse(await InvokeAsync(harness, ClockMyTimeTool.ToolName, worker, new() { ["clockOut"] = true }));
        var stranger = await InvokeAsync(harness, ClockMyTimeTool.ToolName, CalendarTestHarness.User("ben", "PunchTime"), new() { ["clockOut"] = true });

        Assert.Equal("Out", punched.GetProperty("punched").GetString());
        Assert.StartsWith("The user isn't allowed to do this", stranger, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClockMyTime_NamesAClockThatTakesNoPunchesFromHere()
    {
        await using var harness = await CreateAsync(keepsAna: true);

        var result = await InvokeAsync(harness, ClockMyTimeTool.ToolName, Ana(), new() { ["clockOut"] = true });

        Assert.Equal("Nothing changed: the user's time is kept by the Time Clock; they clock in and out at /my-work.", result);
    }

    [Fact]
    public async Task ListTimeEntries_FiltersByNameAndDays_AndRefusesAnUnknownStage()
    {
        await using var harness = await CreateAsync();
        var work = await AddStageAsync(harness, "Work", selfService: true);

        await AddEntryAsync(harness, "ana", "Ana", work, _sundayNine, 120);
        await AddEntryAsync(harness, "ben", "Ben", work, _sundayNine, 60);

        var result = JsonElement.Parse(await InvokeAsync(harness, ListTimeEntriesTool.ToolName, Manager(), new() { ["userName"] = "An", ["from"] = "2026-11-01", ["to"] = "2026-11-01" }));
        var unknown = await InvokeAsync(harness, ListTimeEntriesTool.ToolName, Manager(), new() { ["stage"] = "Lunch" });
        var item = Assert.Single(result.GetProperty("items").EnumerateArray());

        Assert.Equal("Ana", item.GetProperty("user").GetString());
        Assert.Equal("Work", item.GetProperty("stage").GetString());
        Assert.Equal("2026-11-01T09:00", item.GetProperty("start").GetString());
        Assert.Equal(120, item.GetProperty("minutes").GetInt32());
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.StartsWith("No stage is called 'Lunch'.", unknown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTimeTotals_TotalsByCategory_AndRefusesABackwardRange()
    {
        await using var harness = await CreateAsync();
        var payable = await AddCategoryAsync(harness, "Payable");
        var work = await AddStageAsync(harness, "Work", selfService: true, payable);

        await AddEntryAsync(harness, "ana", "Ana", work, _sundayNine, 90);

        var result = JsonElement.Parse(await InvokeAsync(harness, GetTimeTotalsTool.ToolName, Manager(), new() { ["from"] = "2026-11-01", ["to"] = "2026-11-01", ["userId"] = "ana" }));
        var backward = await InvokeAsync(harness, GetTimeTotalsTool.ToolName, Manager(), new() { ["from"] = "2026-11-02", ["to"] = "2026-11-01" });
        var row = Assert.Single(result.GetProperty("items").EnumerateArray());

        Assert.Equal(1.5m, row.GetProperty("totalHours").GetDecimal());
        Assert.Equal("Payable", row.GetProperty("hoursByCategory")[0].GetProperty("category").GetString());
        Assert.Equal(1.5m, row.GetProperty("hoursByCategory")[0].GetProperty("hours").GetDecimal());
        Assert.Equal("The first day (from) must not be after the last day (to).", backward);
    }

    [Fact]
    public async Task SaveTimeEntry_ChangesTheEndWithAReason_AndRefusesWithoutOne()
    {
        await using var harness = await CreateAsync();
        var work = await AddStageAsync(harness, "Work", selfService: true);
        var entry = await AddEntryAsync(harness, "ana", "Ana", work, _sundayNine, 60);

        var noReason = await InvokeAsync(harness, SaveTimeEntryTool.ToolName, Manager(), new() { ["entryId"] = entry.ItemId, ["end"] = "2026-11-01T11:00" });
        var saved = JsonElement.Parse(await InvokeAsync(harness, SaveTimeEntryTool.ToolName, Manager(), new() { ["entryId"] = entry.ItemId, ["end"] = "2026-11-01T11:00", ["reason"] = "Forgot to clock out." }));
        var stored = await harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().FindAsync(entry.ItemId), commit: false);

        Assert.Equal("Nothing was saved: say why the time is added or changed (reason).", noReason);
        Assert.False(saved.GetProperty("created").GetBoolean());
        Assert.Equal("2026-11-01T11:00", saved.GetProperty("entry").GetProperty("end").GetString());
        Assert.Equal(_sundayNine.AddHours(2), stored.EndUtc);
        Assert.Equal("Forgot to clock out.", Assert.Single(stored.Changes).Reason);
    }

    [Fact]
    public async Task SaveTimeEntry_RefusesANewEntryForAnUnknownPerson()
    {
        await using var harness = await CreateAsync();
        await AddStageAsync(harness, "Work", selfService: true);

        var result = await InvokeAsync(harness, SaveTimeEntryTool.ToolName, Manager(), new() { ["userId"] = "nobody", ["stage"] = "Work", ["start"] = "2026-11-01T09:00", ["end"] = "2026-11-01T10:00", ["reason"] = "Missed" });

        Assert.Equal("Nothing was saved: choose whose time it is (an existing userId or userName).", result);
    }

    [Fact]
    public async Task DeleteTimeEntry_DeletesWithAReason_AndReportsAnUnknownEntry()
    {
        await using var harness = await CreateAsync();
        var work = await AddStageAsync(harness, "Work", selfService: true);
        var entry = await AddEntryAsync(harness, "ana", "Ana", work, _sundayNine, 60);

        var deleted = JsonElement.Parse(await InvokeAsync(harness, DeleteTimeEntryTool.ToolName, Manager(), new() { ["entryId"] = entry.ItemId, ["reason"] = "Entered twice." }));
        var unknown = await InvokeAsync(harness, DeleteTimeEntryTool.ToolName, Manager(), new() { ["entryId"] = "missing", ["reason"] = "Entered twice." });
        var stored = await harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().FindAsync(entry.ItemId), commit: false);

        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.Null(stored);
        Assert.Equal("Nothing was deleted: The entry doesn't exist.", unknown);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName)
    {
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(AIToolsStartup));
        var requiresApproval = NewTools.Single(tool => tool.Name == toolName).RequiresApproval;

        Assert.True(tools.TryGetValue(toolName, out var tool), $"The tool '{toolName}' is not registered.");
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Time Tracker", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
        Assert.Equal(toolName, Create(toolName).Name);
        Assert.False(string.IsNullOrWhiteSpace(Create(toolName).Description));
    }

    [Fact]
    public void Startup_RegistersSixToolsWithOneApproval_AndTwoReadOnlyKinds_OnTheTimeTrackerFeature()
    {
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(AIToolsStartup));
        var kinds = GetKinds();
        var required = typeof(AIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(AIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        Assert.Equal(6, tools.Count);
        Assert.Equal(1, tools.Values.Count(tool => tool.RequiresApproval));
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(TimeTrackerConstants.Features.TimeTracker, feature.FeatureName);
        Assert.Equal(["timeCategory", "timeStage"], kinds.Keys.Order(StringComparer.Ordinal));
        Assert.All(kinds.Values, kind => Assert.False(kind.CanWrite));
        Assert.All(kinds.Values, kind => Assert.Equal(TimeTrackerPermissions.ManageTimeTrackerSettings, kind.Permission));
    }

    [Fact]
    public void TimeStageKind_ProjectsNamedFieldsOnly()
    {
        var kind = (AIRecordKind<TimeStage>)GetKinds()["timeStage"];
        var stage = new TimeStage { ItemId = "s1", Name = "Work", Code = "W", CategoryIds = ["c1"] };

        stage.Properties["Secret"] = "canary";

        var json = JsonSerializer.Serialize(kind.Projection(stage));

        Assert.Contains("\"name\":\"Work\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("canary", json, StringComparison.Ordinal);
    }

    private const string ClockId = "Workforce.TimeClock";

    private static IReadOnlyDictionary<string, AIRecordKind> GetKinds()
    {
        var services = new ServiceCollection();

        new AIToolsStartup(new PassThroughStringLocalizer<AIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            GetMyTimeStatusTool.ToolName => new GetMyTimeStatusTool(),
            ClockMyTimeTool.ToolName => new ClockMyTimeTool(),
            ListTimeEntriesTool.ToolName => new ListTimeEntriesTool(),
            GetTimeTotalsTool.ToolName => new GetTimeTotalsTool(),
            SaveTimeEntryTool.ToolName => new SaveTimeEntryTool(),
            DeleteTimeEntryTool.ToolName => new DeleteTimeEntryTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static Task<string> InvokeAsync(CalendarTestHarness harness, string toolName, ClaimsPrincipal user, Dictionary<string, object> arguments)
        => WorkforceTimeOffAdminAIToolTests.InvokeAsync(harness, Create(toolName), user, arguments);

    private static ClaimsPrincipal Ana()
        => CalendarTestHarness.User("ana", TimeTrackerPermissions.TrackOwnTime.Name);

    private static ClaimsPrincipal Manager()
        => CalendarTestHarness.User("maya", TimeTrackerPermissions.ManageTimeEntries.Name);

    private static Task<CalendarTestHarness> CreateAsync(bool keepsAna = false, IExternalTimeClockPunches punches = null)
    {
        var externalClock = new Mock<IExternalTimeClock>();

        externalClock.Setup(clock => clock.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => keepsAna && userId == "ana" ? new ExternalTimeClockInfo { Id = ClockId, Name = "the Time Clock", Url = "/my-work" } : null);
        externalClock.Setup(clock => clock.FindByIdAsync(ClockId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalTimeClockInfo { Id = ClockId, Name = "the Time Clock", Url = "/my-work" });

        return CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddTimeTrackerCore();
                services.AddScoped<TimeTrackerTimeZone>();
                services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                services.AddSingleton(externalClock.Object);

                if (punches is not null)
                {
                    services.AddSingleton(punches);
                }
            },
            TimeTrackerIndexSchema.CreateAsync,
            [new TimeTrackerIndexProvider(), new TimeCategoryIndexProvider(), new TimeStageIndexProvider()],
            [TimeTrackerConstants.CollectionName]);
    }

    private static Task<TimeCategory> AddCategoryAsync(CalendarTestHarness harness, string name)
        => harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<TimeCategoryManager>();
            var category = await manager.NewAsync();

            category.Name = name;
            await manager.CreateAsync(category);

            return category;
        });

    private static Task<TimeStage> AddStageAsync(CalendarTestHarness harness, string name, bool selfService, params TimeCategory[] categories)
        => harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<TimeStageManager>();
            var stage = await manager.NewAsync();

            stage.Name = name;
            stage.SelfService = selfService;
            stage.CategoryIds = [.. categories.Select(category => category.ItemId)];
            await manager.CreateAsync(stage);

            return stage;
        });

    private static async Task<TimeEntry> AddEntryAsync(CalendarTestHarness harness, string userId, string userName, TimeStage stage, DateTime startUtc, int minutes)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().AddAsync(
            new TimeEntry { UserId = userId, UserName = userName, StageId = stage.ItemId, StartUtc = startUtc, EndUtc = startUtc.AddMinutes(minutes), Source = TimeTrackerConstants.Sources.Manager },
            "maya",
            "Set up for the test."));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Entry;
    }
}
