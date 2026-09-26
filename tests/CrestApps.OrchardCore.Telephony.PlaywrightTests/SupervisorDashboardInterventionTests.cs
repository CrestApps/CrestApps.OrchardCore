using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// The live dashboard's supervisor interventions, on the Contact Center's real dashboard script: per-agent Listen,
/// Whisper, Barge, Stop and Take over, and the More menu -- End call, Transfer, Record, the agent's state and a message --
/// with what each posts, what state it shows, and why a call cannot be monitored when it cannot.
/// </summary>
public sealed class SupervisorDashboardInterventionTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task AnAgentOnACall_OffersListenWhisperBargeAndTakeOver_AndListenRingsTheSupervisor()
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent());

        // Act
        await page.ClickAsync("[data-cc-engage='int-1'][data-cc-mode='Monitor']");

        // Assert
        var post = await WaitForPostAsync("engage");
        Assert.Equal("int-1", post["interactionId"]);
        Assert.Equal("Monitor", post["mode"]);
        Assert.True(await page.Locator("[data-cc-engage='int-1'][data-cc-mode='Whisper']").IsVisibleAsync());
        Assert.True(await page.Locator("[data-cc-engage='int-1'][data-cc-mode='Barge']").IsVisibleAsync());
        Assert.True(await page.Locator("[data-cc-takeover='int-1']").IsEnabledAsync());
        await CaptureAsync(page, "dashboard-agent-on-call");
    }

    [Fact]
    public async Task WhileEngaged_TheActiveModeIsPressed_AnotherModeSwitchesOnTheSameLeg_AndStopStops()
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent(monitorMode: "Monitor", connected: true));

        // Assert - the engagement is visible.
        Assert.Equal("true", await page.Locator("[data-cc-switch='int-1'][data-cc-mode='Monitor']").GetAttributeAsync("aria-pressed"));
        Assert.Equal("false", await page.Locator("[data-cc-switch='int-1'][data-cc-mode='Whisper']").GetAttributeAsync("aria-pressed"));
        Assert.Contains("Listen", await page.Locator("[data-cc-monitor-state]").InnerTextAsync());
        await CaptureAsync(page, "dashboard-listening");

        // Act
        await page.ClickAsync("[data-cc-switch='int-1'][data-cc-mode='Whisper']");
        var switched = await WaitForPostAsync("switch");
        await page.ClickAsync("[data-cc-stop='int-1']");
        var stopped = await WaitForPostAsync("stop");

        // Assert
        Assert.Equal("Whisper", switched["mode"]);
        Assert.Equal("int-1", stopped["interactionId"]);
        Assert.DoesNotContain(Server.Supervisor.Posts, post => post.Action == "engage");
    }

    [Fact]
    public async Task TakeOver_BeforeTheSupervisorIsOnTheCall_BargesFirst_ThenTakesTheCallOnceConnected()
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent());

        // Act - take over from not being on the call.
        await page.ClickAsync("[data-cc-takeover='int-1']");
        var engaged = await WaitForPostAsync("engage");

        // The supervisor's phone answers: the next state the dashboard reads shows them connected, barging.
        Server.Supervisor.State = State(Agent(monitorMode: "Barge", connected: true));
        var takenOver = await WaitForPostAsync("takeover", TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal("Barge", engaged["mode"]);
        Assert.Equal("int-1", takenOver["interactionId"]);
    }

    [Fact]
    public async Task EndCall_AsksFirst_ThenEndsTheCall()
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent());

        // Act
        await OpenMenuAsync(page);
        await page.ClickAsync("[data-cc-action='end-call']");
        await page.Locator("[data-cc-panel-confirm]").WaitForAsync();
        Assert.Empty(Server.Supervisor.Posts);
        await CaptureAsync(page, "dashboard-end-call-confirm");
        await page.ClickAsync("[data-cc-panel-confirm]");

        // Assert
        Assert.Equal("int-1", (await WaitForPostAsync("end-call"))["interactionId"]);
    }

    [Fact]
    public async Task Transfer_ToAQueue_PostsTheQueue()
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent());

        // Act
        await OpenMenuAsync(page);
        await page.ClickAsync("[data-cc-action='transfer']");
        await page.SelectOptionAsync("[data-cc-transfer-target]", "Queue:queue-2");
        await page.ClickAsync("[data-cc-panel-confirm]");

        // Assert
        var post = await WaitForPostAsync("transfer");
        Assert.Equal("int-1", post["interactionId"]);
        Assert.Equal("Queue", post["targetType"]);
        Assert.Equal("queue-2", post["targetId"]);
    }

    [Fact]
    public async Task Transfer_ToANumber_PostsTheNumber()
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent());

        // Act
        await OpenMenuAsync(page);
        await page.ClickAsync("[data-cc-action='transfer']");
        await page.SelectOptionAsync("[data-cc-transfer-target]", "External:");
        await page.FillAsync("[data-cc-transfer-number]", "+15557654321");
        await page.ClickAsync("[data-cc-panel-confirm]");

        // Assert
        var post = await WaitForPostAsync("transfer");
        Assert.Equal("External", post["targetType"]);
        Assert.Equal("+15557654321", post["targetId"]);
    }

    [Fact]
    public async Task Message_SendsTheTextToTheAgent()
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent());

        // Act
        await OpenMenuAsync(page);
        await page.ClickAsync("[data-cc-action='message']");
        await page.FillAsync("[data-cc-message-text]", "Offer the retention discount.");
        await page.ClickAsync("[data-cc-panel-confirm]");

        // Assert
        var post = await WaitForPostAsync("message");
        Assert.Equal("agent-1", post["agentId"]);
        Assert.Equal("Offer the retention discount.", post["text"]);
    }

    [Theory]
    [InlineData("state:Break", "Break")]
    [InlineData("state:Away", "Away")]
    [InlineData("state:Available", "Available")]
    public async Task SetState_PostsTheState(string action, string status)
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent());

        // Act
        await OpenMenuAsync(page);
        await page.ClickAsync($"[data-cc-action='{action}']");

        // Assert
        var post = await WaitForPostAsync("agent-state");
        Assert.Equal("agent-1", post["agentId"]);
        Assert.Equal(status, post["status"]);
    }

    [Fact]
    public async Task Record_TurnsRecordingOff_ForARecordingCall()
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent());

        // Act
        await OpenMenuAsync(page);
        await page.ClickAsync("[data-cc-action='record-off']");

        // Assert
        var post = await WaitForPostAsync("recording");
        Assert.Equal("false", post["record"]);
    }

    [Fact]
    public async Task TheMoreMenu_OpensAndClosesFromTheKeyboard()
    {
        // Arrange
        var page = await OpenDashboardAsync(Agent());

        // Act
        await page.FocusAsync("[data-cc-more='agent-1']");
        await page.Keyboard.PressAsync("Enter");
        await page.Locator("[data-cc-menu='agent-1']").WaitForAsync();
        var focused = await page.EvaluateAsync<string>("() => document.activeElement.getAttribute('data-cc-action')");
        await page.Keyboard.PressAsync("ArrowDown");
        var next = await page.EvaluateAsync<string>("() => document.activeElement.getAttribute('data-cc-action')");
        await page.Keyboard.PressAsync("Escape");

        // Assert
        Assert.Equal("end-call", focused);
        Assert.Equal("transfer", next);
        await page.Locator("[data-cc-menu='agent-1']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
        Assert.Equal("agent-1", await page.EvaluateAsync<string>("() => document.activeElement.getAttribute('data-cc-more')"));
    }

    [Fact]
    public async Task AnAgentOnACallThatIsNotAContactCenterInteraction_SaysItCannotBeMonitored()
    {
        // Arrange
        var agent = new JsonObject
        {
            ["agentId"] = "agent-2",
            ["userId"] = "user-2",
            ["displayName"] = "Bea Agent",
            ["presenceStatus"] = "Available",
            ["activeInteractions"] = 0,
            ["availableMonitoringModes"] = new JsonArray(),
            ["availableInterventions"] = new JsonArray(),
            ["monitoringUnavailableReason"] = "On a call that is not a Contact Center interaction.",
        };

        // Act
        var page = await OpenDashboardAsync(agent);

        // Assert
        var note = page.Locator(".cc-agent__unavailable");
        await note.WaitForAsync();
        Assert.Equal("On a call that is not a Contact Center interaction.", await note.GetAttributeAsync("title"));
        Assert.Equal(0, await page.Locator("[data-cc-engage]").CountAsync());
    }

    [Fact]
    public async Task TheActions_FitTheDesktopAppWindow()
    {
        // Arrange
        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = DesktopAppViewport });
        Server.Supervisor.State = State(Agent(monitorMode: "Whisper", connected: true));
        await page.GotoAsync(Server.BaseUrl + SupervisorHarness.DashboardUrl);
        await page.Locator("[data-cc-stop='int-1']").WaitForAsync();

        // Act
        var overflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth + 1");
        await OpenMenuAsync(page);

        // Assert
        Assert.False(overflow);
        Assert.True(await page.Locator("[data-cc-action='message']").IsVisibleAsync());
        await CaptureAsync(page, "dashboard-desktop-app-width");
    }

    private async Task<IPage> OpenDashboardAsync(JsonObject agent)
    {
        Server.Supervisor.State = State(agent);
        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1280, Height = 800 } });
        await page.GotoAsync(Server.BaseUrl + SupervisorHarness.DashboardUrl);
        await page.Locator(".cc-agent").WaitForAsync();

        return page;
    }

    private static async Task OpenMenuAsync(IPage page)
    {
        await page.ClickAsync("[data-cc-more='agent-1']");
        await page.Locator("[data-cc-menu='agent-1']").WaitForAsync();
    }

    private async Task<Dictionary<string, string>> WaitForPostAsync(string action, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));

        while (DateTime.UtcNow < deadline)
        {
            var post = Server.Supervisor.Posts.FirstOrDefault(candidate => candidate.Action == action);

            if (post.Form is not null)
            {
                return post.Form;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"The dashboard never posted '{action}'. Posted: {string.Join(", ", Server.Supervisor.Posts.Select(post => post.Action))}.");
    }

    private static JsonObject Agent(string monitorMode = null, bool connected = false)
        => new()
        {
            ["agentId"] = "agent-1",
            ["userId"] = "user-1",
            ["displayName"] = "Ann Agent",
            ["presenceStatus"] = "Busy",
            ["activeInteractions"] = 1,
            ["activeInteractionId"] = "int-1",
            ["availableMonitoringModes"] = new JsonArray("Monitor", "Whisper", "Barge"),
            ["availableInterventions"] = new JsonArray("TakeOver", "EndCall", "Transfer", "Record"),
            ["recordingState"] = "Recording",
            ["monitorMode"] = monitorMode,
            ["monitorConnected"] = connected,
        };

    private static JsonObject State(JsonObject agent)
        => new()
        {
            ["queues"] = JsonNode.Parse(JsonSerializer.Serialize(new[]
            {
                new { id = "queue-1", name = "Sales", waitingCount = 0, signedInAgentCount = 1, availableAgentCount = 0, busyAgentCount = 1, notReadyAgentCount = 0, longestWaitSeconds = 0, slaBreachCount = 0, slaThresholdSeconds = 0 },
                new { id = "queue-2", name = "Support", waitingCount = 0, signedInAgentCount = 0, availableAgentCount = 0, busyAgentCount = 0, notReadyAgentCount = 0, longestWaitSeconds = 0, slaBreachCount = 0, slaThresholdSeconds = 0 },
            })),
            ["agents"] = new JsonArray(agent),
            ["totalWaiting"] = 0,
            ["availableAgents"] = 0,
            ["canIntervene"] = true,
            ["canMessage"] = true,
        };
}
