using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Searching the transfer panel by name, and never offering the agent their own extension.
/// </summary>
/// <remarks>
/// <para>
/// Typing a name into the transfer panel did nothing: the field refused every letter. It was enhanced with the keypad's
/// country-flag input (intl-tel-input), whose strict mode -- on by default -- drops any key that is not a digit, in the
/// extension mode as much as in the number mode. The extension mode's field is now a plain search box, and the number
/// mode's keeps its flag with the strict keys off, so a colleague is found by name in either.
/// </para>
/// <para>
/// The agent was also listed among the people to transfer to -- "Mike Alhayek, Ext 1" on Mike Alhayek's own phone -- and
/// could dial or transfer to their own extension, which only rings the phone they are using.
/// </para>
/// <para>
/// Keys are typed one by one, as a person types them: filling the field in one go skips the key filter that dropped them.
/// </para>
/// </remarks>
public sealed class SoftPhoneTransferNameSearchTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task InExtensionMode_TypingAName_NarrowsTheListToThem_AndEnterTransfersToThem()
    {
        // Arrange
        var page = await OpenOnACallAsync();
        await OpenTransferPanelAsync(page);
        await SwitchToExtensionAsync(page);

        // Act
        await page.Locator("[data-telephony-transfer-input]").PressSequentiallyAsync("Test");

        // Assert - the letters reached the field, and the list is down to the one person they match.
        Assert.Equal("Test", await page.InputValueAsync("[data-telephony-transfer-input]"));
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-telephony-directory-destination]').length === 1");
        Assert.Contains("Test 2", await page.Locator("[data-telephony-directory-destination=\"2\"]").InnerTextAsync());
        await CaptureAsync(page, "transfer-name-search");

        // Act - Enter, without clicking the row.
        await page.PressAsync("[data-telephony-transfer-input]", "Enter");

        // Assert
        await WaitForAsync(() => Server.Provider.GetTransferRequestCount() == 1);
        Assert.Equal("2", Server.Provider.GetLastTransfer().To);
        Assert.True(Server.Provider.GetLastTransfer().IsExtension);
    }

    [Fact]
    public async Task InNumberMode_TypingAName_IsKeptByTheCountryFlagField_AndSearchesTheList()
    {
        // Arrange
        var page = await OpenOnACallAsync();
        await OpenTransferPanelAsync(page);
        Assert.True(await page.Locator("[data-telephony-transfer-panel] .iti__selected-country").IsVisibleAsync());

        // Act
        await page.Locator("[data-telephony-transfer-input]").PressSequentiallyAsync("Test");

        // Assert
        Assert.Equal("Test", await page.InputValueAsync("[data-telephony-transfer-input]"));
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-telephony-directory-destination]').length === 1");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        await WaitForAsync(() => Server.Provider.GetTransferRequestCount() == 1);
        Assert.Equal("2", Server.Provider.GetLastTransfer().To);
        Assert.True(Server.Provider.GetLastTransfer().IsExtension);
    }

    [Fact]
    public async Task TheAgentsOwnExtension_IsNotListed_AndTypingItIsRefused()
    {
        // Arrange
        var page = await OpenOnACallAsync();
        await OpenTransferPanelAsync(page);

        // Assert - everybody but the agent.
        Assert.Equal(0, await page.Locator("[data-telephony-directory-destination=\"1\"]").CountAsync());
        Assert.Equal(1, await page.Locator("[data-telephony-directory-destination=\"3\"]").CountAsync());

        // Act
        await SwitchToExtensionAsync(page);
        await page.Locator("[data-telephony-transfer-input]").PressSequentiallyAsync("1");

        // Assert - not offered as somewhere to transfer to, and refused if the agent presses Enter anyway.
        var own = page.Locator("[data-telephony-transfer-own]");
        await own.WaitForAsync();
        Assert.Equal("That's your own extension.", (await own.InnerTextAsync()).Trim());
        Assert.Equal(0, await page.Locator("[data-telephony-transfer-number]").CountAsync());
        await CaptureAsync(page, "transfer-own-extension");

        await page.PressAsync("[data-telephony-transfer-input]", "Enter");

        var error = page.Locator("[data-telephony-transfer-error]");
        await error.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Equal("That's your own extension.", (await error.InnerTextAsync()).Trim());
        Assert.Equal(0, Server.Provider.GetTransferRequestCount());
    }

    [Fact]
    public async Task TheKeypad_RefusesToDialTheAgentsOwnExtension()
    {
        // Arrange
        UseExtensions();
        var page = await OpenAsync("?intlTelInput&styled", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await WaitForAsync(() => Server.Provider.GetExtensionDirectoryRequestCount() >= 1);
        await page.ClickAsync("[data-telephony-dial-mode-toggle]");

        // Act
        await page.Locator("[data-telephony-number]").PressSequentiallyAsync("1");
        await page.ClickAsync("[data-telephony-dial]");

        // Assert
        var error = page.Locator("[data-telephony-error]");
        await error.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Equal("That's your own extension.", (await error.InnerTextAsync()).Trim());
        Assert.Equal(0, Server.Provider.GetExtensionDialCount());

        // A colleague's extension is still dialed.
        await page.FillAsync("[data-telephony-number]", string.Empty);
        await page.Locator("[data-telephony-number]").PressSequentiallyAsync("2");
        await page.ClickAsync("[data-telephony-dial]");
        await WaitForAsync(() => Server.Provider.GetExtensionDialCount() == 1);
    }

    // The signed-in user is Mike Alhayek, on extension 1.
    private void UseExtensions()
    {
        Server.Provider.RemoveDirectory();
        Server.Provider.UseExtensions(
            ["1"],
            new TelephonyExtensionDirectoryEntry { Extension = "1", DisplayName = "Mike Alhayek" },
            new TelephonyExtensionDirectoryEntry { Extension = "2", DisplayName = "Test 2" },
            new TelephonyExtensionDirectoryEntry { Extension = "3", DisplayName = "Sam Lee" });
    }

    private async Task<IPage> OpenOnACallAsync()
    {
        UseExtensions();

        var page = await OpenAsync("?intlTelInput&styled", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+17025550142");

        return page;
    }

    private static async Task OpenTransferPanelAsync(IPage page)
    {
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"2\"]").WaitForAsync();
    }

    private static async Task SwitchToExtensionAsync(IPage page)
    {
        await page.ClickAsync("[data-telephony-transfer-dial-mode]");
        Assert.Equal("true", await page.Locator("[data-telephony-transfer-dial-mode]").GetAttributeAsync("aria-pressed"));
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
