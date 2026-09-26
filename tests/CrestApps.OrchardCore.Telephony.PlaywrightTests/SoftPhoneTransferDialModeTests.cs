using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// The transfer panel's Number / Extension toggle. Typing extension "2" into the panel was refused with "Enter a complete
/// phone number or extension.", because the panel had one text box and checked everything typed into it as a phone number
/// of two digits or more. The panel now has the keypad's own toggle: a number is typed into the keypad's country-flag
/// field and checked the way the keypad checks one, and an extension is sent as an extension.
/// </summary>
public sealed class SoftPhoneTransferDialModeTests : SoftPhoneBrowserTest
{
    private const string OwnNumber = "+17025550100";
    private const string InteractionId = "interaction-1";

    [Fact]
    public async Task BlindTransfer_ToExtension2_IsSentAsAnExtension()
    {
        // Arrange
        var page = await OpenOnACallAsync("?intlTelInput&styled");
        await page.ClickAsync("[data-telephony-transfer]");

        // Act
        await SwitchToExtensionAsync(page);
        await page.FillAsync("[data-telephony-transfer-input]", "2");
        Assert.Contains("extension 2", await page.Locator("[data-telephony-transfer-number]").InnerTextAsync());
        await CaptureAsync(page, "transfer-extension");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        var transfer = await WaitForTransferAsync(page);
        Assert.Equal("2", transfer.GetProperty("to").GetString());
        Assert.True(transfer.GetProperty("isExtension").GetBoolean());
        Assert.Equal(0, transfer.GetProperty("mode").GetInt32());
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-transfer-panel]').hidden");
    }

    [Fact]
    public async Task WarmTransfer_ToExtension2_IsSentAsAnExtension()
    {
        // Arrange - the screenshot's case: warm chosen, "2" typed.
        var page = await OpenOnACallAsync("?intlTelInput&attendedTransfer&styled");
        await page.ClickAsync("[data-telephony-transfer]");
        await page.ClickAsync("[data-telephony-transfer-mode=\"warm\"]");

        // Act
        await SwitchToExtensionAsync(page);
        await page.FillAsync("[data-telephony-transfer-input]", "2");
        await page.PressAsync("[data-telephony-transfer-input]", "Enter");

        // Assert
        var transfer = await WaitForTransferAsync(page);
        Assert.Equal("2", transfer.GetProperty("to").GetString());
        Assert.True(transfer.GetProperty("isExtension").GetBoolean());
        Assert.Equal(1, transfer.GetProperty("mode").GetInt32());
        Assert.True(await page.Locator("[data-telephony-transfer-error]").IsHiddenAsync());
    }

    [Fact]
    public async Task NumberMode_UsesTheKeypadsCountryFlagInput_AndSendsTheNumberInInternationalForm()
    {
        // Arrange
        var page = await OpenOnACallAsync("?intlTelInput&styled");
        await page.ClickAsync("[data-telephony-transfer]");

        // Assert - the field is the keypad's: intl-tel-input with its country flag, on the keypad's country.
        var panel = page.Locator("[data-telephony-transfer-panel]");
        await panel.Locator(".iti [data-telephony-transfer-input]").WaitForAsync();
        Assert.True(await panel.Locator(".iti__selected-country").IsVisibleAsync());
        Assert.Equal("false", await page.Locator("[data-telephony-transfer-dial-mode]").GetAttributeAsync("aria-pressed"));

        // Act
        await page.FillAsync("[data-telephony-transfer-input]", "(702) 555-0199");
        await page.ClickAsync("[data-telephony-transfer-number]");

        // Assert
        var transfer = await WaitForTransferAsync(page);
        Assert.Equal("+17025550199", transfer.GetProperty("to").GetString());
        Assert.False(transfer.GetProperty("isExtension").GetBoolean());
    }

    [Fact]
    public async Task NumberMode_RefusesAnIncompleteNumber_AndPointsAtTheExtensionToggle()
    {
        // Arrange
        var page = await OpenOnACallAsync("?intlTelInput&styled");
        await page.ClickAsync("[data-telephony-transfer]");

        // Act - "2" typed without switching to an extension.
        await page.FillAsync("[data-telephony-transfer-input]", "2");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        var error = page.Locator("[data-telephony-transfer-error]");
        await error.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Contains("extension", await error.InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await TransferCountAsync(page));
    }

    [Fact]
    public async Task NumberMode_RefusesTheTenantsOwnNumber()
    {
        // Arrange - the phone's own outbound caller id comes from its registered media session.
        var page = await OpenAsync("?browserAudio=true&intlTelInput&styled", DesktopAppViewport);
        await page.EvaluateAsync(
            """
            (ownNumber) => window.telephonySoftPhone.getInstance().registerMediaAdapter('in-memory', function () {
                return { outboundCallerId: ownNumber, handleCallState: function () { }, dispose: function () { } };
            })
            """,
            OwnNumber);
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+17025550142");
        await page.ClickAsync("[data-telephony-transfer]");

        // Act
        await page.FillAsync("[data-telephony-transfer-input]", "(702) 555-0100");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        var error = page.Locator("[data-telephony-transfer-error]");
        await error.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Contains("own number", await error.InnerTextAsync());
        Assert.Equal(0, await TransferCountAsync(page));
    }

    [Fact]
    public async Task ExtensionMode_PutsTheFlagAway_AndTheToggleGoesBackToANumber_InTheDesktopWindow()
    {
        // Arrange
        var page = await OpenOnACallAsync("?intlTelInput&styled");
        await page.ClickAsync("[data-telephony-transfer]");
        var panel = page.Locator("[data-telephony-transfer-panel]");
        await panel.Locator(".iti__selected-country").WaitForAsync();

        // Act
        await SwitchToExtensionAsync(page);

        // Assert
        Assert.True(await panel.Locator(".iti__selected-country").IsHiddenAsync());
        // A plain search box: a colleague is found by name as well as by the extension's digits.
        Assert.Equal("text", await page.Locator("[data-telephony-transfer-input]").GetAttributeAsync("inputmode"));
        Assert.Equal("text", await page.Locator("[data-telephony-transfer-input]").GetAttributeAsync("type"));
        await AssertFitsTheWindowAsync(page, "[data-telephony-transfer-panel]");
        await CaptureAsync(page, "transfer-extension-mode");

        // Act - and back.
        await page.ClickAsync("[data-telephony-transfer-dial-mode]");

        // Assert
        Assert.Equal("false", await page.Locator("[data-telephony-transfer-dial-mode]").GetAttributeAsync("aria-pressed"));
        Assert.True(await panel.Locator(".iti__selected-country").IsVisibleAsync());
    }

    [Fact]
    public async Task PickingFromTheDirectory_WorksInExtensionMode()
    {
        // Arrange
        var page = await OpenOnACallAsync("?intlTelInput");
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"2002\"]").WaitForAsync();

        // Act
        await SwitchToExtensionAsync(page);
        await page.ClickAsync("[data-telephony-directory-destination=\"2002\"]");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert - the provider's own directory entry is sent as the provider named it.
        var transfer = await WaitForTransferAsync(page);
        Assert.Equal("2002", transfer.GetProperty("to").GetString());
        Assert.False(transfer.GetProperty("isExtension").GetBoolean());
    }

    [Theory]
    [InlineData("blind", "transfer")]
    [InlineData("warm", "consult-start")]
    public async Task OnAContactCenterCall_Extension2_IsSentToTheContactCenterAsAnExtension(string mode, string command)
    {
        // Arrange
        var page = await OpenOnAContactCenterCallAsync("?transferService&intlTelInput&styled");
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"agent:agent-bea\"]").WaitForAsync();
        await page.ClickAsync($"[data-telephony-transfer-mode=\"{mode}\"]");

        // Act - outside numbers are turned off for this tenant, but an extension is inside the phone system.
        await SwitchToExtensionAsync(page);
        await page.FillAsync("[data-telephony-transfer-input]", "2");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        var sent = await WaitForCommandAsync(page, command);
        Assert.Equal(InteractionId, sent.GetProperty("interactionId").GetString());
        Assert.Equal("extension", sent.GetProperty("targetType").GetString());
        Assert.Equal("2", sent.GetProperty("targetId").GetString());
        Assert.Equal(0, await TransferCountAsync(page));
    }

    [Fact]
    public async Task OnAContactCenterCall_PickingAnAgent_WorksInExtensionMode()
    {
        // Arrange
        var page = await OpenOnAContactCenterCallAsync("?transferService&intlTelInput");
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"agent:agent-bea\"]").WaitForAsync();

        // Act
        await SwitchToExtensionAsync(page);
        await page.FillAsync("[data-telephony-transfer-input]", "201");
        await page.ClickAsync("[data-telephony-directory-destination=\"agent:agent-bea\"]");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        var sent = await WaitForCommandAsync(page, "transfer");
        Assert.Equal("agent", sent.GetProperty("targetType").GetString());
        Assert.Equal("agent-bea", sent.GetProperty("targetId").GetString());
    }

    [Fact]
    public async Task ACallDialedStraightFromThisBrowser_SaysItCannotBeTransferred_AndSendsNothing()
    {
        // Arrange - on Telnyx a keypad call is placed by the browser's own SDK; the server has no handle on it.
        Server.Provider.BrowserMediaAdapterName = "telnyx-webrtc";
        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = DesktopAppViewport });
        await page.AddInitScriptAsync(FakeTelnyxSdk.Script);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true&styled");
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+17024993350");
        await page.ClickAsync("[data-telephony-dial]");
        await page.Locator("[data-telephony-transfer]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });

        // Act
        await page.ClickAsync("[data-telephony-transfer]");

        // Assert
        var blocked = page.Locator("[data-telephony-transfer-blocked=\"browser-call\"]");
        await blocked.WaitForAsync();
        Assert.Contains("cannot transfer", await blocked.InnerTextAsync());
        Assert.Equal(0, await page.Locator("[data-telephony-transfer-confirm]").CountAsync());
        await AssertFitsTheWindowAsync(page, "[data-telephony-transfer-panel]");
        await CaptureAsync(page, "transfer-browser-call");
        Assert.Equal(0, await TransferCountAsync(page));
    }

    private async Task<IPage> OpenOnACallAsync(string query)
    {
        var page = await OpenAsync(query, DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+17025550142");

        return page;
    }

    private async Task<IPage> OpenOnAContactCenterCallAsync(string query)
    {
        var page = await OpenAsync(query, DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");

        // A connected inbound call the Contact Center routed to this agent: it names its interaction.
        await page.EvaluateAsync(
            """
            (interactionId) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                {
                    callId: 'cc-call-1',
                    from: '+17024993350',
                    direction: 1,
                    state: 3,
                    providerName: 'InMemory',
                    metadata: { interactionId }
                })
            """,
            InteractionId);
        await page.Locator("[data-telephony-transfer]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        return page;
    }

    private static async Task SwitchToExtensionAsync(IPage page)
    {
        await page.ClickAsync("[data-telephony-transfer-dial-mode]");
        Assert.Equal("true", await page.Locator("[data-telephony-transfer-dial-mode]").GetAttributeAsync("aria-pressed"));
    }

    private static Task<int> TransferCountAsync(IPage page)
        => page.EvaluateAsync<int>("() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount')");

    private static async Task<JsonElement> WaitForTransferAsync(IPage page)
    {
        await WaitForPromiseAsync(
            page,
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount').then(value => value === 1)");

        return await page.EvaluateAsync<JsonElement>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetLastTransfer')");
    }

    private static async Task<JsonElement> WaitForCommandAsync(IPage page, string kind)
    {
        await WaitForPromiseAsync(
            page,
            "(kind) => fetch('/test/transfer-commands').then(response => response.json()).then(commands => commands.some(command => command.kind === kind))",
            kind);

        var commands = await page.EvaluateAsync<JsonElement>("() => fetch('/test/transfer-commands').then(response => response.json())");

        return commands.EnumerateArray().Last(command => command.GetProperty("kind").GetString() == kind);
    }

    private static async Task AssertFitsTheWindowAsync(IPage page, string selector)
    {
        var box = await page.Locator(selector).BoundingBoxAsync();

        Assert.NotNull(box);
        Assert.True(box.X >= 0 && box.X + box.Width <= DesktopAppViewport.Width, $"{selector} spans {box.X}..{box.X + box.Width}px, wider than the {DesktopAppViewport.Width}px window.");
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth"));
    }
}
