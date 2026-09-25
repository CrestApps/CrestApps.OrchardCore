using System.Text.Json;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Numbers dialed on the keypad of a provider that connects them itself (Telnyx), driven through the soft phone's own
/// Telnyx adapter over <see cref="FakeTelnyxSdk"/>.
/// </summary>
/// <remarks>
/// The browser used to dial these itself, so the platform never had the call: an agent with two or three of them up
/// could neither transfer one to a colleague nor merge them. Now the phone asks the platform to dial, naming the
/// credential it is registered on; the platform rings that credential and the phone answers its own leg without ringing,
/// exactly as for an extension call. Only when the platform says it cannot does the phone dial the number itself -- and
/// such a call still says it cannot be transferred or merged.
/// </remarks>
public sealed class SoftPhoneBridgedDialTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task KeypadCall_IsPlacedByThePlatform_AndThePhoneAnswersItsOwnLeg_WithTheUsualInCallControls()
    {
        // Arrange
        var page = await OpenTelnyxPhoneAsync();

        // Act
        var callId = await DialAsync(page, "+17025550101");

        // Assert - the platform was asked to dial, naming this phone; the browser dialed nothing itself, and the leg the
        // platform rang back was answered without an incoming-call prompt.
        var dial = Server.Provider.GetLastDial();
        Assert.Equal("+17025550101", dial.To);
        Assert.Equal("fake-credential-1", dial.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneCredentialId]);
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.fakeTelnyx.placedCount()"));
        Assert.Equal(1, await LegAsync<int>(page, callId, "answers"));
        Assert.True(await page.Locator("[data-telephony-incoming]").IsHiddenAsync());

        // The in-call view is the usual one: the number, the running time, and hold, mute and hang-up.
        Assert.Contains("555-0101", await page.Locator("[data-telephony-number]").InputValueAsync());
        await page.WaitForFunctionAsync("() => /\\d+:\\d{2}/.test(document.querySelector('[data-telephony-status]').textContent)");
        Assert.True(await page.Locator("[data-telephony-hold]").IsVisibleAsync());
        Assert.True(await page.Locator("[data-telephony-mute]").IsVisibleAsync());
        Assert.True(await page.Locator("[data-telephony-hangup]").IsVisibleAsync());
        await CaptureAsync(page, "bridged-dial-in-call");

        // Digits go to the platform's call, which sends them to the dialed party.
        await page.EvaluateAsync("() => window.telephonySoftPhone.getInstance().pressKey('5')");
        await WaitForAsync(() => Server.Provider.GetLastDigits()?.CallId == callId);
        Assert.Equal("5", Server.Provider.GetLastDigits().Digits);

        // Hanging up ends the platform's call, and its end takes the phone's own leg down with it.
        await page.ClickAsync("[data-telephony-hangup]");
        await WaitForAsync(() => Server.Provider.GetHangupRequestCount() == 1);
        await page.EvaluateAsync("() => window.telephonySoftPhone.getInstance().getConnection().invoke('DisconnectLatestCall')");
        await page.WaitForFunctionAsync("() => window.telephonySoftPhone.getInstance().getActiveCalls().length === 0");
        await page.WaitForFunctionAsync("legId => window.fakeTelnyx.byLeg(legId).state === 'destroy'", callId);
    }

    [Fact]
    public async Task KeypadCall_CanBeTransferredToExtension2()
    {
        // Arrange
        var page = await OpenTelnyxPhoneAsync();
        var callId = await DialAsync(page, "+17025550101");

        // Act
        await page.ClickAsync("[data-telephony-transfer]");
        await page.ClickAsync("[data-telephony-transfer-dial-mode]");
        await page.FillAsync("[data-telephony-transfer-input]", "2");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        await WaitForAsync(() => Server.Provider.GetTransferRequestCount() == 1);
        var transfer = Server.Provider.GetLastTransfer();
        Assert.Equal(callId, transfer.CallId);
        Assert.Equal("2", transfer.To);
        Assert.True(transfer.IsExtension);
        Assert.Equal(0, await page.Locator("[data-telephony-transfer-blocked]:visible").CountAsync());
    }

    [Fact]
    public async Task ThreeKeypadCalls_MergeIntoOneConference_WithEveryLegTakenOffHold()
    {
        // Arrange - three numbers dialed, the first two held in turn to dial the next.
        var page = await OpenTelnyxPhoneAsync();
        var callIds = new List<string> { await DialAsync(page, "+17025550101") };
        var microphone = await SenderTrackAsync(page, callIds[0]);
        await HoldAsync(page, callIds[0]);
        callIds.Add(await DialAsync(page, "+17025550102"));
        await HoldAsync(page, callIds[1]);
        callIds.Add(await DialAsync(page, "+17025550103"));

        // Each hold reached the leg of the call it was for: the two held legs send the hold tone, the third the agent.
        Assert.NotEqual(microphone, await SenderTrackAsync(page, callIds[0]));
        Assert.NotEqual(microphone, await SenderTrackAsync(page, callIds[1]));
        Assert.Equal(microphone, await SenderTrackAsync(page, callIds[2]));

        // Act
        await page.Locator("[data-telephony-merge-select-all]").CheckAsync();
        Assert.Contains("Merge 3 calls", await page.Locator("[data-telephony-merge-calls]").InnerTextAsync());
        await page.ClickAsync("[data-telephony-merge-calls]");

        // Assert - one merge of all three, one conference on screen, and the agent heard on every leg again.
        await WaitForAsync(() => Server.Provider.GetMergeRequestCount() == 1);
        Assert.Equal(callIds.Order(), Server.Provider.GetLastMerge().GetCallIds().Order());
        await page.Locator("[data-telephony-conference]").WaitForAsync();
        Assert.Equal(3, await page.Locator("[data-telephony-conference-participant]").CountAsync());

        foreach (var callId in callIds)
        {
            await WaitForSenderTrackAsync(page, callId, microphone);
        }

        await CaptureAsync(page, "bridged-dial-conference-of-three");
    }

    [Fact]
    public async Task KeypadCall_ThePlatformCannotConnect_IsDialedFromTheBrowser_AndStillSaysItCannotBeTransferredOrMerged()
    {
        // Arrange
        Server.Provider.EnableBridgedDial(unavailable: true);
        var page = await OpenTelnyxPhoneAsync(enableBridgedDial: false);

        // Act
        await page.FillAsync("[data-telephony-number]", "+17025550101");
        await page.ClickAsync("[data-telephony-dial]");

        // Assert - asked first, then dialed here, once, with no error left on screen.
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.placedCount() === 1", null, new PageWaitForFunctionOptions { Timeout = 5000 });
        Assert.Equal(1, Server.Provider.GetDialRequestCount());
        await page.WaitForFunctionAsync(
            "() => window.telephonySoftPhone.getInstance().getActiveCalls().some(call => call.browserOriginated && call.state === 'Connected')",
            null,
            new PageWaitForFunctionOptions { Timeout = 10000 });
        Assert.Equal(string.Empty, (await page.Locator("[data-telephony-error]").TextContentAsync() ?? string.Empty).Trim());
        await WaitForAsync(() => Server.Provider.ClientDiagnosticCodes.Contains("bridged-dial-unavailable"));

        // Such a call has no call the platform can act on, and the phone still says so.
        await page.ClickAsync("[data-telephony-transfer]");
        var blocked = page.Locator("[data-telephony-transfer-blocked]");
        await blocked.WaitForAsync();
        Assert.Contains("cannot transfer it", await blocked.InnerTextAsync());
        Assert.Equal(0, Server.Provider.GetTransferRequestCount());
    }

    private async Task<IPage> OpenTelnyxPhoneAsync(bool enableBridgedDial = true)
    {
        if (enableBridgedDial)
        {
            Server.Provider.EnableBridgedDial();
        }

        Server.Provider.BrowserMediaAdapterName = "telnyx-webrtc";

        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = DesktopAppViewport });
        await page.AddInitScriptAsync(FakeTelnyxSdk.Script);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true&styled");
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.ready", null, new PageWaitForFunctionOptions { Timeout = 5000 });

        return page;
    }

    // Dials from the keypad, rings the leg the platform places back to this browser for it, has the platform report the
    // call connected, and returns the call's id.
    private async Task<string> DialAsync(IPage page, string number)
    {
        var dials = Server.Provider.GetDialRequestCount();

        await page.FillAsync("[data-telephony-number]", number);
        await page.ClickAsync("[data-telephony-dial]");
        await WaitForAsync(() => Server.Provider.GetDialRequestCount() == dials + 1 && Server.Provider.GetLatestCall()?.To == number);

        var callId = Server.Provider.GetLatestCall().CallId;

        await page.EvaluateAsync("legId => window.fakeTelnyx.ringLeg(legId)", callId);
        await page.WaitForFunctionAsync(
            "legId => window.fakeTelnyx.byLeg(legId).state === 'active'",
            callId,
            new PageWaitForFunctionOptions { Timeout = 5000 });
        await PublishLatestCallStateAsync(page);
        // The hub may send the state by number or by name.
        await page.WaitForFunctionAsync(
            "callId => ['Connected', 3].includes((window.telephonySoftPhone.getInstance().getActiveCalls().find(call => call.callId === callId) || {}).state)",
            callId);

        return callId;
    }

    private static async Task HoldAsync(IPage page, string callId)
    {
        await page.ClickAsync("[data-telephony-hold]");
        await page.WaitForFunctionAsync(
            "callId => (window.telephonySoftPhone.getInstance().getActiveCalls().find(call => call.callId === callId) || {}).isOnHold === true",
            callId);
    }

    private static Task<T> LegAsync<T>(IPage page, string legId, string property)
        => page.EvaluateAsync<T>("([legId, property]) => window.fakeTelnyx.byLeg(legId)[property]", new[] { legId, property });

    private static async Task<string> SenderTrackAsync(IPage page, string legId)
    {
        // A hold swaps the sender's track asynchronously; give it a moment to settle.
        await page.WaitForTimeoutAsync(300);

        var sending = await page.EvaluateAsync<JsonElement>("legId => window.fakeTelnyx.readSending(legId)", legId);

        return sending.GetProperty("trackId").GetString();
    }

    private static async Task WaitForSenderTrackAsync(IPage page, string legId, string trackId)
    {
        var matched = await page.EvaluateAsync<bool>(
            """
            async ([legId, trackId]) => {
                const deadline = Date.now() + 5000;

                while (Date.now() < deadline) {
                    if ((await window.fakeTelnyx.readSending(legId)).trackId === trackId) {
                        return true;
                    }

                    await new Promise(resolve => setTimeout(resolve, 100));
                }

                return false;
            }
            """,
            new[] { legId, trackId });

        Assert.True(matched, $"The leg {legId} never sent the agent's microphone again.");
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
