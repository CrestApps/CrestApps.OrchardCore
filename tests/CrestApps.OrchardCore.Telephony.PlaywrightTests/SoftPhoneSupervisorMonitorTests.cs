using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// A supervisor listening to a call on their own soft phone, driven through the phone's real Telnyx adapter over
/// <see cref="FakeTelnyxSdk"/>: the phone answers the one monitor leg it was told to expect, by itself, and shows it as
/// an engagement banner -- who is being monitored, Listen / Whisper / Barge, Stop -- never as a call of its own. Any
/// other leg, including a monitor leg it did not ask for, is left exactly as it was.
/// </summary>
public sealed class SoftPhoneSupervisorMonitorTests : SoftPhoneBrowserTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheMonitorLegItWasToldToExpect_IsAnsweredByItself_AndIsNeverACallOfItsOwn(bool headerOnly)
    {
        // Arrange
        var page = await OpenSupervisorPhoneAsync();
        await RequestAsync(page, "tok-1", "Ann Agent");

        // Act
        await page.EvaluateAsync("([leg, token, headerOnly]) => window.fakeTelnyx.ringMonitorLeg(leg, token, headerOnly)", new object[] { "sv-1", "tok-1", headerOnly });

        // Assert
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-1').state === 'active'");
        Assert.Equal(1, await AnswersAsync(page, "sv-1"));
        Assert.False(await page.Locator("[data-telephony-incoming-answer]").IsVisibleAsync());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.telephonySoftPhone.getInstance().getActiveCalls().length"));
        Assert.Null(await page.EvaluateAsync<string>("() => { var call = window.telephonySoftPhone.getInstance().getCurrentCall(); return call ? call.callId : null; }"));

        var banner = page.Locator("[data-cc-monitor-banner]");
        await banner.Locator("[data-cc-monitor-title]", new LocatorLocatorOptions { HasText = "Ann Agent" }).WaitForAsync();
        Assert.True(await banner.IsVisibleAsync());
        await CaptureAsync(page, "supervisor-phone-connecting");
    }

    [Fact]
    public async Task AMonitorLegNobodyAskedFor_IsHungUp_WithoutRinging()
    {
        // Arrange - the phone expects one engagement's leg, and a leg for another arrives.
        var page = await OpenSupervisorPhoneAsync();
        await RequestAsync(page, "tok-mine", "Ann Agent");

        // Act
        await page.EvaluateAsync("() => window.fakeTelnyx.ringMonitorLeg('sv-other', 'tok-someone-else')");

        // Assert - it waits a moment for an arm that might still be on its way, then lets the leg go.
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-other').state === 'destroy'", null, new PageWaitForFunctionOptions { Timeout = 8000 });
        Assert.Equal(0, await AnswersAsync(page, "sv-other"));
        Assert.False(await page.Locator("[data-telephony-incoming-answer]").IsVisibleAsync());
    }

    [Fact]
    public async Task AMonitorLegThatArrivesBeforeItsArm_IsStillAnswered()
    {
        // Arrange
        var page = await OpenSupervisorPhoneAsync();

        // Act - the provider's invite beats the real-time message by a moment.
        await page.EvaluateAsync("() => window.fakeTelnyx.ringMonitorLeg('sv-early', 'tok-early')");
        await page.WaitForTimeoutAsync(400);
        await RequestAsync(page, "tok-early", "Ann Agent");

        // Assert
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-early').answers === 1");
    }

    [Fact]
    public async Task WhileAnEngagementIsExpected_AColleaguesCallStillRings()
    {
        // Arrange
        var page = await OpenSupervisorPhoneAsync();
        await RequestAsync(page, "tok-1", "Ann Agent");

        // Act
        await page.EvaluateAsync("() => window.fakeTelnyx.ringColleagueLeg('ext-in-1')");

        // Assert
        await page.Locator("[data-telephony-incoming]").WaitForAsync();
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(0, await AnswersAsync(page, "ext-in-1"));

        // And the engagement's own leg is still answered when it comes.
        await page.EvaluateAsync("() => window.fakeTelnyx.ringMonitorLeg('sv-1', 'tok-1')");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-1').answers === 1");
    }

    [Fact]
    public async Task TheBanner_SwitchesModeOnTheSameLeg_AndStopHangsUpOnlyTheMonitorLeg()
    {
        // Arrange - connected and listening.
        var page = await OpenSupervisorPhoneAsync();
        await RequestAsync(page, "tok-1", "Ann Agent");
        await page.EvaluateAsync("() => window.fakeTelnyx.ringMonitorLeg('sv-1', 'tok-1')");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-1').state === 'active'");
        await EmitAsync(page, "Connected", "Monitor");
        var whisper = page.Locator("[data-cc-monitor-mode='Whisper']");
        await whisper.WaitForAsync();
        Assert.Equal("true", await page.Locator("[data-cc-monitor-mode='Monitor']").GetAttributeAsync("aria-checked"));
        await CaptureAsync(page, "supervisor-phone-listening");

        // Act - Whisper, then Stop.
        await whisper.ClickAsync();
        await page.WaitForFunctionAsync("() => window.fakeCcHub.invocations.some(call => call.method === 'SwitchMonitorMode')");
        var switched = await page.EvaluateAsync<int[]>("() => window.fakeCcHub.invocations.filter(call => call.method === 'SwitchMonitorMode').map(call => call.args[1])");
        await EmitAsync(page, "ModeChanged", "Whisper");
        await page.WaitForFunctionAsync("() => document.querySelector(\"[data-cc-monitor-mode='Whisper']\").getAttribute('aria-checked') === 'true'");

        await page.ClickAsync("[data-cc-monitor-stop]");
        await page.WaitForFunctionAsync("() => window.fakeCcHub.invocations.some(call => call.method === 'StopMonitoring')");

        // Assert - the mode changed on the same leg (Whisper is 1), the supervisor was never rung again, and Stop let only
        // the monitor leg go.
        Assert.Equal([1], switched);
        Assert.Equal(1, await AnswersAsync(page, "sv-1"));
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-1').state === 'destroy'");
        await page.Locator("[data-cc-monitor-banner]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }

    [Fact]
    public async Task AfterATakeover_TheBannerOffersHangUp_AndNoModeSwitcher()
    {
        // Arrange
        var page = await OpenSupervisorPhoneAsync();
        await RequestAsync(page, "tok-1", "Ann Agent");
        await page.EvaluateAsync("() => window.fakeTelnyx.ringMonitorLeg('sv-1', 'tok-1')");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-1').state === 'active'");

        // Act
        await EmitAsync(page, "TookOver", "Barge");

        // Assert
        await page.Locator("[data-cc-monitor-title]", new PageLocatorOptions { HasText = "took over" }).WaitForAsync();
        Assert.Equal(0, await page.Locator("[data-cc-monitor-mode]").CountAsync());
        await CaptureAsync(page, "supervisor-phone-took-over");

        await page.ClickAsync("[data-cc-monitor-stop]");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-1').state === 'destroy'");
        Assert.False(await page.EvaluateAsync<bool>("() => window.fakeCcHub.invocations.some(call => call.method === 'StopMonitoring')"));
    }

    [Fact]
    public async Task TheSupervisorsMicrophone_FollowsTheMode_OffWhileListening_OnWhileCoachingOrJoining_AndAfterATakeover()
    {
        // Arrange - live, the phone kept its microphone off on the monitor leg whatever the mode: nobody heard a
        // supervisor who joined the call, or took it over.
        var page = await OpenSupervisorPhoneAsync();
        await RequestAsync(page, "tok-1", "Ann Agent");
        await page.EvaluateAsync("() => window.fakeTelnyx.ringMonitorLeg('sv-1', 'tok-1')");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-1').state === 'active'");
        await EmitAsync(page, "Connected", "Monitor");

        // Act & Assert
        Assert.True(await SendsAsync(page, "sv-1", expected: false), "The supervisor is heard while listening.");

        await EmitAsync(page, "ModeChanged", "Whisper");
        Assert.True(await SendsAsync(page, "sv-1", expected: true), "The agent cannot hear a coaching supervisor.");

        await EmitAsync(page, "ModeChanged", "Monitor");
        Assert.True(await SendsAsync(page, "sv-1", expected: false), "The supervisor is still heard after going back to listening.");

        await EmitAsync(page, "ModeChanged", "Barge");
        Assert.True(await SendsAsync(page, "sv-1", expected: true), "Nobody hears a supervisor who joined the call.");

        await EmitAsync(page, "ModeChanged", "Monitor");
        await EmitAsync(page, "TookOver", "Barge");
        Assert.True(await SendsAsync(page, "sv-1", expected: true), "Nobody hears a supervisor who took the call over.");
    }

    [Fact]
    public async Task AnEngagementStartedAsAJoin_IsAnsweredWithTheMicrophoneOn()
    {
        // Arrange
        var page = await OpenSupervisorPhoneAsync();
        await RequestAsync(page, "tok-1", "Ann Agent", "Barge");

        // Act
        await page.EvaluateAsync("() => window.fakeTelnyx.ringMonitorLeg('sv-1', 'tok-1')");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('sv-1').state === 'active'");

        // Assert
        Assert.True(await SendsAsync(page, "sv-1", expected: true), "Nobody hears a supervisor who joined the call.");
    }

    // Whether the supervisor's leg comes to send live (or silent) audio within a few seconds.
    private static Task<bool> SendsAsync(IPage page, string leg, bool expected)
        => page.EvaluateAsync<bool>(
            """
            async ([leg, expected]) => {
                for (let attempt = 0; attempt < 40; attempt++) {
                    if ((await window.fakeTelnyx.readSending(leg)).enabled === expected) {
                        return true;
                    }

                    await new Promise(resolve => setTimeout(resolve, 100));
                }

                return false;
            }
            """,
            new object[] { leg, expected });

    private async Task<IPage> OpenSupervisorPhoneAsync()
    {
        Server.Provider.BrowserMediaAdapterName = "telnyx-webrtc";
        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = DesktopAppViewport });
        await page.AddInitScriptAsync(FakeTelnyxSdk.Script);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true&styled&" + SupervisorHarness.SupervisorPhoneQueryKey);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.ready", null, new PageWaitForFunctionOptions { Timeout = 5000 });

        // The banner script wires itself once the phone is up.
        await page.WaitForFunctionAsync("() => !!window.fakeCcHub && !!document.querySelector('[data-cc-monitor-banner]').__ccMonitorBound");

        return page;
    }

    // The server telling the supervisor's phone to expect a leg: what ContactCenterMonitoringService sends before ringing it.
    private static async Task RequestAsync(IPage page, string token, string agentName, string mode = "Monitor")
        => await page.EvaluateAsync(
            "([token, agentName, mode]) => window.fakeCcHub.emit('SupervisorEngagementChanged', { state: 'Requested', interactionId: 'int-1', supervisorUserId: 'sup-1', agentName: agentName, mode: mode, monitorToken: token })",
            new[] { token, agentName, mode });

    private static async Task EmitAsync(IPage page, string state, string mode)
        => await page.EvaluateAsync(
            "([state, mode]) => window.fakeCcHub.emit('SupervisorEngagementChanged', { state: state, interactionId: 'int-1', supervisorUserId: 'sup-1', mode: mode })",
            new[] { state, mode });

    private static Task<int> AnswersAsync(IPage page, string leg)
        => page.EvaluateAsync<int>("leg => (window.fakeTelnyx.byLeg(leg) || { answers: -1 }).answers", leg);
}
