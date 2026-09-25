using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Joining calls into a conference. With one call held and a second call up, agents could not see how to join them:
/// Merge only appeared, as an unlabelled icon, once two checkboxes were ticked. These tests hold the phone to offering a
/// labelled Merge beside the call lines that names the calls it joins, and to showing the conference it makes.
/// </summary>
public sealed class SoftPhoneMergeCallsTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task Merge_WithAHeldCallAndAnActiveCall_IsOffered_AndMakesAConference()
    {
        // Arrange
        var page = await CreateHeldAndActiveCallAsync();
        var baselineCount = await InvokeCountAsync(page, "GetMergeRequestCount");

        // Assert - Merge is offered as soon as two calls could be joined, and once both are ticked names them.
        var merge = page.Locator("[data-telephony-merge-calls]");
        await merge.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.Locator("[data-telephony-merge-select-all]").CheckAsync();
        merge = page.Locator("[data-telephony-merge-calls]");
        var summary = await page.Locator("[data-telephony-merge-summary]").InnerTextAsync();
        Assert.Contains("(555) 123-4567", summary);
        Assert.Contains("(555) 765-4321", summary);
        Assert.Contains("Merge 2 calls", await merge.InnerTextAsync());
        await AssertFitsTheWindowAsync(page, "[data-telephony-active-calls]");
        await CaptureAsync(page, "merge-offered");

        // Act
        await merge.ClickAsync();

        // Assert - one merge of both calls, and the conference is listed with both participants.
        await page.WaitForFunctionAsync(
            "([count]) => window.telephonySoftPhone.getInstance().getConnection().invoke('GetMergeRequestCount').then(value => value === count + 1)",
            new[] { baselineCount });
        var conference = page.Locator("[data-telephony-conference]");
        await conference.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Contains("2 participants", await conference.InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await page.Locator("[data-telephony-conference-participant]").CountAsync());
        Assert.Equal(2, await page.Locator("[data-telephony-participant-hangup]").CountAsync());
        Assert.Equal(0, await page.Locator("[data-telephony-merge-calls]").CountAsync());
        await CaptureAsync(page, "conference");
    }

    [Fact]
    public async Task ConferenceParticipant_HangUp_EndsOnlyThatCall()
    {
        // Arrange
        var page = await CreateHeldAndActiveCallAsync();
        await page.Locator("[data-telephony-merge-select-all]").CheckAsync();
        await page.ClickAsync("[data-telephony-merge-calls]");
        await page.Locator("[data-telephony-participant-hangup]").Nth(1).WaitForAsync();
        var baselineCount = await InvokeCountAsync(page, "GetHangupRequestCount");
        var participant = await page.Locator("[data-telephony-participant-hangup]").Nth(0).GetAttributeAsync("data-telephony-participant-hangup");

        // Act
        await page.Locator("[data-telephony-participant-hangup]").Nth(0).ClickAsync();

        // Assert - exactly one hang-up, for that participant; the other call stays up.
        await page.WaitForFunctionAsync(
            "([count]) => window.telephonySoftPhone.getInstance().getConnection().invoke('GetHangupRequestCount').then(value => value === count + 1)",
            new[] { baselineCount });
        await page.WaitForFunctionAsync(
            "([callId]) => !document.querySelector(`[data-telephony-participant-hangup=\"${callId}\"]`)",
            new[] { participant });
        Assert.True(await page.Locator("[data-telephony-hangup]").IsVisibleAsync());
    }

    private async Task<IPage> CreateHeldAndActiveCallAsync()
    {
        var page = await OpenAsync("?styled", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);
        await DialAndConnectAsync(page, "+15557654321");
        await page.Locator("[data-telephony-call-select]").Nth(1).WaitForAsync();

        return page;
    }

    private static Task<int> InvokeCountAsync(IPage page, string method)
    {
        return page.EvaluateAsync<int>(
            "([method]) => window.telephonySoftPhone.getInstance().getConnection().invoke(method)",
            new[] { method });
    }

    private static async Task AssertFitsTheWindowAsync(IPage page, string selector)
    {
        var box = await page.Locator(selector).BoundingBoxAsync();

        Assert.NotNull(box);
        Assert.True(box.X >= 0 && box.X + box.Width <= DesktopAppViewport.Width, $"{selector} spans {box.X}..{box.X + box.Width}px, wider than the {DesktopAppViewport.Width}px window.");
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth"));
    }
}
