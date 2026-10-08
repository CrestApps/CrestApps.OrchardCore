using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// The name of the person behind an extension, wherever the phone shows one. An extension on its own -- "2" -- told the
/// agent nothing about who they were calling or handing a caller to; the server now names each extension's person, and
/// the phone reads that list once and shows the name beside the extension.
/// </summary>
public sealed class SoftPhoneExtensionNameTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task TransferPanel_OffersATypedExtension_WithTheNameOfWhoeverItRings()
    {
        // Arrange
        var page = await OpenOnACallAsync();
        await page.ClickAsync("[data-telephony-transfer]");
        await page.ClickAsync("[data-telephony-transfer-dial-mode]");

        // Act
        await page.FillAsync("[data-telephony-transfer-input]", "2");

        // Assert
        var offer = page.Locator("[data-telephony-transfer-number]");
        await offer.WaitForAsync();
        Assert.Equal("Transfer to extension 2 · Jane Doe", (await offer.InnerTextAsync()).Trim());

        // An extension nobody is behind is offered as it is.
        await page.FillAsync("[data-telephony-transfer-input]", "9");
        Assert.Equal("Transfer to extension 9", (await offer.InnerTextAsync()).Trim());
        await CaptureAsync(page, "transfer-extension-named");
    }

    [Fact]
    public async Task TransferPanel_WithoutAProviderDirectory_ListsTheExtensionsByName_AndSendsAPickedOneAsAnExtension()
    {
        // Arrange - a provider with no directory of its own, as Telnyx has none.
        Server.Provider.RemoveDirectory();
        var page = await OpenOnACallAsync();

        // Act
        await page.ClickAsync("[data-telephony-transfer]");
        var jane = page.Locator("[data-telephony-directory-destination=\"2\"]");
        await jane.WaitForAsync();

        // Assert
        Assert.Contains("Jane Doe", await jane.InnerTextAsync());
        Assert.Contains("Ext 2", await jane.InnerTextAsync());
        Assert.Contains("Sam Lee", await page.Locator("[data-telephony-directory-destination=\"3\"]").InnerTextAsync());

        // Act - pick Jane and transfer.
        await jane.ClickAsync();
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert - rung as extension 2, not dialed as the number 2.
        await WaitForAsync(() => Server.Provider.GetTransferRequestCount() == 1);
        Assert.Equal("2", Server.Provider.GetLastTransfer().To);
        Assert.True(Server.Provider.GetLastTransfer().IsExtension);
    }

    [Fact]
    public async Task Keypad_NamesTheExtensionAsItIsTyped_WithoutALookupPerKeystroke()
    {
        // Arrange
        var page = await OpenAsync("", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await WaitForAsync(() => Server.Provider.GetExtensionDirectoryRequestCount() == 1);
        await page.ClickAsync("[data-telephony-dial-mode-toggle]");
        var hint = page.Locator("[data-telephony-extension-hint]");

        // Act & Assert
        await page.Locator("[data-telephony-number]").PressSequentiallyAsync("2");
        await hint.WaitForAsync();
        Assert.Equal("Jane Doe", (await hint.InnerTextAsync()).Trim());

        await page.Locator("[data-telephony-number]").PressSequentiallyAsync("3");
        await hint.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

        await page.FillAsync("[data-telephony-number]", string.Empty);
        await page.Locator("[data-telephony-number]").PressSequentiallyAsync("3");
        await hint.WaitForAsync();
        Assert.Equal("Sam Lee", (await hint.InnerTextAsync()).Trim());

        // The names were read once, when the phone connected, not once per key.
        Assert.Equal(1, Server.Provider.GetExtensionDirectoryRequestCount());
    }

    [Fact]
    public async Task InCall_ACallToAnExtension_IsShownAsThePersonAndTheExtension()
    {
        // Arrange
        var page = await OpenAsync("", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await WaitForAsync(() => Server.Provider.GetExtensionDirectoryRequestCount() == 1);

        // Act - the platform reports the agent's call to extension 2, named by the username it rang.
        await page.EvaluateAsync(
            """
            () => window.telephonySoftPhone.getInstance().getConnection().invoke('PublishTrackedCallState', {
                callId: 'ext-call-1',
                to: 'jdoe',
                direction: 0,
                state: 3,
                providerName: 'InMemory',
                metadata: { extensionNumber: '2' }
            })
            """);

        // Assert
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-number]').value === 'Jane Doe · ext 2'");

        // A later report of the call that no longer says which extension it was placed to still shows the person.
        await page.EvaluateAsync(
            """
            () => window.telephonySoftPhone.getInstance().getConnection().invoke('PublishCallState', {
                callId: 'ext-call-1',
                to: 'sip:gencred@sip.telnyx.com',
                direction: 0,
                state: 3,
                providerName: 'InMemory'
            })
            """);
        await page.WaitForTimeoutAsync(300);
        Assert.Equal("Jane Doe · ext 2", await page.Locator("[data-telephony-number]").InputValueAsync());
        await CaptureAsync(page, "in-call-extension-named");
    }

    [Fact]
    public async Task Recent_ACallToAnExtension_IsNamedAfterThePersonItRings()
    {
        // Arrange
        var page = await OpenAsync("", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await WaitForAsync(() => Server.Provider.GetExtensionDirectoryRequestCount() == 1);

        // Act
        await page.ClickAsync("[data-telephony-tab=\"history\"]");

        // Assert - the entry stored the username it rang; the phone shows the person's name and the extension.
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-telephony-history-list]').innerText.indexOf('Jane Doe · ext 2') !== -1");
        Assert.Contains("+1 (555) 123-4567", await page.Locator("[data-telephony-history-list]").InnerTextAsync());
    }

    private async Task<IPage> OpenOnACallAsync()
    {
        var page = await OpenAsync("", DesktopAppViewport);

        await page.ClickAsync("[data-telephony-toggle]");
        await WaitForAsync(() => Server.Provider.GetExtensionDirectoryRequestCount() == 1);
        await DialAndConnectAsync(page, "+15551234567");

        return page;
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
