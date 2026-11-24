using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the soft phone's own Telnyx adapter over <see cref="FakeTelnyxSdk"/> through the leg the platform rings for a
/// dialer call, which the phone answers by itself and which the server reports by the customer's call, never by the leg.
/// </summary>
public sealed class SoftPhoneDialerLegTests : SoftPhoneBrowserTest
{
    // Regression: agents on Power campaigns answered, said hello, and the call dropped. A report that some other call had
    // ended -- the previous dial, or a history entry the server swept as orphaned -- fell through to the latest platform
    // leg and hung up the call the agent was on.
    [Fact]
    public async Task DialerLeg_SurvivesAnEndReportedForAnotherCall_AndEndsWithItsOwnCall()
    {
        // Arrange: the dialer's leg arrives and is answered, and the server reports the customer's call connected.
        var page = await OpenTelnyxPhoneAsync();
        await RingDialerLegAsync(page, "res-b", "agent-leg-b");
        await PublishTrackedCallStateAsync(page, "customer-b", 3);
        await page.WaitForFunctionAsync(
            "() => (document.querySelector('[data-telephony-status]').textContent || '').indexOf('In call') === 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Act: the server reports another call ended.
        await PublishCallStateAsync(page, "customer-a", 5);
        await page.WaitForTimeoutAsync(1000);

        // Assert: the agent is still on the call.
        Assert.Equal("active", await LegStateAsync(page, "agent-leg-b"));
        Assert.DoesNotContain("platform-leg-hangup", Server.Provider.ClientDiagnosticCodes);

        // Act: the customer's own call ends.
        await PublishTrackedCallStateAsync(page, "customer-b", 5);

        // Assert: its leg is hung up, and the phone says so.
        await page.WaitForFunctionAsync(
            "() => window.fakeTelnyx.byLeg('agent-leg-b').state === 'destroy'",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });
        await WaitForAsync(() => Server.Provider.ClientDiagnosticCodes.Contains("platform-leg-hangup"));
    }

    // Regression: the server lists a dialer call only once the agent is joined, so a refresh of the phone's calls in the
    // moments after the leg was answered came back empty, and an empty list hung up the leg. The microphone, turned off
    // when the previous call ended, also stayed off until the server's report of the join, so the customer heard nothing
    // of the agent's "hello".
    [Fact]
    public async Task DialerLeg_AnsweredBeforeTheServerReportsIt_StaysUp_AndSendsTheMicrophone()
    {
        // Arrange: a first dialer call comes and goes, which leaves the shared microphone off.
        var page = await OpenTelnyxPhoneAsync();
        await RingDialerLegAsync(page, "res-a", "agent-leg-a");
        await PublishTrackedCallStateAsync(page, "customer-a", 3);
        await PublishTrackedCallStateAsync(page, "customer-a", 5);
        await page.WaitForFunctionAsync(
            "() => window.fakeTelnyx.byLeg('agent-leg-a').state === 'destroy'",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // The next dialer leg is answered; the server has not reported its call yet.
        await RingDialerLegAsync(page, "res-b", "agent-leg-b");
        var hangupsBefore = HangupDiagnostics();

        // Act: a stale end for a call the phone never showed makes the phone refresh its calls, and the list is empty.
        await PublishCallStateAsync(page, "stale-entry", 5);
        await page.WaitForTimeoutAsync(1000);

        // Assert: the leg is still up, and the customer hears the agent.
        Assert.Equal("active", await LegStateAsync(page, "agent-leg-b"));
        await page.WaitForFunctionAsync(
            "() => window.fakeTelnyx.readSending('agent-leg-b').then(sending => sending.enabled && sending.trackState === 'live')",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000, PollingInterval = 200 });
        Assert.Equal(hangupsBefore, HangupDiagnostics());
    }

    // How many legs the phone has hung up on the server's word so far.
    private int HangupDiagnostics()
        => Server.Provider.ClientDiagnosticCodes.Count(code => code == "platform-leg-hangup");

    private async Task<IPage> OpenTelnyxPhoneAsync()
    {
        Server.Provider.BrowserMediaAdapterName = "telnyx-webrtc";

        var page = await Browser.NewPageAsync();
        await page.AddInitScriptAsync(FakeTelnyxSdk.Script);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true");
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.ready", null, new PageWaitForFunctionOptions { Timeout = 5000 });

        return page;
    }

    // The dialer's reservation for this agent arms the phone, as the Contact Center layer does when the dial is placed,
    // and the platform rings the agent's leg, which the phone answers without ringing.
    private static async Task RingDialerLegAsync(IPage page, string reservationId, string legId)
    {
        await page.EvaluateAsync(
            "reservationId => window.telephonySoftPhone.getInstance().armInboundAutoAnswer(reservationId, 'agent-user', 'agent-user')",
            reservationId);
        await page.EvaluateAsync("legId => window.fakeTelnyx.ringLeg(legId)", legId);
        await page.WaitForFunctionAsync(
            "legId => window.fakeTelnyx.byLeg(legId).state === 'active'",
            legId,
            new PageWaitForFunctionOptions { Timeout = 5000 });
    }

    // A call the server tracks: it is listed when the phone refreshes its calls, until it ends.
    private static async Task PublishTrackedCallStateAsync(IPage page, string callId, int state)
        => await page.EvaluateAsync(
            """
            ([callId, state]) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishTrackedCallState',
                { callId, from: '+15550001000', direction: 0, state, providerName: 'InMemory' })
            """,
            new object[] { callId, state });

    // A report about a call the server does not list.
    private static async Task PublishCallStateAsync(IPage page, string callId, int state)
        => await page.EvaluateAsync(
            """
            ([callId, state]) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                { callId, from: '+15550001000', direction: 0, state, providerName: 'InMemory' })
            """,
            new object[] { callId, state });

    private static Task<string> LegStateAsync(IPage page, string legId)
        => page.EvaluateAsync<string>("legId => (window.fakeTelnyx.byLeg(legId) || { state: 'none' }).state", legId);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(100);
        }

        Assert.True(condition());
    }
}
