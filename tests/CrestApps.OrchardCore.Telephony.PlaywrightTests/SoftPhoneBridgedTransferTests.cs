using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Transferring a number dialed from the keypad on a provider that connects such calls itself (Telnyx), and taking one
/// over from a colleague, driven through the soft phone's own Telnyx adapter over <see cref="FakeTelnyxSdk"/>.
/// </summary>
/// <remarks>
/// A warm transfer of such a call was refused with "A warm transfer is not available for a number dialed from the soft
/// phone", and a colleague handed one blind could not transfer it again: their phone treated it as a call it had dialed
/// itself. Now the caller is held on this call's own hold tone while the destination is rung, the transfer panel follows
/// the transfer as a consult, and a colleague's phone rings the call with Answer and Decline and then treats it as a call
/// the platform tracks.
/// </remarks>
public sealed class SoftPhoneBridgedTransferTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task WarmTransferOfAKeypadCallToExtension2_HoldsTheCaller_ConsultsOnACallOfItsOwn_AndCompletes()
    {
        // Arrange
        var page = await OpenTelnyxPhoneAsync();
        var callId = await DialAsync(page, "+17025550101");

        // Act
        await StartTransferAsync(page, warm: true, extension: "2");

        // Assert - the caller is held before anything else, then the transfer names this phone for the consult.
        await WaitForAsync(() => Server.Provider.GetTransferRequestCount() == 1);
        Assert.Equal($"Hold:{callId}", Server.Provider.HoldCommands.First());
        var transfer = Server.Provider.GetLastTransfer();
        Assert.Equal(callId, transfer.CallId);
        Assert.Equal("2", transfer.To);
        Assert.True(transfer.IsExtension);
        Assert.Equal(TransferMode.Warm, transfer.Mode);
        Assert.Equal("fake-credential-1", transfer.Metadata[TelephonyConstants.RequestMetadata.SoftPhoneCredentialId]);

        var consult = page.Locator("[data-telephony-consult]");
        await consult.WaitForAsync();
        Assert.Contains("Calling 2", await page.Locator("[data-telephony-consult-status]").InnerTextAsync());
        Assert.True(await page.Locator("[data-telephony-consult-complete]").IsDisabledAsync());

        // The consult rings this phone back, and the phone answers its own leg without ringing.
        var consultId = Server.Provider.LastConsultId;
        await page.EvaluateAsync("legId => window.fakeTelnyx.ringLeg(legId)", consultId);
        await page.WaitForFunctionAsync("legId => window.fakeTelnyx.byLeg(legId).answers === 1", consultId);
        Assert.True(await page.Locator("[data-telephony-incoming]").IsHiddenAsync());

        // The colleague answers.
        Server.Provider.SetConsultStatus(consultId, TelephonyConstants.ConsultStatuses.Connected);
        await page.WaitForFunctionAsync("() => /Talking to 2/.test(document.querySelector('[data-telephony-consult-status]').textContent)");
        Assert.True(await page.Locator("[data-telephony-consult-complete]").IsEnabledAsync());
        await CaptureAsync(page, "bridged-warm-transfer-consult");

        await page.ClickAsync("[data-telephony-consult-complete]");
        await WaitForAsync(() => Server.Provider.ConsultCommands.Contains($"CompleteConsult:{callId}:{consultId}"));
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-transfer-panel]').hidden");
    }

    [Fact]
    public async Task CancellingAWarmTransfer_HangsUpTheConsult_AndTakesTheCallerOffHold()
    {
        // Arrange
        var page = await OpenTelnyxPhoneAsync();
        var callId = await DialAsync(page, "+17025550101");
        await StartTransferAsync(page, warm: true, extension: "2");
        await page.Locator("[data-telephony-consult]").WaitForAsync();
        var consultId = Server.Provider.LastConsultId;

        // Act
        await page.ClickAsync("[data-telephony-consult-cancel]");

        // Assert
        await WaitForAsync(() => Server.Provider.ConsultCommands.Contains($"CancelConsult:{callId}:{consultId}"));
        await WaitForAsync(() => Server.Provider.HoldCommands.Contains($"Resume:{callId}"));
        await page.WaitForFunctionAsync(
            "callId => (window.telephonySoftPhone.getInstance().getActiveCalls().find(call => call.callId === callId) || {}).isOnHold === false",
            callId);
    }

    [Fact]
    public async Task BlindTransferOfAKeypadCall_IsFollowedUntilTheDestinationAnswers_AndTheCallerStaysHeldIfNobodyDoes()
    {
        // Arrange
        var page = await OpenTelnyxPhoneAsync();
        var callId = await DialAsync(page, "+17025550101");

        // Act
        await StartTransferAsync(page, warm: false, extension: "2");

        // Assert - a blind transfer completes itself when the destination answers; the agent can only call it off.
        await WaitForAsync(() => Server.Provider.GetTransferRequestCount() == 1);
        Assert.Equal($"Hold:{callId}", Server.Provider.HoldCommands.First());
        Assert.Equal(TransferMode.Blind, Server.Provider.GetLastTransfer().Mode);
        await page.Locator("[data-telephony-consult]").WaitForAsync();
        Assert.True(await page.Locator("[data-telephony-consult-complete]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-consult-cancel]").IsVisibleAsync());
        await CaptureAsync(page, "bridged-blind-transfer-ringing");

        Server.Provider.SetConsultStatus(Server.Provider.LastConsultId, TelephonyConstants.ConsultStatuses.Cancelled);

        await page.WaitForFunctionAsync("() => /did not take the call/.test(document.querySelector('[data-telephony-consult-status]').textContent)");
        Assert.True(await page.Locator("[data-telephony-consult-done]").IsVisibleAsync());
        Assert.True(await page.EvaluateAsync<bool>(
            "callId => (window.telephonySoftPhone.getInstance().getActiveCalls().find(call => call.callId === callId) || {}).isOnHold === true",
            callId));
    }

    [Fact]
    public async Task ACallAColleagueHandsOver_RingsWithAnswerAndDecline_AndCanBeTransferredAgain()
    {
        // Arrange - the platform records the call in this user's history and rings their browser on the transfer leg.
        var page = await OpenTelnyxPhoneAsync();
        Server.Provider.TrackCall(new TelephonyCall
        {
            CallId = "xfer-9",
            From = "+17025550101",
            Direction = CallDirection.Inbound,
            State = CallState.Ringing,
            ProviderName = "InMemory",
            StartedUtc = DateTimeOffset.UtcNow,
        });

        // Act
        await page.EvaluateAsync("() => window.fakeTelnyx.ringTransferLeg('xfer-9', '+17025550101', 'Agent One')");

        // Assert - it rings, showing the caller (not the platform's own number) and who handed it over.
        var incoming = page.Locator("[data-telephony-incoming]");
        await incoming.WaitForAsync();
        Assert.Contains("7025550101", await page.Locator("[data-telephony-incoming-caller]").InnerTextAsync());
        Assert.Contains("Transferred by Agent One", await page.Locator("[data-telephony-incoming-queue]").InnerTextAsync());
        Assert.True(await page.Locator("[data-telephony-incoming-voicemail]").IsHiddenAsync());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.fakeTelnyx.byLeg('xfer-9').answers"));
        await CaptureAsync(page, "handed-over-call-ringing");

        await page.ClickAsync("[data-telephony-incoming-answer]");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('xfer-9').answers === 1");
        await page.EvaluateAsync(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('PublishTrackedCallState', { callId: 'xfer-9', from: '+17025550101', direction: 1, state: 3, providerName: 'InMemory' })");
        await page.WaitForFunctionAsync(
            "() => ['Connected', 3].includes((window.telephonySoftPhone.getInstance().getActiveCalls().find(call => call.callId === 'xfer-9') || {}).state)");

        // A call the platform tracks, not one this browser placed: it can be transferred again.
        Assert.False(await page.EvaluateAsync<bool>(
            "() => !!window.telephonySoftPhone.getInstance().getActiveCalls().find(call => call.callId === 'xfer-9').browserOriginated"));

        await StartTransferAsync(page, warm: false, extension: "3");
        await WaitForAsync(() => Server.Provider.GetTransferRequestCount() == 1);
        Assert.Equal(0, await page.Locator("[data-telephony-transfer-blocked]:visible").CountAsync());
        Assert.Equal("xfer-9", Server.Provider.GetLastTransfer().CallId);
        Assert.Equal("3", Server.Provider.GetLastTransfer().To);
        Assert.Contains("Hold:xfer-9", Server.Provider.HoldCommands);
    }

    [Fact]
    public async Task DecliningACallAColleagueHandsOver_HangsUpItsLeg()
    {
        // Arrange
        var page = await OpenTelnyxPhoneAsync();
        Server.Provider.TrackCall(new TelephonyCall
        {
            CallId = "xfer-9",
            From = "+17025550101",
            Direction = CallDirection.Inbound,
            State = CallState.Ringing,
            ProviderName = "InMemory",
            StartedUtc = DateTimeOffset.UtcNow,
        });
        await page.EvaluateAsync("() => window.fakeTelnyx.ringTransferLeg('xfer-9', '+17025550101', 'Agent One')");
        await page.Locator("[data-telephony-incoming]").WaitForAsync();

        // Act
        await page.ClickAsync("[data-telephony-incoming-ignore]");

        // Assert - the platform hears the decline as the transfer not answered, and gives the caller back.
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('xfer-9').state === 'destroy'");
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.fakeTelnyx.byLeg('xfer-9').answers"));
    }

    private async Task<IPage> OpenTelnyxPhoneAsync()
    {
        Server.Provider.EnableBridgedDial();
        Server.Provider.EnableAttendedTransfer();
        Server.Provider.EnableConsultTransfer();
        Server.Provider.BrowserMediaAdapterName = "telnyx-webrtc";

        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = DesktopAppViewport });
        await page.AddInitScriptAsync(FakeTelnyxSdk.Script);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true&styled&attendedTransfer");
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
        await page.WaitForFunctionAsync(
            "callId => ['Connected', 3].includes((window.telephonySoftPhone.getInstance().getActiveCalls().find(call => call.callId === callId) || {}).state)",
            callId);

        return callId;
    }

    private static async Task StartTransferAsync(IPage page, bool warm, string extension)
    {
        await page.ClickAsync("[data-telephony-transfer]");

        if (warm)
        {
            await page.ClickAsync("[data-telephony-transfer-mode=\"warm\"]");
        }

        await page.ClickAsync("[data-telephony-transfer-dial-mode]");
        await page.FillAsync("[data-telephony-transfer-input]", extension);
        await page.ClickAsync("[data-telephony-transfer-confirm]");
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
