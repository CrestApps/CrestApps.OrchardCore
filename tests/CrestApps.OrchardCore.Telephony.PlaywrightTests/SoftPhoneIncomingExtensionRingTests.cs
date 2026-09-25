using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// A colleague's extension call rings this phone with Answer and Decline -- every time, not only the first -- driven
/// through the soft phone's own Telnyx adapter over <see cref="FakeTelnyxSdk"/>.
/// </summary>
/// <remarks>
/// Live, the first extension call to a phone rang and was answered. Every later one was answered on arrival, under a
/// second, with no ring and nothing on screen, so the caller heard nobody and the callee never knew. Answering a ring
/// had left the phone believing it was expecting a leg, for good: the flag that says "the agent answered" was never
/// cleared when that call ended, and the adapter read it as "answer the next leg".
/// </remarks>
public sealed class SoftPhoneIncomingExtensionRingTests : SoftPhoneBrowserTest
{
    // Stands in for the Contact Center's accept endpoint, which a Contact Center offer's Answer posts to.
    private const string AcceptScript =
        """
        window.acceptTest = { accepts: 0 };
        (function () {
            var realFetch = window.fetch.bind(window);
            window.fetch = function (url, init) {
                if (String(url).indexOf('/accept') !== 0) {
                    return realFetch(url, init);
                }

                window.acceptTest.accepts++;
                return Promise.resolve({ ok: true, json: function () { return Promise.resolve({ succeeded: true, requiresDeviceAnswer: false }); } });
            };
        }());
        """;

    [Theory]
    [InlineData("ringLeg")]
    [InlineData("ringColleagueLeg")]
    public async Task ASecondAndThirdExtensionCall_Ring_AfterAnEarlierOneWasAnsweredAndEnded(string ring)
    {
        // Arrange - a colleague calls, the agent answers, and the colleague hangs up.
        var page = await OpenTelnyxPhoneAsync();
        await RingAndAnswerAsync(page, ring, "ext-in-1");
        await page.EvaluateAsync("() => window.fakeTelnyx.byLeg('ext-in-1').hangup()");
        await page.WaitForFunctionAsync("() => window.telephonySoftPhone.getInstance().getActiveCalls().length === 0");

        // Act / Assert - the next call rings, and so does the one after it.
        foreach (var leg in new[] { "ext-in-2", "ext-in-3" })
        {
            await RingAsync(page, ring, leg);
            await page.Locator("[data-telephony-incoming]").WaitForAsync();
            await page.WaitForTimeoutAsync(300);
            Assert.Equal(0, await AnswersAsync(page, leg));
            Assert.True(await page.Locator("[data-telephony-incoming-answer]").IsVisibleAsync());
            await CaptureAsync(page, "extension-call-rings-again-" + leg);

            await page.ClickAsync("[data-telephony-incoming-answer]");
            await page.WaitForFunctionAsync("leg => window.fakeTelnyx.byLeg(leg).answers === 1", leg);
            await page.EvaluateAsync("leg => window.fakeTelnyx.byLeg(leg).hangup()", leg);
            await page.WaitForFunctionAsync("() => window.telephonySoftPhone.getInstance().getActiveCalls().length === 0");
        }
    }

    [Fact]
    public async Task AColleaguesRing_DuringAnAcceptedContactCenterCall_IsNeverAnsweredByItself()
    {
        // Arrange - a Contact Center offer is accepted, and its leg (untagged) reaches the browser after the accept: it is
        // the one leg the phone expects, and is answered without ringing.
        var page = await OpenTelnyxPhoneAsync(AcceptScript);
        await SetIncomingOfferAsync(page, "cc-call", "+17025550101", "res-1");
        await page.ClickAsync("[data-telephony-incoming-answer]");
        await page.WaitForFunctionAsync("() => window.acceptTest.accepts === 1");
        await page.EvaluateAsync("() => window.fakeTelnyx.ringLeg('cc-leg')");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.byLeg('cc-leg').answers === 1");

        // Act - a colleague calls while the agent is on that call, with a plain leg and with a tagged one.
        await page.EvaluateAsync("() => window.fakeTelnyx.ringLeg('colleague-1')");
        await page.EvaluateAsync("() => window.fakeTelnyx.ringColleagueLeg('colleague-2')");
        await page.WaitForTimeoutAsync(400);

        // Assert - neither was answered; the phone turned them away rather than dropping them into the call.
        Assert.Equal(0, await AnswersAsync(page, "colleague-1"));
        Assert.Equal(0, await AnswersAsync(page, "colleague-2"));
    }

    [Fact]
    public async Task AnExtensionDialThatFails_LeavesNothingExpected_SoTheNextCallRings()
    {
        // Arrange - the harness refuses every extension call, the way a busy or offline colleague's is refused.
        var page = await OpenTelnyxPhoneAsync();
        await page.ClickAsync("[data-telephony-dial-mode-toggle]");
        await page.FillAsync("[data-telephony-number]", "2");
        await page.ClickAsync("[data-telephony-dial]");
        await WaitForAsync(() => Server.Provider.GetExtensionDialCount() == 1);
        await page.WaitForTimeoutAsync(300);

        // Act
        await page.EvaluateAsync("() => window.fakeTelnyx.ringLeg('ext-in-1')");

        // Assert
        await page.Locator("[data-telephony-incoming]").WaitForAsync();
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(0, await AnswersAsync(page, "ext-in-1"));
    }

    private static async Task RingAsync(IPage page, string ring, string leg)
        => await page.EvaluateAsync("([ring, leg]) => window.fakeTelnyx[ring](leg)", new[] { ring, leg });

    private static async Task RingAndAnswerAsync(IPage page, string ring, string leg)
    {
        await RingAsync(page, ring, leg);
        await page.Locator("[data-telephony-incoming]").WaitForAsync();
        Assert.Equal(0, await AnswersAsync(page, leg));
        await page.ClickAsync("[data-telephony-incoming-answer]");
        await page.WaitForFunctionAsync("leg => window.fakeTelnyx.byLeg(leg).state === 'active'", leg);
    }

    private static Task<int> AnswersAsync(IPage page, string leg)
        => page.EvaluateAsync<int>("leg => (window.fakeTelnyx.byLeg(leg) || { answers: -1 }).answers", leg);

    private async Task<IPage> OpenTelnyxPhoneAsync(string extraScript = null)
    {
        Server.Provider.BrowserMediaAdapterName = "telnyx-webrtc";
        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = DesktopAppViewport });
        await page.AddInitScriptAsync(FakeTelnyxSdk.Script);

        if (extraScript is not null)
        {
            await page.AddInitScriptAsync(extraScript);
        }

        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true&styled");
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.WaitForFunctionAsync("() => window.fakeTelnyx.ready", null, new PageWaitForFunctionOptions { Timeout = 5000 });

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
