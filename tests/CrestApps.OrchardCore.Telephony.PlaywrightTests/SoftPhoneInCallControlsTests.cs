using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// The in-call controls on the real widget markup, in the desktop app's 430 x 740 window: the agent's own controls in one
/// labelled row with a single red Hang up, the actions that bring someone else in below it, the keypad closed until it is
/// asked for, and Add call holding the call and handing the agent an empty field with a way back.
/// </summary>
/// <remarks>
/// Agents found the in-call row of identical round icons -- hold, mute, transfer, a dark red "disconnect all" and a red
/// hang-up, over a keypad that stayed open all call -- overwhelming, most of all when holding a caller to dial someone
/// else and merge or transfer.
/// </remarks>
public sealed class SoftPhoneInCallControlsTests : SoftPhoneBrowserTest
{
    private const string Standalone = "?widget&embedded";

    [Fact]
    public async Task AConnectedCall_ShowsTheAgentsOwnControls_ThenTheLabelledOptions_AndOnlyHangUpIsRed()
    {
        // Arrange
        var page = await OpenAsync(Standalone, DesktopAppViewport);

        // Act
        await DialAndConnectAsync(page, "+15551234567");

        // Assert - Mute, Hold, Keypad and Hang up, then Transfer and Add call; nothing to end for anyone else yet.
        Assert.Equal(["Mute", "Hold", "Keypad", "Hang up"], await VisibleLabelsAsync(page, "[data-telephony-call-controls]"));
        Assert.Equal(["Transfer", "Add call"], await VisibleLabelsAsync(page, "[data-telephony-call-options]"));
        Assert.True(await page.Locator("[data-telephony-hangup-all]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-dial]").IsHiddenAsync());
        Assert.Equal(1, await CountRedControlsAsync(page));
        await AssertFitsTheWindowAsync(page);
    }

    [Fact]
    public async Task TheKeypad_StaysClosedOnACall_UntilTheAgentOpensIt()
    {
        // Arrange
        var page = await OpenAsync(Standalone, DesktopAppViewport);
        await DialAndConnectAsync(page, "+15551234567");
        var toggle = page.Locator("[data-telephony-keypad-toggle]");
        Assert.True(await page.Locator("[data-telephony-keypad-panel]").IsHiddenAsync());
        Assert.Equal("false", await toggle.GetAttributeAsync("aria-pressed"));

        // Act
        await toggle.ClickAsync();

        // Assert
        Assert.True(await page.Locator("[data-telephony-keypad-panel]").IsVisibleAsync());
        Assert.Equal("true", await toggle.GetAttributeAsync("aria-pressed"));
        await AssertFitsTheWindowAsync(page);
    }

    [Fact]
    public async Task Mute_IsShownOn_WhileTheCallIsMuted()
    {
        // Arrange
        var page = await OpenAsync(Standalone, DesktopAppViewport);
        await DialAndConnectAsync(page, "+15551234567");

        // Act
        await page.ClickAsync("[data-telephony-mute]");
        var unmute = page.Locator("[data-telephony-unmute]");
        await unmute.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Assert
        Assert.Contains("is-on", await unmute.GetAttributeAsync("class"));
        Assert.Equal("Unmute", (await unmute.InnerTextAsync()).Trim());
        Assert.True(await page.Locator("[data-telephony-mute]").IsHiddenAsync());
    }

    [Fact]
    public async Task AddCall_HoldsTheCall_AndGivesAnEmptyFocusedField_WithAWayBack()
    {
        // Arrange
        var page = await OpenAsync(Standalone, DesktopAppViewport);
        await DialAndConnectAsync(page, "+15551234567");
        var held = await GetCurrentCallIdAsync(page);

        // Act
        await page.ClickAsync("[data-telephony-add-call]");

        // Assert - the call is held, and the field is empty, focused and the agent's.
        await WaitForAsync(() => Server.Provider.HoldCommands.Contains($"Hold:{held}"));
        await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.hasAttribute('data-telephony-number')");
        Assert.Equal(string.Empty, await page.Locator("[data-telephony-number]").InputValueAsync());
        Assert.True(await page.Locator("[data-telephony-dial]").IsVisibleAsync());
        Assert.True(await page.Locator("[data-telephony-add-call-cancel]").IsVisibleAsync());
        Assert.True(await page.Locator("[data-telephony-keypad-panel]").IsVisibleAsync());
        Assert.True(await page.Locator("[data-telephony-call-controls]").IsHiddenAsync());
        Assert.Contains("On hold", await page.Locator("[data-telephony-active-calls]").InnerTextAsync());
        await AssertFitsTheWindowAsync(page);

        // A key reached for does not bring the held call's number back into the field.
        await page.ClickAsync("[data-telephony-key=\"5\"]");
        Assert.Equal("5", await page.Locator("[data-telephony-number]").InputValueAsync());
        Assert.Null(Server.Provider.GetLastDigits());

        // Act - Escape goes back to the held call.
        await page.Locator("[data-telephony-number]").PressAsync("Escape");

        // Assert
        await WaitForAsync(() => Server.Provider.HoldCommands.Contains($"Resume:{held}"));
        await page.Locator("[data-telephony-call-controls]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-add-call-cancel]").IsHiddenAsync());
    }

    [Fact]
    public async Task AddCall_ThenEnter_DialsTheNumberAdded_WhileTheFirstCallStaysHeld()
    {
        // Arrange
        var page = await OpenAsync(Standalone, DesktopAppViewport);
        await DialAndConnectAsync(page, "+15551234567");
        var held = await GetCurrentCallIdAsync(page);
        var dials = Server.Provider.GetDialRequestCount();
        await page.ClickAsync("[data-telephony-add-call]");
        await page.Locator("[data-telephony-resume]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        await page.WaitForFunctionAsync("() => document.activeElement && document.activeElement.hasAttribute('data-telephony-number')");

        // Act
        await page.Keyboard.TypeAsync("5557654321");
        await page.Keyboard.PressAsync("Enter");

        // Assert
        await WaitForAsync(() => Server.Provider.GetDialRequestCount() == dials + 1);
        Assert.Contains("5557654321", Server.Provider.GetLastDial().To);
        Assert.DoesNotContain($"Resume:{held}", Server.Provider.HoldCommands);
        await PublishLatestCallStateAsync(page);
        await page.Locator("[data-telephony-call-controls]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Two lines: each with its state, and ending every call is offered, outlined rather than red.
        var lines = page.Locator("[data-telephony-active-calls-list] .telephony-soft-phone__line-state");
        Assert.Equal(["Active", "On hold"], (await lines.AllInnerTextsAsync()).Select(text => text.Trim()).Order());
        Assert.Equal("End all", await page.Locator("[data-telephony-hangup-all]").GetAttributeAsync("aria-label"));
        Assert.Equal(1, await CountRedControlsAsync(page));
        await AssertFitsTheWindowAsync(page);
    }

    [Theory]
    [InlineData("?widget&embedded&browserAudio", 430, 740)]
    [InlineData("?widget&browserAudio", 1280, 800)]
    public async Task TheSettings_UseTheWindowsHeight_RatherThanAFixedBox(string query, int width, int height)
    {
        // Arrange
        var page = await OpenAsync(query, new ViewportSize { Width = width, Height = height });

        if (!query.Contains("embedded", StringComparison.Ordinal))
        {
            await page.ClickAsync("[data-telephony-toggle]");
        }

        await page.Locator("[data-telephony-settings-toggle]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Act
        await page.ClickAsync("[data-telephony-settings-toggle]");
        var settings = page.Locator("[data-telephony-settings-panel]");
        await settings.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Assert - the panel was capped at 32rem (512 px) and scrolled inside that box even with room to spare.
        var box = await settings.BoundingBoxAsync();
        Assert.NotNull(box);
        Assert.True(box.Height > 560, $"The settings are {box.Height}px tall in a {height}px window.");
        Assert.True(box.Y >= 0 && box.Y + box.Height <= height + 1, $"The settings span {box.Y}..{box.Y + box.Height}px, outside the {height}px window.");
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth"));
    }

    private static async Task<string[]> VisibleLabelsAsync(IPage page, string group)
        => [.. (await page.Locator($"{group} button:visible").AllInnerTextsAsync()).Select(text => text.Trim())];

    // Buttons in the call area painted red: a filled background, not an outline.
    private static Task<int> CountRedControlsAsync(IPage page)
        => page.EvaluateAsync<int>(
            """
            () => Array.from(document.querySelectorAll('.telephony-soft-phone__actions button, .telephony-soft-phone__actions button *'))
                .filter(element => element.offsetParent !== null)
                .filter(element => {
                    const match = getComputedStyle(element).backgroundColor.match(/\d+/g);
                    return match && Number(match[0]) > 150 && Number(match[1]) < 90 && Number(match[2]) < 90 && (match[3] === undefined || Number(match[3]) > 0);
                }).length
            """);

    private static async Task AssertFitsTheWindowAsync(IPage page)
    {
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth"));

        var outside = await page.EvaluateAsync<string[]>(
            """
            width => Array.from(document.querySelectorAll('.telephony-soft-phone__actions button, [data-telephony-active-calls] button'))
                .filter(element => element.offsetParent !== null)
                .filter(element => { const box = element.getBoundingClientRect(); return box.left < 0 || box.right > width; })
                .map(element => element.outerHTML.slice(0, 80))
            """,
            DesktopAppViewport.Width);

        Assert.Empty(outside);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(100);
        }

        Assert.True(condition());
    }
}
