using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the real soft phone bundle through a Contact Center offer whose provider leg rang the browser while the offer
/// was still on screen, to prove the phone never takes that call without the agent accepting it.
/// </summary>
public sealed class SoftPhoneOfferLegTests : SoftPhoneBrowserTest
{
    // Stands in for the offer's accept endpoint and for the provider media client. The adapter keeps the context the
    // phone hands it, so a test can ring the browser with an offer's leg the way the provider SDK does.
    private const string InitScript =
        """
        window.offerTest = { accepts: 0, context: null, legs: {} };
        var realFetch = window.fetch.bind(window);
        window.fetch = function (url, init) {
            if (String(url).indexOf('/accept') !== 0) {
                return realFetch(url, init);
            }

            window.offerTest.accepts++;
            return Promise.resolve({ ok: true, json: function () { return Promise.resolve({ succeeded: true, requiresDeviceAnswer: false }); } });
        };
        """;

    // Stands in for the desktop app that hosts the /softphone page in WebView2: records what the page posts to it and
    // lets a test post the host's own messages back.
    private const string HostInitScript =
        """
        window.hostTest = { posted: [], listener: null };
        window.chrome = window.chrome || {};
        window.chrome.webview = {
            postMessage: function (message) { window.hostTest.posted.push(message); },
            addEventListener: function (type, listener) { if (type === 'message') { window.hostTest.listener = listener; } }
        };
        """;

    private const string RegisterAdapterScript =
        """
        () => window.telephonySoftPhone.getInstance().registerMediaAdapter('in-memory', function (context) {
            window.offerTest.context = context;
            return { handleCallState: function () { }, dispose: function () { } };
        })
        """;

    // Bug: after the agent had taken a call, the next offer's leg -- rung to the browser and held while the offer was
    // on screen -- turned into a call in progress ten seconds in, with nobody touching anything. The server reported
    // the caller's leg (live the whole time, answered by the platform for the automated assistant and the hold music)
    // as connected; the phone took that as the call connecting, dropped the incoming-call prompt and showed the call
    // with its in-call buttons. No accept was ever made, so the server expired the offer and sent the caller on while
    // the phone still showed them on the line.
    [Fact]
    public async Task NextOffer_AfterAnAcceptedCall_IsNeverTakenWithoutAnAccept()
    {
        // Arrange: the agent takes a call and it ends. The accept was also confirmed from the server, the way the
        // Contact Center layer does when an offer is accepted anywhere.
        var page = await OpenPhoneAsync();
        await ShowOfferAsync(page, "call-one", "res-one", expiresInMs: 30000);
        await page.ClickAsync("[data-telephony-incoming-answer]");
        await page.WaitForFunctionAsync("() => window.offerTest.accepts === 1");
        await SettleOfferFromServerAsync(page, "res-one", accepted: true);
        await ReportCallStateAsync(page, "call-one", 3);
        await ReportCallStateAsync(page, "call-one", 5);
        await page.WaitForTimeoutAsync(200);

        // A second offer rings, and the platform rings its leg to the browser, which holds it. Its deadline is real time,
        // so it leaves room for the steps before the offer is checked still ringing: at 2.5 seconds a busy machine let it
        // expire first.
        await ShowOfferAsync(page, "call-two", "res-two", expiresInMs: 8000);
        await RingOfferLegAsync(page, "res-two", "leg-two");

        // Act: the server reports the caller's live leg as connected while the offer is still ringing.
        await ReportCallStateAsync(page, "call-two", 3);
        await page.WaitForTimeoutAsync(300);

        // Assert: nothing was answered or accepted, the prompt is still up, and the phone does not show a call.
        await AssertStillRingingAsync(page, "call-two");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.offerTest.accepts"));
        Assert.Equal(0, await LegCountAsync(page, "leg-two", "answers"));

        // A leg that is not the offer's (a colleague's call) is not answered on the strength of the earlier accept.
        Assert.False(await page.EvaluateAsync<bool>("() => window.offerTest.context.shouldAutoAnswerInbound()"));

        // The prompt stays until the server's deadline, and then the phone gives the offer up: the held leg is hung up.
        await page.Locator("[data-telephony-incoming]").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = 20000,
        });
        Assert.Equal(0, await LegCountAsync(page, "leg-two", "answers"));
        Assert.Equal(1, await LegCountAsync(page, "leg-two", "hangups"));
        await AssertNotInCallAsync(page);
    }

    // The server withdraws the offer (it expired) while it still reports the caller's leg as live: the phone drops
    // the leg it held and never shows the call, even when the report comes after the offer is gone.
    [Fact]
    public async Task OfferWithdrawnWithoutAnAccept_DropsItsLeg_AndKeepsItsCallOffThePhone()
    {
        // Arrange
        var page = await OpenPhoneAsync();
        await ShowOfferAsync(page, "call-gone", "res-gone", expiresInMs: 30000);
        await RingOfferLegAsync(page, "res-gone", "leg-gone");
        await ReportCallStateAsync(page, "call-gone", 3);

        // Act
        await SettleOfferFromServerAsync(page, "res-gone", accepted: false);
        await ReportCallStateAsync(page, "call-gone", 3);
        await page.WaitForTimeoutAsync(300);

        // Assert
        Assert.True(await page.Locator("[data-telephony-incoming]").IsHiddenAsync());
        Assert.Equal(0, await LegCountAsync(page, "leg-gone", "answers"));
        Assert.Equal(1, await LegCountAsync(page, "leg-gone", "hangups"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.offerTest.accepts"));
        await AssertNotInCallAsync(page);
    }

    // The agent accepts: the held leg is answered and the server's connected report then shows the call.
    [Fact]
    public async Task OfferAccepted_AnswersItsHeldLeg_AndShowsTheCall()
    {
        // Arrange
        var page = await OpenPhoneAsync();
        await ShowOfferAsync(page, "call-take", "res-take", expiresInMs: 30000);
        await RingOfferLegAsync(page, "res-take", "leg-take");

        // Act
        await page.ClickAsync("[data-telephony-incoming-answer]");
        await page.WaitForFunctionAsync("() => window.offerTest.accepts === 1");
        await ReportCallStateAsync(page, "call-take", 3);

        // Assert
        await page.WaitForFunctionAsync(
            "() => (document.querySelector('[data-telephony-status]').textContent || '').indexOf('In call') === 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });
        Assert.Equal(1, await LegCountAsync(page, "leg-take", "answers"));
        Assert.Equal(0, await LegCountAsync(page, "leg-take", "hangups"));
    }

    // The offer is accepted somewhere else (the docked agent bar) and the server says so after reporting the call
    // connected: the phone answers its held leg and shows the call it held back.
    [Fact]
    public async Task OfferAcceptedElsewhere_AfterTheConnectedReport_ShowsTheCall()
    {
        // Arrange
        var page = await OpenPhoneAsync();
        await ShowOfferAsync(page, "call-bar", "res-bar", expiresInMs: 30000);
        await RingOfferLegAsync(page, "res-bar", "leg-bar");
        await ReportCallStateAsync(page, "call-bar", 3);
        await AssertStillRingingAsync(page, "call-bar");

        // Act
        await SettleOfferFromServerAsync(page, "res-bar", accepted: true);

        // Assert
        await page.WaitForFunctionAsync(
            "() => (document.querySelector('[data-telephony-status]').textContent || '').indexOf('In call') === 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });
        Assert.True(await page.Locator("[data-telephony-incoming]").IsHiddenAsync());
        Assert.Equal(1, await LegCountAsync(page, "leg-bar", "answers"));
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.offerTest.accepts"));
    }

    // The desktop host shows its own notification for the offer and the agent answers there. That answer is carried
    // out through the page's own accept, and a server report of the caller's live leg that arrives first neither takes
    // the call nor tells the host the offer is over.
    [Fact]
    public async Task HostAnswer_GoesThroughTheAccept_AndAnEarlierLiveReportNeverTakesTheCall()
    {
        // Arrange
        var page = await OpenPhoneAsync("?browserAudio=true&embedded=true", withHost: true);
        await page.WaitForFunctionAsync("() => typeof window.hostTest.listener === 'function'");
        await HostSendAsync(page, """{ "type": "host-ready", "protocol": 1 }""");
        await ShowOfferAsync(page, "call-host", "res-host", expiresInMs: 30000, waitForModal: false);
        await RingOfferLegAsync(page, "res-host", "leg-host");
        await page.WaitForFunctionAsync("() => window.hostTest.posted.some(m => m.type === 'incoming-call' && m.callId === 'call-host')");
        await HostSendAsync(page, """{ "type": "incoming-call-shown", "callId": "call-host", "ringing": true }""");

        // Act: the server reports the caller's leg connected before anyone accepted.
        await ReportCallStateAsync(page, "call-host", 3);
        await page.WaitForTimeoutAsync(300);

        // Assert: still ringing, nothing taken, and the host still shows its notification.
        var current = await page.EvaluateAsync<JsonElement>("() => window.telephonySoftPhone.getInstance().getCurrentCall()");
        Assert.Equal("call-host", current.GetProperty("callId").GetString());
        Assert.Equal("Ringing", current.GetProperty("state").ToString());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.offerTest.accepts"));
        Assert.Equal(0, await LegCountAsync(page, "leg-host", "answers"));
        Assert.False(await page.EvaluateAsync<bool>("() => window.hostTest.posted.some(m => m.type === 'incoming-call-ended')"));

        // Act: the agent answers in the host's notification.
        await HostSendAsync(page, """{ "type": "incoming-call-action", "callId": "call-host", "action": "answer" }""");

        // Assert: one accept, the held leg answered once, and the host told the answer was handled.
        await page.WaitForFunctionAsync("() => window.offerTest.accepts === 1");
        await page.WaitForFunctionAsync("() => window.hostTest.posted.some(m => m.type === 'incoming-call-action-result' && m.handled === true)");
        Assert.Equal(1, await LegCountAsync(page, "leg-host", "answers"));
        Assert.Equal(0, await LegCountAsync(page, "leg-host", "hangups"));
    }

    private async Task<IPage> OpenPhoneAsync(string query = "?browserAudio=true", bool withHost = false)
    {
        var page = await Browser.NewPageAsync();
        await page.AddInitScriptAsync(InitScript);

        if (withHost)
        {
            await page.AddInitScriptAsync(HostInitScript);
        }

        await page.GotoAsync(Server.BaseUrl + query);
        await WaitForConnectedAsync(page);
        await page.EvaluateAsync(RegisterAdapterScript);
        await page.ClickAsync("[data-telephony-toggle]");

        return page;
    }

    private static async Task ShowOfferAsync(IPage page, string callId, string reservationId, int expiresInMs, bool waitForModal = true)
    {
        await page.EvaluateAsync(
            """
            ([callId, reservationId, expiresInMs]) => {
                const now = new Date();
                window.telephonySoftPhone.getInstance().setIncomingOffer(
                    { callId, from: '+15550001000', direction: 'Inbound', state: 'Ringing', providerName: 'InMemory' },
                    {
                        properties: {
                            acceptUrl: '/accept',
                            reservationId,
                            serverTimeUtc: now.toISOString(),
                            expiresUtc: new Date(now.getTime() + expiresInMs).toISOString()
                        }
                    });
            }
            """,
            new object[] { callId, reservationId, expiresInMs });

        if (waitForModal)
        {
            await page.Locator("[data-telephony-incoming]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        }

        // The phone registers when an offer rings; the leg can only reach a registered phone.
        await page.WaitForFunctionAsync("() => !!window.offerTest.context", null, new PageWaitForFunctionOptions { Timeout = 5000 });
    }

    // Rings the browser with the offer's leg, tagged the way the platform tags it, as the provider SDK would.
    private static async Task RingOfferLegAsync(IPage page, string reservationId, string legId)
    {
        var claimed = await page.EvaluateAsync<bool>(
            """
            ([reservationId, legId]) => {
                const leg = { answers: 0, hangups: 0 };
                window.offerTest.legs[legId] = leg;
                const take = window.offerTest.context.claimOfferLeg({
                    clientState: btoa(JSON.stringify({ i: 'cc-predial', r: reservationId })),
                    telnyxCallControlId: legId
                });

                if (typeof take !== 'function') {
                    return false;
                }

                take({
                    legId,
                    answer: function () { leg.answers++; },
                    hangup: function () { leg.hangups++; }
                });

                return true;
            }
            """,
            new[] { reservationId, legId });

        Assert.True(claimed);
    }

    // What the Contact Center layer does when the server withdraws an offer (OfferRevoked).
    private static async Task SettleOfferFromServerAsync(IPage page, string reservationId, bool accepted)
    {
        await page.EvaluateAsync(
            """
            ([reservationId, accepted]) => {
                const api = window.telephonySoftPhone.getInstance();
                const acceptPending = api.isIncomingAcceptPending();
                api.markOfferSettled({ reservationId });
                const answeredHeldLeg = api.settleOfferLeg(reservationId, accepted);

                if (accepted && !answeredHeldLeg) {
                    api.armInboundAutoAnswer(reservationId);
                }

                api.clearIncomingOffer({
                    preserveCurrentCall: accepted && acceptPending,
                    preservePendingAccept: accepted && acceptPending
                });
            }
            """,
            new object[] { reservationId, accepted });
    }

    private static async Task ReportCallStateAsync(IPage page, string callId, int state)
    {
        await page.EvaluateAsync(
            """
            ([callId, state]) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                { callId, from: '+15550001000', direction: 1, state, providerName: 'InMemory' })
            """,
            new object[] { callId, state });
    }

    private static async Task HostSendAsync(IPage page, string json)
    {
        await page.EvaluateAsync("json => window.hostTest.listener({ data: JSON.parse(json) })", json);
    }

    private static Task<int> LegCountAsync(IPage page, string legId, string counter)
    {
        return page.EvaluateAsync<int>("([legId, counter]) => window.offerTest.legs[legId][counter]", new[] { legId, counter });
    }

    private static async Task AssertStillRingingAsync(IPage page, string callId)
    {
        Assert.True(await page.Locator("[data-telephony-incoming]").IsVisibleAsync());
        var current = await page.EvaluateAsync<JsonElement>("() => window.telephonySoftPhone.getInstance().getCurrentCall()");
        Assert.Equal(callId, current.GetProperty("callId").GetString());
        Assert.Equal("Ringing", current.GetProperty("state").ToString());
        Assert.False((await page.Locator("[data-telephony-status]").InnerTextAsync()).StartsWith("In call", StringComparison.Ordinal));
    }

    private static async Task AssertNotInCallAsync(IPage page)
    {
        Assert.False((await page.Locator("[data-telephony-status]").InnerTextAsync()).StartsWith("In call", StringComparison.Ordinal));
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.telephonySoftPhone.getInstance().getActiveCalls().length"));
    }
}
