using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the real soft phone bundle and its real Telnyx media adapter, against a stand-in for the Telnyx WebRTC SDK,
/// through calls the browser places itself. The platform never sees such a call: the phone's own reports are the only
/// record of it, so every call the phone placed has to report its end exactly once, and the phone may never go on
/// showing a call the SDK no longer has.
/// </summary>
public sealed class SoftPhoneBrowserCallTrackingTests : SoftPhoneBrowserTest
{
    // A stand-in for window.TelnyxWebRTC with just the surface the adapter uses: a client that logs in, keeps its calls
    // in a `calls` registry by id (as the SDK does) and reports each state change as a callUpdate notification. A test
    // drives the far end (answering, hanging up) and the socket (dropping, reconnecting) through window.fakeTelnyx.
    private const string FakeTelnyxScript =
        """
        (function () {
            var nextId = 1;
            var state = window.fakeTelnyx = { clients: [], calls: [] };

            function FakeCall(client, options, direction) {
                this.id = 'sdk-call-' + (nextId++);
                this.client = client;
                this.options = options || {};
                this.direction = direction;
                this.state = 'new';
                this.peer = null;
                client.calls[this.id] = this;
                state.calls.push(this);
            }

            FakeCall.prototype.setState = function (next) {
                this.state = next;

                if (next === 'destroy') {
                    delete this.client.calls[this.id];
                }

                this.client.emit('telnyx.notification', { type: 'callUpdate', call: this });
            };

            FakeCall.prototype.hangup = function () {
                if (this.state === 'hangup' || this.state === 'destroy') {
                    return Promise.resolve();
                }

                this.setState('hangup');
                this.setState('destroy');

                return Promise.resolve();
            };

            FakeCall.prototype.hold = function () { this.setState('held'); return Promise.resolve(); };
            FakeCall.prototype.unhold = function () { this.setState('active'); return Promise.resolve(); };
            FakeCall.prototype.muteAudio = function () { };
            FakeCall.prototype.unmuteAudio = function () { };
            FakeCall.prototype.answer = function () { this.setState('active'); };

            function TelnyxRTC(options) {
                this.options = options;
                this.calls = {};
                this.handlers = {};
                state.clients.push(this);
            }

            TelnyxRTC.prototype.on = function (name, handler) {
                (this.handlers[name] = this.handlers[name] || []).push(handler);

                return this;
            };

            TelnyxRTC.prototype.off = function () { return this; };

            TelnyxRTC.prototype.emit = function (name, payload) {
                (this.handlers[name] || []).slice().forEach(function (handler) { handler(payload); });
            };

            TelnyxRTC.prototype.connect = function () {
                var client = this;
                setTimeout(function () { client.emit('telnyx.ready'); }, 0);
            };

            TelnyxRTC.prototype.disconnect = function () {
                this.calls = {};

                return Promise.resolve();
            };

            TelnyxRTC.prototype.newCall = function (options) {
                var call = new FakeCall(this, options, 'outbound');
                setTimeout(function () { call.setState('trying'); }, 0);

                return call;
            };

            window.TelnyxWebRTC = { TelnyxRTC: TelnyxRTC };

            state.client = function () { return state.clients[state.clients.length - 1]; };
            state.callTo = function (number) {
                return state.calls.filter(function (call) { return call.options.destinationNumber === number; }).pop();
            };

            // The registration config the adapter fetches before it logs in.
            var realFetch = window.fetch.bind(window);
            window.fetch = function (url, init) {
                if (String(url).indexOf('registration-config') >= 0) {
                    return Promise.resolve({
                        ok: true,
                        json: function () {
                            return Promise.resolve({
                                signaling: { authorizationUser: 'agent-login' },
                                credential: { id: 'credential-1', value: 'secret' },
                                ice: {},
                                media: {}
                            });
                        }
                    });
                }

                return realFetch(url, init);
            };
        }());
        """;

    // Bug: the agent dialed a number, put it on hold and dialed a second one. The adapter kept one state callback for
    // "the" outbound call, so the second call took it over: when the first call ended the phone was never told, its end
    // never reached the server (the history stayed "in progress" for good), and the phone went on showing it in a call.
    // Both calls also played through the one remote audio element (the SDK's SHARED_REMOTE_ELEMENT_OVERWRITE warning).
    [Fact]
    public async Task SecondCallPlacedWhileTheFirstIsHeld_BothEnd_TheyReportTheirEndsOnce_AndThePhoneClears()
    {
        // Arrange
        var page = await OpenTelnyxPhoneAsync();
        await PlaceAndAnswerAsync(page, "+17025550101");
        await page.EvaluateAsync("() => window.telephonySoftPhone.getInstance().hold()");
        await PlaceAndAnswerAsync(page, "+17025550102");
        await WaitForActiveCallCountAsync(page, 2);

        // Each call plays through an audio element of its own.
        var separateAudio = await page.EvaluateAsync<bool>(
            """
            () => {
                const first = window.fakeTelnyx.callTo('+17025550101').options.remoteElement;
                const second = window.fakeTelnyx.callTo('+17025550102').options.remoteElement;
                return !!first && !!second && first !== second;
            }
            """);

        // Act: the far end of the held call hangs up first, then the second call ends.
        await page.EvaluateAsync("() => window.fakeTelnyx.callTo('+17025550101').hangup()");
        await WaitForActiveCallCountAsync(page, 1);
        await page.EvaluateAsync("() => window.fakeTelnyx.callTo('+17025550102').hangup()");

        // Assert
        await WaitForActiveCallCountAsync(page, 0);
        var log = await ReadBrowserCallLogAsync(page, expectedEnds: 2);
        Assert.Equal(2, log.Started.Length);
        Assert.Equal(log.Started.Order(StringComparer.Ordinal), log.Ended.Order(StringComparer.Ordinal));
        Assert.Equal(log.Ended.Length, log.Ended.Distinct(StringComparer.Ordinal).Count());
        Assert.True(await page.EvaluateAsync<bool>("() => window.telephonySoftPhone.getInstance().getCurrentCall() === null"));

        // Each call's end was heard from the call itself, not inferred later by the phone's check for calls it lost.
        Assert.DoesNotContain(log.Diagnostics, entry =>
            entry.StartsWith("browser-call-reconciled:", StringComparison.Ordinal) ||
            entry.StartsWith("browser-call-vanished:", StringComparison.Ordinal));
        Assert.True(separateAudio, "Each call must play through a remote audio element of its own.");

        // The phone told the server the first call was up while it lasted, so the server's sweep leaves a live call be.
        Assert.Contains(log.Started.Order(StringComparer.Ordinal).First(), log.Alive);
    }

    // Bug: the SDK's socket dropped (the agent's Wi-Fi went away) and came back without the call; the SDK never sent a
    // hang-up for it, so the phone showed "In call" for a call that no longer existed, indefinitely.
    [Fact]
    public async Task SdkDropsTheCallAcrossAReconnect_ThePhoneClearsIt_AndReportsItsEndOnce()
    {
        // Arrange
        var page = await OpenTelnyxPhoneAsync();
        await PlaceAndAnswerAsync(page, "+17025550103");

        // Act: the socket drops, the call vanishes from the SDK without a word, and the client logs back in.
        await page.EvaluateAsync(
            """
            () => {
                const client = window.fakeTelnyx.client();
                const call = window.fakeTelnyx.callTo('+17025550103');
                client.emit('telnyx.socket.close');
                delete client.calls[call.id];
                client.emit('telnyx.socket.open');
                client.emit('telnyx.ready');
            }
            """);

        // Assert: the phone noticed the call was gone, ended it and reported the end once.
        await WaitForActiveCallCountAsync(page, 0);
        var log = await ReadBrowserCallLogAsync(page, expectedEnds: 1);
        Assert.Single(log.Ended);
        Assert.Equal(log.Started, log.Ended);
        Assert.Contains(log.Diagnostics, entry => entry.StartsWith("browser-call-vanished:", StringComparison.Ordinal));
    }

    // The page is reloaded mid-call while the hub is down: the SDK, and the call with it, go away with the page, the
    // old page cannot report the end, and the new page has to settle the call the old one never got to report.
    [Fact]
    public async Task PageReloadedDuringACall_TheNewPageReportsTheCallsEnd()
    {
        // Arrange
        var page = await OpenTelnyxPhoneAsync();
        await PlaceAndAnswerAsync(page, "+17025550104");
        var started = (await ReadBrowserCallLogAsync(page, expectedEnds: 0)).Started;

        // Act
        page.Dialog += (_, dialog) => dialog.AcceptAsync();
        await page.EvaluateAsync(
            """
            async () => {
                // The hub goes down and stays down until the page is gone.
                const connection = window.telephonySoftPhone.getInstance().getConnection();
                connection.start = () => new Promise(() => { });
                await connection.stop();
            }
            """);
        await page.ReloadAsync();
        await WaitForConnectedAsync(page);

        // Assert
        var log = await ReadBrowserCallLogAsync(page, expectedEnds: 1);
        Assert.Equal(started, log.Ended.Distinct(StringComparer.Ordinal));
        await WaitForActiveCallCountAsync(page, 0);
    }

    private async Task<IPage> OpenTelnyxPhoneAsync()
    {
        var page = await Browser.NewPageAsync();
        await page.AddInitScriptAsync(FakeTelnyxScript);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true&" + SoftPhoneTestServer.MediaAdapterQueryKey + "=telnyx-webrtc");
        await WaitForConnectedAsync(page);
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.clients.length > 0");

        return page;
    }

    private static async Task PlaceAndAnswerAsync(IPage page, string number)
    {
        await page.EvaluateAsync("number => window.telephonySoftPhone.getInstance().dialNumber(number)", number);
        await page.WaitForFunctionAsync("number => !!window.fakeTelnyx.callTo(number)", number);
        await page.EvaluateAsync("number => window.fakeTelnyx.callTo(number).setState('active')", number);
        await page.WaitForFunctionAsync(
            """
            number => window.telephonySoftPhone.getInstance().getActiveCalls()
                .some(call => call.to === number && call.state === 'Connected')
            """,
            number);
    }

    private static async Task WaitForActiveCallCountAsync(IPage page, int count, int timeoutMs = 10000)
        => await page.WaitForFunctionAsync(
            "count => window.telephonySoftPhone.getInstance().getActiveCalls().length === count",
            count,
            new PageWaitForFunctionOptions { Timeout = timeoutMs });

    private static async Task<BrowserCallLogSnapshot> ReadBrowserCallLogAsync(IPage page, int expectedEnds)
    {
        // The reports are fire-and-forget over the hub, so give the last of them a moment to land before reading.
        var json = await page.EvaluateAsync<JsonElement>(
            """
            async expected => {
                const connection = window.telephonySoftPhone.getInstance().getConnection();
                const deadline = Date.now() + 10000;
                let log = await connection.invoke('GetBrowserCallLog');

                while (log.ended.length < expected && Date.now() < deadline) {
                    await new Promise(resolve => setTimeout(resolve, 100));
                    log = await connection.invoke('GetBrowserCallLog');
                }

                // A report the phone should not have sent (a second end) would land just after the expected ones.
                await new Promise(resolve => setTimeout(resolve, 300));

                return await connection.invoke('GetBrowserCallLog');
            }
            """,
            expectedEnds);

        return new BrowserCallLogSnapshot
        {
            Started = [.. json.GetProperty("started").EnumerateArray().Select(value => value.GetString())],
            Ended = [.. json.GetProperty("ended").EnumerateArray().Select(value => value.GetString())],
            Alive = [.. json.GetProperty("alive").EnumerateArray().Select(value => value.GetString())],
            Diagnostics = [.. json.GetProperty("diagnostics").EnumerateArray().Select(value => value.GetString())],
        };
    }
}
