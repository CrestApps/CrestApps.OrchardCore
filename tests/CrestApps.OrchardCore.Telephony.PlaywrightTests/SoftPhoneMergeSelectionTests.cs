using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Picking the calls to merge. With several calls up, the agent ticks the box beside each line in the keypad view and
/// merges the ticked ones; Merge names how many it joins and does nothing until two or more are ticked. A running
/// conference is one line: ticking it and another call adds that call to it. Everything fits the desktop app's window.
/// </summary>
public sealed class SoftPhoneMergeSelectionTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task EveryLineHasACheckbox_AndMergeIsEnabledOnlyOnceTwoAreTicked()
    {
        // Arrange
        var page = await OpenAsync("?styled", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        var first = await DialAsync(page, "+15551234567");
        await HoldAsync(page);
        var second = await DialAsync(page, "+15557654321");
        var merge = page.Locator("[data-telephony-merge-calls]");
        await merge.WaitForAsync();

        // Assert - a checkbox beside each line in the keypad view, and Merge shown but not yet usable.
        Assert.True(await page.Locator("[data-telephony-view=\"keypad\"] [data-telephony-active-calls]").IsVisibleAsync());
        Assert.Equal(2, await page.Locator("[data-telephony-conference-call]").CountAsync());
        Assert.True(await merge.IsDisabledAsync());
        Assert.Contains("two or more", await page.Locator("[data-telephony-merge-summary]").InnerTextAsync());
        await AssertFitsTheWindowAsync(page, "[data-telephony-active-calls]");
        await CaptureAsync(page, "merge-select-none");

        // Act - one ticked.
        await Checkbox(page, first).CheckAsync();

        // Assert
        Assert.True(await page.Locator("[data-telephony-merge-calls]").IsDisabledAsync());

        // Act - two ticked.
        await Checkbox(page, second).CheckAsync();

        // Assert
        merge = page.Locator("[data-telephony-merge-calls]");
        Assert.True(await merge.IsEnabledAsync());
        Assert.Contains("Merge 2 calls", await merge.InnerTextAsync());
        Assert.True(await page.Locator("[data-telephony-merge-select-all]").IsCheckedAsync());
        await AssertFitsTheWindowAsync(page, "[data-telephony-active-calls]");
        await CaptureAsync(page, "merge-select-two");

        // Act - untick one again.
        await Checkbox(page, first).UncheckAsync();

        // Assert
        Assert.True(await page.Locator("[data-telephony-merge-calls]").IsDisabledAsync());
        Assert.Equal(0, await InvokeCountAsync(page, "GetMergeRequestCount"));
    }

    [Fact]
    public async Task MergingThreeTickedLines_MakesOneConferenceOfAllThree()
    {
        // Arrange
        var page = await OpenAsync("?styled", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        var callIds = new List<string> { await DialAsync(page, "+15551234567") };
        await HoldAsync(page);
        callIds.Add(await DialAsync(page, "+15557654321"));
        await HoldAsync(page);
        callIds.Add(await DialAsync(page, "+15559876543"));
        await page.Locator("[data-telephony-merge-select-all]").WaitForAsync();

        // Act
        await page.Locator("[data-telephony-merge-select-all]").CheckAsync();
        var merge = page.Locator("[data-telephony-merge-calls]");
        Assert.Contains("Merge 3 calls", await merge.InnerTextAsync());
        await AssertFitsTheWindowAsync(page, "[data-telephony-active-calls]");
        await CaptureAsync(page, "merge-select-three");
        await merge.ClickAsync();

        // Assert - one merge naming all three, and one conference of three on screen.
        var request = await WaitForMergeAsync(page, 1);
        Assert.Equal(callIds.Order(), request.GetProperty("callIds").EnumerateArray().Select(id => id.GetString()).Order());
        var conference = page.Locator("[data-telephony-conference]");
        await conference.WaitForAsync();
        Assert.Contains("3 participants", await conference.InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, await page.Locator("[data-telephony-conference-participant]").CountAsync());
        Assert.Equal(0, await page.Locator("[data-telephony-merge-calls]").CountAsync());
        await AssertFitsTheWindowAsync(page, "[data-telephony-active-calls]");
        await CaptureAsync(page, "merge-conference-of-three");
    }

    [Fact]
    public async Task TickingTheConferenceAndAnotherLine_AddsThatLineToTheConference()
    {
        // Arrange - a conference of two, held, and a third call up.
        var page = await OpenAsync("?styled", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        var first = await DialAsync(page, "+15551234567");
        await HoldAsync(page);
        var second = await DialAsync(page, "+15557654321");
        await page.Locator("[data-telephony-merge-select-all]").CheckAsync();
        await page.ClickAsync("[data-telephony-merge-calls]");
        var made = await WaitForMergeAsync(page, 1);
        var primary = made.GetProperty("callIds")[0].GetString();
        Assert.Contains(primary, new[] { first, second });
        await page.Locator("[data-telephony-conference]").WaitForAsync();
        await HoldAsync(page);
        var third = await DialAsync(page, "+15559876543");
        await page.Locator("[data-telephony-merge-calls]").WaitForAsync();

        // Assert - the conference is one line to join, so one tick is not yet enough.
        Assert.True(await page.Locator("[data-telephony-merge-calls]").IsDisabledAsync());

        // Act
        await Checkbox(page, second).CheckAsync();
        await Checkbox(page, third).CheckAsync();
        var add = page.Locator("[data-telephony-merge-calls]");
        Assert.Contains("Add to conference", await add.InnerTextAsync());
        await CaptureAsync(page, "merge-add-to-conference");
        await add.ClickAsync();

        // Assert - the conference is named back to the provider, with only the call that joins it.
        var request = await WaitForMergeAsync(page, 2);
        Assert.Equal([primary, third], request.GetProperty("callIds").EnumerateArray().Select(id => id.GetString()));
        Assert.Equal("conf-" + primary, request.GetProperty("conferenceName").GetString());
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-telephony-conference-participant]').length === 3");
        Assert.Contains("3 participants", await page.Locator("[data-telephony-conference]").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        await AssertFitsTheWindowAsync(page, "[data-telephony-active-calls]");
    }

    [Fact]
    public async Task CallsDialedStraightFromThisBrowser_ShowADisabledCheckbox_AndSayWhyTheyCannotBeMerged()
    {
        // Arrange - on Telnyx a keypad call is placed by the browser's own SDK; the server cannot conference it.
        Server.Provider.BrowserMediaAdapterName = "telnyx-webrtc";
        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = DesktopAppViewport });
        await page.AddInitScriptAsync(FakeTelnyxSdk.Script);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true&styled");
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await DialInBrowserAsync(page, "+17024993350");
        await page.ClickAsync("[data-telephony-hold]");
        await page.Locator("[data-telephony-resume]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await DialInBrowserAsync(page, "+17025550142");

        // Act
        var note = page.Locator("[data-telephony-merge-blocked]");
        await note.WaitForAsync();

        // Assert
        var checkboxes = page.Locator("[data-telephony-conference-call]");
        Assert.Equal(2, await checkboxes.CountAsync());
        Assert.True(await checkboxes.Nth(0).IsDisabledAsync());
        Assert.True(await checkboxes.Nth(1).IsDisabledAsync());
        Assert.Contains("cannot be merged", await note.InnerTextAsync());
        Assert.Equal(0, await page.Locator("[data-telephony-merge-calls]").CountAsync());
        await AssertFitsTheWindowAsync(page, "[data-telephony-active-calls]");
        await CaptureAsync(page, "merge-browser-calls");
    }

    private static ILocator Checkbox(IPage page, string callId)
        => page.Locator($"[data-telephony-conference-call=\"{callId}\"]");

    // Places a call from the keypad, has the provider report it connected, and returns its id.
    private static async Task<string> DialAsync(IPage page, string number)
    {
        await DialAndConnectAsync(page, number);

        return await GetCurrentCallIdAsync(page);
    }

    private static async Task DialInBrowserAsync(IPage page, string number)
    {
        var before = await page.EvaluateAsync<int>("() => window.telephonySoftPhone.getInstance().getActiveCalls().length");

        await page.FillAsync("[data-telephony-number]", number);
        await page.ClickAsync("[data-telephony-dial]");
        await page.WaitForFunctionAsync(
            "(count) => window.telephonySoftPhone.getInstance().getActiveCalls().filter(call => call.state === 'Connected').length > 0 && window.telephonySoftPhone.getInstance().getActiveCalls().length === count + 1",
            before,
            new PageWaitForFunctionOptions { Timeout = 10000 });
    }

    private static async Task<JsonElement> WaitForMergeAsync(IPage page, int count)
    {
        await page.WaitForFunctionAsync(
            "(count) => window.telephonySoftPhone.getInstance().getConnection().invoke('GetMergeRequestCount').then(value => value === count)",
            count);

        return await page.EvaluateAsync<JsonElement>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetLastMerge')");
    }

    private static Task<int> InvokeCountAsync(IPage page, string method)
        => page.EvaluateAsync<int>(
            "([method]) => window.telephonySoftPhone.getInstance().getConnection().invoke(method)",
            new[] { method });

    private static async Task AssertFitsTheWindowAsync(IPage page, string selector)
    {
        var box = await page.Locator(selector).BoundingBoxAsync();

        Assert.NotNull(box);
        Assert.True(box.X >= 0 && box.X + box.Width <= DesktopAppViewport.Width, $"{selector} spans {box.X}..{box.X + box.Width}px, wider than the {DesktopAppViewport.Width}px window.");
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth"));
    }
}
