using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Calling an extension from the keypad and from the Recent list: the field starts fresh after a call, an extension is
/// found by name, and a Recent extension call is called back as the extension it was.
/// </summary>
/// <remarks>
/// Live, after an extension call ended with another call held, the keypad's field went on showing "Test 2 · ext 2" --
/// the ended call's label -- as though that call were still up, because the held call had no number of its own to write
/// over it. And the field took digits only, so a colleague could be called only by knowing their extension.
/// </remarks>
public sealed class SoftPhoneKeypadExtensionTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task AfterAnExtensionCallEnds_WithAnotherCallHeld_TheFieldNoLongerShowsItsLabel_AndTypingStartsFresh()
    {
        // Arrange - a call on hold with no number to show, and an extension call up.
        var page = await OpenKeypadAsync();
        await PublishTrackedAsync(page, "held-1", state: 4, extension: null);
        await PublishTrackedAsync(page, "ext-call-1", state: 3, extension: "2");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-number]').value === 'Jane Doe · ext 2'");

        // Act - the extension call ends; the held call is the one on screen again.
        await PublishTrackedAsync(page, "ext-call-1", state: 5, extension: "2");
        await page.WaitForFunctionAsync("() => window.telephonySoftPhone.getInstance().getActiveCalls().length === 1");
        await page.WaitForTimeoutAsync(200);

        // Assert
        Assert.DoesNotContain("ext 2", await page.Locator("[data-telephony-number]").InputValueAsync());
        await CaptureAsync(page, "keypad-field-after-extension-call");

        // Typing starts a fresh entry, and the extension typed is the one dialed.
        await page.ClickAsync("[data-telephony-dial-mode-toggle]");
        await page.ClickAsync("[data-telephony-key=\"2\"]");
        Assert.Equal("2", await page.Locator("[data-telephony-number]").InputValueAsync());
        await page.ClickAsync("[data-telephony-dial]");
        await WaitForAsync(() => Server.Provider.GetLastExtensionDial()?.Extension == "2");
    }

    [Fact]
    public async Task Recent_CallingBackAnExtensionCall_DialsTheExtension()
    {
        // Arrange
        var page = await OpenKeypadAsync();
        await page.ClickAsync("[data-telephony-tab=\"history\"]");
        var callBack = page.Locator("[data-telephony-history-extension=\"true\"]");
        await callBack.WaitForAsync();

        // Act
        await callBack.ClickAsync();

        // Assert - the extension, never the name it was stored with, and never as a phone number.
        await WaitForAsync(() => Server.Provider.GetExtensionDialCount() == 1);
        Assert.Equal("2", Server.Provider.GetLastExtensionDial().Extension);
        Assert.Equal(0, Server.Provider.GetDialRequestCount());
    }

    [Fact]
    public async Task KeypadExtensionField_FindsAColleagueByName_AndDialsThePickedExtension()
    {
        // Arrange
        var page = await OpenKeypadAsync();
        await page.ClickAsync("[data-telephony-dial-mode-toggle]");

        // Act - letters reach the field, and the list shows who they match.
        await page.Locator("[data-telephony-number]").PressSequentiallyAsync("jan");
        var jane = page.Locator("[data-telephony-keypad-extension=\"2\"]");
        await jane.WaitForAsync();

        // Assert
        Assert.Equal("jan", await page.Locator("[data-telephony-number]").InputValueAsync());
        Assert.Contains("Jane Doe", await jane.InnerTextAsync());
        Assert.Equal(0, await page.Locator("[data-telephony-keypad-extension=\"3\"]").CountAsync());
        await AssertFitsTheWindowAsync(page, "[data-telephony-keypad-results]");
        await CaptureAsync(page, "keypad-extension-search");

        await jane.ClickAsync();
        await WaitForAsync(() => Server.Provider.GetLastExtensionDial()?.Extension == "2");
    }

    [Fact]
    public async Task KeypadExtensionField_EnterDialsTheOnePersonANameNarrowsTo_AndRefusesTheAgentsOwnExtension()
    {
        // Arrange - the agent is extension 3.
        Server.Provider.UseExtensions(
            ["3"],
            new TelephonyExtensionDirectoryEntry { Extension = "2", DisplayName = "Jane Doe", UserName = "jdoe" },
            new TelephonyExtensionDirectoryEntry { Extension = "3", DisplayName = "Sam Lee", UserName = "slee" },
            new TelephonyExtensionDirectoryEntry { Extension = "4", DisplayName = "Janet Park", UserName = "jpark" });
        var page = await OpenKeypadAsync();
        await page.ClickAsync("[data-telephony-dial-mode-toggle]");

        // Act / Assert - the agent's own extension, by name or by digits.
        await page.Locator("[data-telephony-number]").PressSequentiallyAsync("sam");
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(0, await page.Locator("[data-telephony-keypad-extension]").CountAsync());
        await page.FillAsync("[data-telephony-number]", "3");
        await page.PressAsync("[data-telephony-number]", "Enter");
        await page.WaitForFunctionAsync("() => /your own extension/.test(document.querySelector('[data-telephony-error]').textContent)");
        Assert.Equal(0, Server.Provider.GetExtensionDialCount());

        // A name matching two people is not dialed; one matching one person is.
        await page.FillAsync("[data-telephony-number]", "jan");
        await page.PressAsync("[data-telephony-number]", "Enter");
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(0, Server.Provider.GetExtensionDialCount());

        await page.FillAsync("[data-telephony-number]", "janet");
        await page.PressAsync("[data-telephony-number]", "Enter");
        await WaitForAsync(() => Server.Provider.GetLastExtensionDial()?.Extension == "4");
    }

    // The held call's number in the field is a display; the agent adding a call had to delete it by hand first.
    [Fact]
    public async Task OnHold_ClickingTheField_ClearsTheHeldCallsNumber_AndDialsOnlyWhatIsTypedThen()
    {
        // Arrange
        var page = await OpenKeypadAsync();
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);
        var field = page.Locator("[data-telephony-number]");
        await page.WaitForFunctionAsync("() => /555/.test(document.querySelector('[data-telephony-number]').value)");

        // Act
        await field.ClickAsync();

        // Assert - empty, and it stays empty while the agent is in it.
        Assert.Equal(string.Empty, await field.InputValueAsync());
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(string.Empty, await field.InputValueAsync());
        await CaptureAsync(page, "keypad-field-cleared-on-hold");

        var dials = Server.Provider.GetDialRequestCount();
        await field.PressSequentiallyAsync("+15557654321");
        await page.ClickAsync("[data-telephony-dial]");
        await WaitForAsync(() => Server.Provider.GetDialRequestCount() == dials + 1);
        Assert.Equal("+15557654321", Server.Provider.GetLastDial().To);
    }

    [Fact]
    public async Task OnHold_SwitchingToExtension_LeavesTheFieldEmptyForTheExtension()
    {
        // Arrange
        var page = await OpenKeypadAsync();
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);
        await page.WaitForFunctionAsync("() => /555/.test(document.querySelector('[data-telephony-number]').value)");

        // Act - and the provider reports the held call again, which redraws the phone.
        await page.ClickAsync("[data-telephony-dial-mode-toggle]");
        await PublishLatestCallStateAsync(page);
        await page.WaitForTimeoutAsync(300);

        // Assert
        Assert.Equal(string.Empty, await page.Locator("[data-telephony-number]").InputValueAsync());

        // Typing an extension then dials that extension alone.
        await page.Locator("[data-telephony-number]").PressSequentiallyAsync("3");
        await page.ClickAsync("[data-telephony-dial]");
        await WaitForAsync(() => Server.Provider.GetLastExtensionDial()?.Extension == "3");
    }

    private async Task<IPage> OpenKeypadAsync()
    {
        var page = await OpenAsync("?styled", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await WaitForAsync(() => Server.Provider.GetExtensionDirectoryRequestCount() >= 1);

        return page;
    }

    private static async Task PublishTrackedAsync(IPage page, string callId, int state, string extension)
        => await page.EvaluateAsync(
            """
            ([callId, state, extension]) => window.telephonySoftPhone.getInstance().getConnection().invoke('PublishTrackedCallState', {
                callId,
                to: extension ? 'jdoe' : '',
                direction: 0,
                state: Number(state),
                isOnHold: Number(state) === 4,
                providerName: 'InMemory',
                startedUtc: new Date().toISOString(),
                metadata: extension ? { extensionNumber: extension } : {}
            })
            """,
            new object[] { callId, state, extension });

    private static async Task AssertFitsTheWindowAsync(IPage page, string selector)
    {
        var box = await page.Locator(selector).BoundingBoxAsync();

        Assert.NotNull(box);
        Assert.True(box.X >= 0 && box.X + box.Width <= DesktopAppViewport.Width, $"{selector} spans {box.X}..{box.X + box.Width}px.");
        Assert.True(box.Y + box.Height <= DesktopAppViewport.Height, $"{selector} ends at {box.Y + box.Height}px.");
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth"));
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
