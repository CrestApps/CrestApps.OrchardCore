using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the soft phone's own transfer panel. Transferring used to open the browser's "Transfer to number" prompt,
/// which was headed with the site's address and cut off inside the desktop app's window; these tests hold the phone to
/// picking the target in its own panel, at the desktop app's size, and to never opening a native dialog.
/// </summary>
public sealed class SoftPhoneTransferPanelTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task Transfer_OpensTheInAppPanel_AndNeverANativeDialog()
    {
        // Arrange
        var (page, dialogs) = await OpenOnACallAsync("?styled");

        // Act
        await page.ClickAsync("[data-telephony-transfer]");

        // Assert - the panel opens inside the phone, in place of the keypad, and fits the desktop app's window.
        var panel = page.Locator("[data-telephony-transfer-panel]");
        await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-keypad-panel]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-transfer-input]").IsVisibleAsync());
        Assert.True(await page.Locator("[data-telephony-transfer-back]").IsVisibleAsync());
        await page.Locator("[data-telephony-directory-destination=\"2001\"]").WaitForAsync();

        var box = await panel.BoundingBoxAsync();
        Assert.NotNull(box);
        Assert.True(box.X >= 0 && box.X + box.Width <= DesktopAppViewport.Width, $"The transfer panel spans {box.X}..{box.X + box.Width}px, wider than the {DesktopAppViewport.Width}px window.");
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth"));
        await CaptureAsync(page, "transfer-panel");

        // Act - back returns to the keypad.
        await page.ClickAsync("[data-telephony-transfer-back]");

        // Assert
        Assert.True(await panel.IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-keypad-panel]").IsVisibleAsync());
        Assert.Empty(dialogs);
    }

    [Fact]
    public async Task BlindTransfer_ToATypedNumber_SendsTheNumberWithoutAPrompt()
    {
        // Arrange
        var (page, dialogs) = await OpenOnACallAsync();
        var callId = await GetCurrentCallIdAsync(page);
        await page.ClickAsync("[data-telephony-transfer]");

        // Assert - this provider only transfers blind, so there is no choice of mode to make.
        Assert.Equal(0, await page.Locator("[data-telephony-transfer-mode]").CountAsync());

        // Act
        await page.FillAsync("[data-telephony-transfer-input]", "(702) 555-0199");
        await page.ClickAsync("[data-telephony-transfer-number]");

        // Assert
        var transfer = await WaitForTransferAsync(page);
        Assert.Equal(callId, transfer.GetProperty("callId").GetString());
        Assert.Equal("+17025550199", transfer.GetProperty("to").GetString());
        Assert.False(transfer.GetProperty("isExtension").GetBoolean());
        Assert.Equal(0, transfer.GetProperty("mode").GetInt32());
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-transfer-panel]').hidden");
        Assert.Empty(dialogs);
    }

    [Fact]
    public async Task Transfer_WithNoTarget_SaysSoInThePanel_AndSendsNothing()
    {
        // Arrange
        var (page, dialogs) = await OpenOnACallAsync();
        await page.ClickAsync("[data-telephony-transfer]");

        // Act
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        var error = page.Locator("[data-telephony-transfer-error]");
        await error.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.False(string.IsNullOrWhiteSpace(await error.InnerTextAsync()));
        Assert.Equal(0, await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount')"));
        Assert.True(await page.Locator("[data-telephony-transfer-panel]").IsVisibleAsync());
        Assert.Empty(dialogs);
    }

    [Fact]
    public async Task Transfer_SearchNarrowsTheDirectory_AndThePickedEntryIsTransferredTo()
    {
        // Arrange
        var (page, _) = await OpenOnACallAsync();
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"2002\"]").WaitForAsync();

        // Act
        await page.FillAsync("[data-telephony-transfer-input]", "sam");

        // Assert
        Assert.Equal(1, await page.Locator("[data-telephony-directory-destination]").CountAsync());

        // Act
        await page.ClickAsync("[data-telephony-directory-destination=\"2002\"]");

        // Assert - the picked entry is marked and named on the button that sends the call.
        Assert.Equal("true", await page.Locator("[data-telephony-directory-destination=\"2002\"]").GetAttributeAsync("aria-pressed"));
        Assert.Contains("Sam Supervisor", await page.Locator("[data-telephony-transfer-confirm]").InnerTextAsync());

        // Act
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        var transfer = await WaitForTransferAsync(page);
        Assert.Equal("2002", transfer.GetProperty("to").GetString());
    }

    [Fact]
    public async Task WarmTransfer_WhenTheProviderOffersIt_IsAChoice_AndIsSentAsWarm()
    {
        // Arrange
        var (page, dialogs) = await OpenOnACallAsync("?attendedTransfer&styled");
        await page.ClickAsync("[data-telephony-transfer]");

        // Assert - both modes are offered, blind chosen first.
        var blind = page.Locator("[data-telephony-transfer-mode=\"blind\"]");
        var warm = page.Locator("[data-telephony-transfer-mode=\"warm\"]");
        await warm.WaitForAsync();
        Assert.Equal("true", await blind.GetAttributeAsync("aria-checked"));
        Assert.Equal("false", await warm.GetAttributeAsync("aria-checked"));

        // Act
        await warm.ClickAsync();
        await page.ClickAsync("[data-telephony-transfer-dial-mode]");
        await page.FillAsync("[data-telephony-transfer-input]", "2001");
        await CaptureAsync(page, "transfer-panel-warm");
        await page.PressAsync("[data-telephony-transfer-input]", "Enter");

        // Assert
        var transfer = await WaitForTransferAsync(page);
        Assert.Equal("2001", transfer.GetProperty("to").GetString());
        Assert.True(transfer.GetProperty("isExtension").GetBoolean());
        Assert.Equal(1, transfer.GetProperty("mode").GetInt32());
        Assert.Empty(dialogs);
    }

    private async Task<(IPage Page, List<string> Dialogs)> OpenOnACallAsync(string query = "")
    {
        var page = await OpenAsync(query, DesktopAppViewport);
        var dialogs = new List<string>();

        // A native dialog would block the page; record it and dismiss it so the test fails on the assertion, not a hang.
        page.Dialog += async (_, dialog) =>
        {
            dialogs.Add(dialog.Type + ": " + dialog.Message);
            await dialog.DismissAsync();
        };

        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");

        return (page, dialogs);
    }

    private static async Task<JsonElement> WaitForTransferAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount').then(value => value === 1)");

        return await page.EvaluateAsync<JsonElement>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetLastTransfer')");
    }
}
