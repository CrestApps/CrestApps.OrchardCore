using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the soft phone's own Telnyx adapter over <see cref="FakeTelnyxSdk"/>, with Chromium's fake microphone, to prove
/// what the caller receives from the agent: the track the agent's sender carries, and the bytes it puts on the wire.
/// </summary>
public sealed class SoftPhoneTelnyxSendAudioTests : SoftPhoneBrowserTest
{
    private const string AcceptScript =
        """
        window.offerTest = { accepts: 0 };
        var acceptFetch = window.fetch.bind(window);
        window.fetch = function (url, init) {
            if (String(url).indexOf('/accept') !== 0) {
                return acceptFetch(url, init);
            }

            window.offerTest.accepts++;
            return Promise.resolve({ ok: true, json: function () { return Promise.resolve({ succeeded: true, requiresDeviceAnswer: false }); } });
        };
        """;

    // Bug: every call after the first on a registration was one-way. The provider reported receiving no packets from the
    // agent and the soft phone's own summary read BytesSent=0 with its send track "ended": the SDK had stopped the shared
    // microphone when the previous call ended, and the next offer's held leg was answered on the dead track.
    [Fact]
    public async Task HeldOfferLeg_AnsweredAfterAnEarlierCallEnded_SendsTheSameLiveMicrophone()
    {
        // Arrange: the agent takes a call on a held offer leg, and the caller hangs up.
        var page = await OpenPhoneAsync();
        await AnswerOfferAsync(page, "call-one", "res-one", "leg-one", expectedAccepts: 1);
        var first = await AssertSendingAsync(page, "leg-one");
        await page.EvaluateAsync("() => window.fakeTelnyx.byLeg('leg-one').hangup()");
        await ReportCallStateAsync(page, "call-one", 5);

        // Act: the next offer rings, its leg is held, and the agent accepts it.
        await AnswerOfferAsync(page, "call-two", "res-two", "leg-two", expectedAccepts: 2);

        // Assert: the caller receives the agent's microphone, the same capture the first call sent: the end of that call
        // did not stop it, and nothing had to register again to get it back.
        var second = await AssertSendingAsync(page, "leg-two");
        Assert.Equal(first.GetProperty("trackId").GetString(), second.GetProperty("trackId").GetString());
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.fakeTelnyx.clientCount"));
    }

    // However the shared capture ended while the phone sat idle, the next held leg is answered on a live microphone: a
    // fresh one is taken first, without registering again (which would hang up the very leg being answered).
    [Fact]
    public async Task HeldOfferLeg_AnsweredOnACaptureThatEnded_TakesAFreshMicrophone()
    {
        // Arrange
        var page = await OpenPhoneAsync();
        await AnswerOfferAsync(page, "call-one", "res-one", "leg-one", expectedAccepts: 1);
        var first = await AssertSendingAsync(page, "leg-one");
        await page.EvaluateAsync("() => { window.sharedCapture = window.fakeTelnyx.byLeg('leg-one').options.localStream; }");
        await page.EvaluateAsync("() => window.fakeTelnyx.byLeg('leg-one').hangup()");
        await ReportCallStateAsync(page, "call-one", 5);
        await page.EvaluateAsync("() => window.sharedCapture.getAudioTracks().forEach(track => track.stop())");

        // Act
        await AnswerOfferAsync(page, "call-two", "res-two", "leg-two", expectedAccepts: 2);

        // Assert
        var second = await AssertSendingAsync(page, "leg-two");
        Assert.NotEqual(first.GetProperty("trackId").GetString(), second.GetProperty("trackId").GetString());
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.fakeTelnyx.clientCount"));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.fakeTelnyx.byLeg('leg-two').answers"));
        await WaitForAsync(() => Server.Provider.ClientDiagnosticCodes.Contains("microphone-revived"));
    }

    // Hold swaps the microphone for hold audio on the sender, and resume swaps it back: after resuming, the caller must
    // be receiving the live microphone again, not the tone and not a stopped track.
    [Fact]
    public async Task HeldOfferLeg_AfterHoldAndResume_SendsTheAgentsMicrophone()
    {
        // Arrange
        var page = await OpenPhoneAsync();
        await AnswerOfferAsync(page, "call-hold", "res-hold", "leg-hold", expectedAccepts: 1);
        var before = await AssertSendingAsync(page, "leg-hold");

        // Act
        var microphoneId = before.GetProperty("trackId").GetString();
        await ReportCallStateAsync(page, "call-hold", 4, isOnHold: true);
        await WaitForSenderTrackAsync(page, "leg-hold", microphoneId, expected: false);
        await ReportCallStateAsync(page, "call-hold", 3);

        // Assert
        await WaitForSenderTrackAsync(page, "leg-hold", microphoneId, expected: true);
        await AssertSendingAsync(page, "leg-hold");
    }

    // However the send track dies, the agent must not stay silently one-way: the phone warns them, reports it, puts a
    // fresh microphone on the call, and the call's quality summary says audio stopped leaving.
    [Fact]
    public async Task SendTrackDyingMidCall_WarnsTheAgent_RecoversTheMicrophone_AndFlagsTheSummary()
    {
        // Arrange
        var page = await OpenPhoneAsync();
        await AnswerOfferAsync(page, "call-dead", "res-dead", "leg-dead", expectedAccepts: 1);
        await AssertSendingAsync(page, "leg-dead");

        await page.EvaluateAsync(
            """
            () => {
                const error = document.querySelector('[data-telephony-error]');
                window.shownErrors = [];
                new MutationObserver(() => window.shownErrors.push(error.textContent))
                    .observe(error, { childList: true, characterData: true, subtree: true });
            }
            """);

        // Act: the track on the sender stops without a word, as a track stopped by other code does.
        await page.EvaluateAsync(
            "() => window.fakeTelnyx.byLeg('leg-dead').peer.instance.getSenders().forEach(s => s.track && s.track.stop())");

        // Assert: the agent is told, the server hears about it, and a live microphone is back on the call.
        await page.WaitForFunctionAsync(
            "() => window.shownErrors.some(text => text.indexOf('The caller may not hear you') === 0)",
            null,
            new PageWaitForFunctionOptions { Timeout = 10000 });
        await WaitForAsync(() => Server.Provider.ClientDiagnosticCodes.Contains("no-outbound-audio"));
        await AssertSendingAsync(page, "leg-dead");

        // Once audio leaves again the warning goes, since it is no longer true.
        await page.WaitForFunctionAsync(
            "() => (document.querySelector('[data-telephony-error]').textContent || '') === ''",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Act: the caller hangs up.
        await page.EvaluateAsync("() => window.fakeTelnyx.byLeg('leg-dead').hangup()");

        // Assert: the call's summary is flagged.
        await WaitForAsync(() => Server.Provider.CallQualityReports.Any(report => report.Final));
        Assert.True(Server.Provider.CallQualityReports.Single(report => report.Final).OutboundAudioStalled);
    }

    private async Task<IPage> OpenPhoneAsync()
    {
        Server.Provider.BrowserMediaAdapterName = "telnyx-webrtc";

        var page = await Browser.NewPageAsync();
        await page.AddInitScriptAsync(FakeTelnyxSdk.Script);
        await page.AddInitScriptAsync(AcceptScript);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true");
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");

        return page;
    }

    private static async Task AnswerOfferAsync(IPage page, string callId, string reservationId, string legId, int expectedAccepts)
    {
        await page.EvaluateAsync(
            """
            ([callId, reservationId]) => {
                const now = new Date();
                window.telephonySoftPhone.getInstance().setIncomingOffer(
                    { callId, from: '+15550001000', direction: 'Inbound', state: 'Ringing', providerName: 'InMemory' },
                    {
                        properties: {
                            acceptUrl: '/accept',
                            reservationId,
                            serverTimeUtc: now.toISOString(),
                            expiresUtc: new Date(now.getTime() + 30000).toISOString()
                        }
                    });
            }
            """,
            new object[] { callId, reservationId });
        await page.Locator("[data-telephony-incoming]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // The phone registers when an offer rings; the platform then rings the registered browser with the offer's leg.
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.ready", null, new PageWaitForFunctionOptions { Timeout = 5000 });
        await page.WaitForFunctionAsync(
            "() => window.telephonySoftPhone.getInstance().getActiveCalls().length > 0",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });
        await page.EvaluateAsync("([reservationId, legId]) => window.fakeTelnyx.ringOfferLeg(reservationId, legId)", new[] { reservationId, legId });
        Assert.Equal(0, await page.EvaluateAsync<int>("legId => window.fakeTelnyx.byLeg(legId).answers", legId));

        await page.ClickAsync("[data-telephony-incoming-answer]");
        await page.WaitForFunctionAsync("n => window.offerTest.accepts === n", expectedAccepts);
        await ReportCallStateAsync(page, callId, 3);
        await page.WaitForFunctionAsync(
            "legId => window.fakeTelnyx.byLeg(legId).state === 'active'",
            legId,
            new PageWaitForFunctionOptions { Timeout = 5000 });
    }

    // Asserts that, within a few seconds, the agent's sender carries a live, enabled track and its sent-bytes counter is
    // rising.
    private static async Task<JsonElement> AssertSendingAsync(IPage page, string legId)
    {
        var sending = await page.EvaluateAsync<JsonElement>(
            """
            async legId => {
                const deadline = Date.now() + 8000;
                let previous = await window.fakeTelnyx.readSending(legId);

                while (Date.now() < deadline) {
                    await new Promise(resolve => setTimeout(resolve, 500));
                    const current = await window.fakeTelnyx.readSending(legId);

                    if (current.trackState === 'live' && current.enabled && current.bytesSent > previous.bytesSent) {
                        return Object.assign({ ok: true }, current);
                    }

                    previous = current;
                }

                return Object.assign({ ok: false }, previous);
            }
            """,
            legId);

        Assert.True(sending.GetProperty("ok").GetBoolean(), $"The agent's leg {legId} is not sending live audio: {sending}.");

        return sending;
    }

    // Waits until the agent's sender carries the given track (or, with expected false, any other track).
    private static async Task WaitForSenderTrackAsync(IPage page, string legId, string trackId, bool expected)
    {
        var matched = await page.EvaluateAsync<bool>(
            """
            async ([legId, trackId, expected]) => {
                const deadline = Date.now() + 5000;

                while (Date.now() < deadline) {
                    const current = await window.fakeTelnyx.readSending(legId);

                    if ((current.trackId === trackId) === expected) {
                        return true;
                    }

                    await new Promise(resolve => setTimeout(resolve, 100));
                }

                return false;
            }
            """,
            new object[] { legId, trackId, expected });

        Assert.True(matched, $"The agent's leg {legId} sender track {(expected ? "never became" : "never left")} {trackId}.");
    }

    private static async Task ReportCallStateAsync(IPage page, string callId, int state, bool isOnHold = false)
    {
        await page.EvaluateAsync(
            """
            ([callId, state, isOnHold]) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishTrackedCallState',
                { callId, from: '+15550001000', direction: 1, state, isOnHold, providerName: 'InMemory' })
            """,
            new object[] { callId, state, isOnHold });
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
