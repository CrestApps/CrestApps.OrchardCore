using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Screenshots of the phone's in-call screens and settings on the real widget markup, for a person to look at. Nothing is
/// saved unless <c>SOFTPHONE_SCREENSHOT_DIR</c> names a folder; the pages load Bootstrap and Font Awesome from the CDN
/// (<c>?host</c>) so they look as they do on a real page.
/// </summary>
public sealed class SoftPhoneUxScreenshotTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task CaptureInCallScreens()
    {
        if (!Capturing)
        {
            return;
        }

        Server.Provider.KeepNoConferenceState();
        var page = await OpenAsync("?widget&embedded&host", DesktopAppViewport);
        await page.WaitForTimeoutAsync(500);

        await DialAndConnectAsync(page, "+15551234567");
        await page.WaitForTimeoutAsync(300);
        await CaptureAsync(page, "01-single-call");

        // Put the caller on hold and dial another number: Add call where the phone has it, else hold and type.
        var addCall = page.Locator("[data-telephony-add-call]");

        if (await addCall.CountAsync() > 0)
        {
            await addCall.ClickAsync();
            await page.Locator("[data-telephony-resume]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await page.WaitForTimeoutAsync(300);
            await CaptureAsync(page, "02a-add-call-empty-field");
            await page.Keyboard.TypeAsync("5557654321");
            await page.WaitForTimeoutAsync(200);
            await CaptureAsync(page, "02b-add-call-number-entered");
            await page.Keyboard.PressAsync("Enter");
        }
        else
        {
            await HoldAsync(page);
            await page.FillAsync("[data-telephony-number]", "+15557654321");
            await page.ClickAsync("[data-telephony-dial]");
        }

        await page.WaitForTimeoutAsync(300);
        await CaptureAsync(page, "02-held-and-dialing");

        await PublishLatestCallStateAsync(page);
        await page.Locator("[data-telephony-hold]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForTimeoutAsync(1200);
        await CaptureAsync(page, "03-two-lines");

        await page.Locator("[data-telephony-merge-select-all]").CheckAsync();
        await page.ClickAsync("[data-telephony-merge-calls]");
        await page.Locator("[data-telephony-conference]").WaitForAsync();
        await page.WaitForTimeoutAsync(300);
        await CaptureAsync(page, "04-conference");

        if (await page.Locator("[data-telephony-hangup-all]").IsVisibleAsync())
        {
            await page.ClickAsync("[data-telephony-hangup-all]");
            await page.WaitForTimeoutAsync(300);
            await CaptureAsync(page, "04b-conference-end-for-all-confirm");
        }
    }

    [Theory]
    [InlineData("?widget&embedded&host&browserAudio", 430, 740, "05-settings-standalone")]
    [InlineData("?widget&host&browserAudio", 1280, 800, "06-settings-floating-light")]
    [InlineData("?widget&host&browserAudio&dark", 1280, 800, "07-settings-floating-dark")]
    public async Task CaptureSettings(string query, int width, int height, string name)
    {
        if (!Capturing)
        {
            return;
        }

        var page = await OpenAsync(query, new ViewportSize { Width = width, Height = height });

        if (!query.Contains("embedded", StringComparison.Ordinal))
        {
            await page.ClickAsync("[data-telephony-toggle]");
        }

        await page.Locator("[data-telephony-settings-toggle]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.ClickAsync("[data-telephony-settings-toggle]");
        await page.Locator("[data-telephony-settings-panel]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForTimeoutAsync(500);
        await CaptureAsync(page, name);
    }

    [Theory]
    [InlineData("?widget&host", "08-two-lines-floating-light")]
    [InlineData("?widget&host&dark", "09-two-lines-floating-dark")]
    public async Task CaptureInCallFloating(string query, string name)
    {
        if (!Capturing)
        {
            return;
        }

        var page = await OpenAsync(query, new ViewportSize { Width = 1280, Height = 800 });
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);
        await DialAndConnectAsync(page, "+15557654321");
        await page.WaitForTimeoutAsync(300);
        await CaptureAsync(page, name);
    }

    private static bool Capturing => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SOFTPHONE_SCREENSHOT_DIR"));
}
