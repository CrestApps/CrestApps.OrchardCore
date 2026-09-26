using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// The live dashboard's More actions: a compact kebab button, and a menu that opens fully on screen and above the admin
/// theme's sidebar. Live, the menu opened to the left of its button and slid under the sidebar, so its items read
/// "vailable", "ot ready" and "reak".
/// </summary>
public sealed class SupervisorDashboardMenuTests : SoftPhoneBrowserTest
{
    public static TheoryData<int, int, string> Layouts => new()
    {
        { 1400, 900, "expanded" },
        { 1400, 900, "collapsed" },
        { 430, 740, "expanded" },
        { 430, 740, "collapsed" },
    };

    [Fact]
    public async Task TheMoreButton_IsAKebabIcon_LabelledForTheAgent()
    {
        // Arrange
        var page = await OpenAsync(1400, 900, "expanded", Agents(1));

        // Act
        var button = page.Locator("[data-cc-more='agent-1']");

        // Assert
        Assert.Equal("More actions for Agent 1", await button.GetAttributeAsync("aria-label"));
        Assert.Equal("More actions for Agent 1", await button.GetAttributeAsync("title"));
        Assert.Equal(1, await button.Locator("i.fa-ellipsis-vertical").CountAsync());
        Assert.Equal(string.Empty, (await button.InnerTextAsync()).Trim());
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public async Task TheMoreButton_SitsAtTheFarRightOfTheAgentsNameRow_WithTheCallActionsBelow(int width, int height, string sidebar)
    {
        // Arrange
        var page = await OpenAsync(width, height, sidebar, Agents(1));

        // Act
        var placed = await page.EvaluateAsync<System.Text.Json.JsonElement>(
            """
            () => {
                const button = document.querySelector("[data-cc-more='agent-1']");
                const row = button.closest('.cc-agent');
                const name = row.querySelector('.cc-agent__name').getBoundingClientRect();
                const kebab = button.getBoundingClientRect();
                const actions = row.querySelector('.cc-agent__actions').getBoundingClientRect();
                const style = getComputedStyle(row);
                const inner = row.getBoundingClientRect().right - parseFloat(style.paddingRight) - parseFloat(style.borderRightWidth);

                const onTop = document.elementFromPoint((kebab.left + kebab.right) / 2, (kebab.top + kebab.bottom) / 2);

                return {
                    visible: !!onTop && onTop.closest('[data-cc-more]') === button,
                    gapToRight: inner - kebab.right,
                    sameRowAsName: kebab.top < name.bottom && kebab.bottom > name.top,
                    actionsBelow: actions.top >= kebab.bottom - 1,
                    rightOfName: kebab.left >= name.right,
                };
            }
            """);

        await CaptureAsync(page, $"dashboard-kebab-{width}-{sidebar}");

        // Assert
        Assert.True(placed.GetProperty("visible").GetBoolean(), $"The kebab is cut off or covered: {placed}");
        Assert.True(Math.Abs(placed.GetProperty("gapToRight").GetDouble()) < 1, $"The kebab is not at (or is past) the row's right edge: {placed}");
        Assert.True(placed.GetProperty("sameRowAsName").GetBoolean(), $"The kebab is not on the name's row: {placed}");
        Assert.True(placed.GetProperty("actionsBelow").GetBoolean(), $"The call actions are not under the name row: {placed}");
        Assert.True(placed.GetProperty("rightOfName").GetBoolean(), $"The kebab is not right of the name: {placed}");
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public async Task TheMenu_OpensFullyOnScreen_AndAboveTheSidebar(int width, int height, string sidebar)
    {
        // Arrange - the first agent's card sits right next to the sidebar.
        var page = await OpenAsync(width, height, sidebar, Agents(1));

        // Act
        await page.ClickAsync("[data-cc-more='agent-1']");
        await page.Locator("[data-cc-menu='agent-1']").WaitForAsync();

        // Assert - every item is inside the viewport, and it is the item, not the sidebar or a card, that is on top.
        var hidden = await HiddenItemsAsync(page, "agent-1");
        Assert.Empty(hidden);
        await CaptureAsync(page, $"dashboard-menu-{width}-{sidebar}");
    }

    [Theory]
    [InlineData(1400, 900)]
    [InlineData(430, 740)]
    public async Task TheMenu_OfAnAgentAtTheBottomOfTheScreen_FlipsAboveItsButton(int width, int height)
    {
        // Arrange - enough agents that the last one's button is near the bottom of the window.
        var page = await OpenAsync(width, height, "collapsed", Agents(24));
        var last = page.Locator("[data-cc-more='agent-24']");
        await last.ScrollIntoViewIfNeededAsync();

        // Act
        await last.ClickAsync();
        await page.Locator("[data-cc-menu='agent-24']").WaitForAsync();

        // Assert
        Assert.Equal("above", await page.Locator("[data-cc-menu='agent-24']").GetAttributeAsync("data-cc-menu-placement"));
        Assert.Empty(await HiddenItemsAsync(page, "agent-24"));
    }

    [Fact]
    public async Task TheMenu_ClosesOnScroll_OnEscape_AndOnAClickOutside()
    {
        // Arrange
        var page = await OpenAsync(1400, 900, "expanded", Agents(24));
        var menu = page.Locator("[data-cc-menu='agent-1']");

        // Act / Assert - Escape, returning focus to the button.
        await page.ClickAsync("[data-cc-more='agent-1']");
        await menu.WaitForAsync();
        await page.Keyboard.PressAsync("Escape");
        await menu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
        Assert.Equal("agent-1", await page.EvaluateAsync<string>("() => document.activeElement.getAttribute('data-cc-more')"));

        // A click outside.
        await page.ClickAsync("[data-cc-more='agent-1']");
        await menu.WaitForAsync();
        await page.Mouse.ClickAsync(1300, 20);
        await menu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

        // A scroll of the page.
        await page.ClickAsync("[data-cc-more='agent-1']");
        await menu.WaitForAsync();
        await page.Mouse.WheelAsync(0, 400);
        await menu.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }

    // The items of an open menu that are not fully on screen or not on top where they are drawn.
    private static Task<string[]> HiddenItemsAsync(IPage page, string agentId)
        => page.EvaluateAsync<string[]>(
            """
            agentId => {
                const items = Array.from(document.querySelectorAll(`[data-cc-menu="${agentId}"] [role="menuitem"]`));
                const width = document.documentElement.clientWidth;
                const height = window.innerHeight;

                return items.filter(item => {
                    const box = item.getBoundingClientRect();

                    if (box.left < 0 || box.top < 0 || box.right > width || box.bottom > height || box.width === 0) {
                        return true;
                    }

                    for (const x of [box.left + 2, box.left + box.width / 2, box.right - 2]) {
                        const hit = document.elementFromPoint(x, box.top + box.height / 2);

                        if (!hit || !item.contains(hit)) {
                            return true;
                        }
                    }

                    return false;
                }).map(item => item.textContent.trim());
            }
            """,
            agentId);

    private async Task<IPage> OpenAsync(int width, int height, string sidebar, JsonArray agents)
    {
        Server.Supervisor.State = new JsonObject
        {
            ["queues"] = new JsonArray(),
            ["agents"] = agents,
            ["totalWaiting"] = 0,
            ["availableAgents"] = 0,
            ["canIntervene"] = true,
            ["canMessage"] = true,
        };

        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = width, Height = height } });
        await page.GotoAsync($"{Server.BaseUrl}{SupervisorHarness.DashboardUrl}?sidebar={sidebar}");
        await page.Locator(".cc-agent").First.WaitForAsync();

        return page;
    }

    private static JsonArray Agents(int count)
    {
        var agents = new JsonArray();

        for (var index = 1; index <= count; index++)
        {
            agents.Add(new JsonObject
            {
                ["agentId"] = $"agent-{index}",
                ["userId"] = $"user-{index}",
                ["displayName"] = $"Agent {index}",
                ["presenceStatus"] = "Busy",
                ["activeInteractions"] = 1,
                ["activeInteractionId"] = $"int-{index}",
                ["availableMonitoringModes"] = new JsonArray("Monitor", "Whisper", "Barge"),
                ["availableInterventions"] = new JsonArray("TakeOver", "EndCall", "Transfer", "Record"),
                ["recordingState"] = "Recording",
            });
        }

        return agents;
    }
}
