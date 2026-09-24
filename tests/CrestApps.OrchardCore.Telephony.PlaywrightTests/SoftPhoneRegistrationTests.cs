using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the real soft phone bundle to prove the browser stays registered with the provider between calls and that
/// answering an offer responds at once, even when the phone has to register first.
/// </summary>
public sealed class SoftPhoneRegistrationTests : IAsyncLifetime
{
    // Stands in for the provider media client (the browser supplies a fake microphone): counts registrations (each adapter call is one) and
    // tear-downs, and lets a test hold a registration open to see what the phone shows meanwhile.
    private const string BrowserAudioInitScript =
        """
        window.registration = { adapterCalls: 0, disposed: 0, accepts: 0, release: null, hold: false };
        // Only the offer's accept is answered here; the hub's own requests go through untouched.
        var realFetch = window.fetch.bind(window);
        window.fetch = function (url, init) {
            if (String(url).indexOf('/accept') !== 0) {
                return realFetch(url, init);
            }

            window.registration.accepts++;
            return Promise.resolve({ ok: true, json: function () { return Promise.resolve({ succeeded: true, requiresDeviceAnswer: false }); } });
        };
        """;

    private const string RegisterAdapterScript =
        """
        () => window.telephonySoftPhone.getInstance().registerMediaAdapter('in-memory', function () {
            var state = window.registration;
            state.adapterCalls++;
            var session = {
                handleCallState: function () { },
                dispose: function () { state.disposed++; }
            };

            if (!state.hold) {
                return session;
            }

            return new Promise(function (resolve) { state.release = function () { resolve(session); }; });
        })
        """;

    private SoftPhoneTestServer _server = null!;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public async ValueTask InitializeAsync()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}.");
        }

        _server = new SoftPhoneTestServer();
        await _server.StartAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--use-fake-device-for-media-stream", "--use-fake-ui-for-media-stream"],
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }

    // Bug: when the last server-tracked call ended, the phone tore its provider registration down and never set it up
    // again. The next offer's leg was rung at a credential nothing was registered on (SIP 480) and the agent could
    // only take the call after clicking Answer and waiting for a fresh registration.
    [Fact]
    public async Task BrowserAudio_StaysRegistered_WhenTheLastServerTrackedCallEnds()
    {
        // Arrange
        var page = await OpenPhoneAsync();
        await ShowOfferAsync(page, "call-one", "res-one");
        await page.ClickAsync("[data-telephony-incoming-answer]");
        await page.WaitForFunctionAsync("() => window.registration.accepts === 1");

        // Act
        await PublishCallStateAsync(page, "call-one", 3);
        await PublishCallStateAsync(page, "call-one", 5);
        await page.WaitForTimeoutAsync(300);

        // Assert
        var state = await page.EvaluateAsync<JsonElement>("() => window.registration");
        Assert.Equal(1, state.GetProperty("adapterCalls").GetInt32());
        Assert.Equal(0, state.GetProperty("disposed").GetInt32());

        // The next offer is answered on the registration that is still live, without registering again.
        await ShowOfferAsync(page, "call-two", "res-two");
        await page.ClickAsync("[data-telephony-incoming-answer]");
        await page.WaitForFunctionAsync("() => window.registration.accepts === 2");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.registration.adapterCalls"));
    }

    // Bug: an unregistered phone waited for the agent's click before it started registering, so the platform's leg
    // for the ringing offer had nowhere to ring and the click paid for the whole registration.
    [Fact]
    public async Task IncomingOffer_WhenThePhoneIsNotRegistered_StartsRegisteringBeforeTheAgentClicks()
    {
        // Arrange
        var page = await OpenPhoneAsync();

        // Act
        await ShowOfferAsync(page, "call-early", "res-early");

        // Assert
        await page.WaitForFunctionAsync("() => window.registration.adapterCalls === 1", null, new PageWaitForFunctionOptions { Timeout = 5000 });
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.registration.accepts"));
    }

    // Bug: a click on Answer that had to wait for the registration changed nothing on screen for several seconds -- the
    // ringtone went on and the buttons stayed live -- so the agent kept clicking, thinking the machine had frozen.
    [Fact]
    public async Task Answer_WhileTheRegistrationIsRunning_ShowsConnectingAtOnce_AndAcceptsOnceItCompletes()
    {
        // Arrange
        var page = await OpenPhoneAsync(holdRegistration: true);
        await ShowOfferAsync(page, "call-slow", "res-slow");

        // Act
        await page.EvaluateAsync(
            """
            () => {
                var answer = document.querySelector('[data-telephony-incoming-answer]');
                answer.click();
                answer.click();
                answer.click();
            }
            """);

        // Assert
        Assert.True(await page.Locator("[data-telephony-incoming-answer]").IsDisabledAsync());
        Assert.True(await page.Locator("[data-telephony-incoming-ignore]").IsDisabledAsync());
        Assert.Equal("Answering…", (await page.Locator("[data-telephony-incoming-answer]").InnerTextAsync()).Trim());
        Assert.Equal(1, await page.Locator("[data-telephony-incoming-answer] .spinner-border").CountAsync());
        Assert.Equal("Connecting...", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.registration.accepts"));

        await page.WaitForFunctionAsync("() => typeof window.registration.release === 'function'");
        await page.EvaluateAsync("() => window.registration.release()");
        await page.WaitForFunctionAsync("() => window.registration.accepts === 1");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.registration.accepts"));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.registration.adapterCalls"));
    }

    // Bug: an offer revoked the moment it was made (the caller hung up as it was presented) flashed the incoming-call
    // modal up: a copy of the offer fetched before the revoke landed after it and re-opened a call that was over.
    [Fact]
    public async Task IncomingOffer_RevokedBeforeItRenders_NeverOpensTheModal()
    {
        // Arrange
        var page = await OpenPhoneAsync();
        await page.EvaluateAsync("() => window.telephonySoftPhone.getInstance().markOfferSettled({ reservationId: 'res-gone', callId: 'call-gone' })");

        // Act
        await page.EvaluateAsync(
            """
            () => window.telephonySoftPhone.getInstance().setIncomingOffer(
                { callId: 'call-gone', from: '+15550001000', direction: 'Inbound', state: 'Ringing', providerName: 'InMemory' },
                { properties: { acceptUrl: '/accept', reservationId: 'res-gone' } })
            """);
        await page.WaitForTimeoutAsync(200);

        // Assert
        Assert.True(await page.Locator("[data-telephony-incoming]").IsHiddenAsync());
    }

    // The caller hung up before the offer reached the phone: the call's own end, reported first, keeps it from opening.
    [Fact]
    public async Task IncomingOffer_WhoseCallAlreadyEnded_NeverOpensTheModal()
    {
        // Arrange
        var page = await OpenPhoneAsync();
        await PublishCallStateAsync(page, "call-ended", 5);

        // Act
        await page.EvaluateAsync(
            """
            () => window.telephonySoftPhone.getInstance().setIncomingOffer(
                { callId: 'call-ended', from: '+15550001000', direction: 'Inbound', state: 'Ringing', providerName: 'InMemory' },
                { properties: { acceptUrl: '/accept', reservationId: 'res-ended' } })
            """);
        await page.WaitForTimeoutAsync(200);

        // Assert
        Assert.True(await page.Locator("[data-telephony-incoming]").IsHiddenAsync());
    }

    private async Task<IPage> OpenPhoneAsync(bool holdRegistration = false)
    {
        var page = await _browser.NewPageAsync();
        await page.AddInitScriptAsync(BrowserAudioInitScript);
        await page.GotoAsync(_server.BaseUrl + "?browserAudio=true");
        await page.WaitForFunctionAsync(
            """
            () => {
                const el = document.querySelector('#telephony-soft-phone');
                const api = el && el.__telephonySoftPhone;
                const connection = api && api.getConnection && api.getConnection();
                return connection && connection.state === 'Connected';
            }
            """);

        // The adapter is registered only after the phone connected, so the registration the phone attempts on connect
        // finds no adapter and fails: the phone starts each test unregistered, as it was when the offer rang.
        await page.WaitForTimeoutAsync(200);
        await page.EvaluateAsync($"() => {{ window.registration.hold = {(holdRegistration ? "true" : "false")}; }}");
        await page.EvaluateAsync(RegisterAdapterScript);
        await page.ClickAsync("[data-telephony-toggle]");

        return page;
    }

    private static async Task ShowOfferAsync(IPage page, string callId, string reservationId)
    {
        await page.EvaluateAsync(
            """
            ([callId, reservationId]) => window.telephonySoftPhone.getInstance().setIncomingOffer(
                { callId, from: '+15550001000', direction: 'Inbound', state: 'Ringing', providerName: 'InMemory' },
                { properties: { acceptUrl: '/accept', reservationId } })
            """,
            new[] { callId, reservationId });
        await page.Locator("[data-telephony-incoming]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    private static async Task PublishCallStateAsync(IPage page, string callId, int state)
    {
        await page.EvaluateAsync(
            """
            ([callId, state]) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                { callId, from: '+15550001000', direction: 1, state, providerName: 'InMemory' })
            """,
            new object[] { callId, state });
    }
}
