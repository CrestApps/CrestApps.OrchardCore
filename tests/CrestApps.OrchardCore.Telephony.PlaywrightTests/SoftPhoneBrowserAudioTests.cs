using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the real soft phone bundle in browser-audio mode against a stand-in media adapter, with Chromium's fake
/// capture device as the microphone.
/// </summary>
public sealed class SoftPhoneBrowserAudioTests : SoftPhoneBrowserTest
{
    // Counts microphone requests while still handing the phone the real (fake-device) capture it needs.
    private const string CountMicrophoneRequestsScript =
        """
        window.browserAudioState = { getUserMediaCount: 0, handledStates: [], decisions: [] };
        (function () {
            var media = navigator.mediaDevices;
            var getUserMedia = media.getUserMedia.bind(media);
            media.getUserMedia = function (constraints) {
                window.browserAudioState.getUserMediaCount++;
                return getUserMedia(constraints);
            };
        }());
        """;

    [Fact]
    public async Task BrowserAudio_DialInitializesAdapterAndMicrophone()
    {
        // Arrange
        var page = await Browser.NewPageAsync();
        await page.AddInitScriptAsync(CountMicrophoneRequestsScript);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true");
        await WaitForConnectedAsync(page);
        await page.EvaluateAsync(
            """
            () => {
                window.telephonySoftPhone.getInstance().registerMediaAdapter('in-memory', function (context) {
                    window.browserAudioState.localStream = context.localStream;
                    return {
                        handleCallState: function (call) {
                            window.browserAudioState.handledStates.push(call ? call.state : null);
                        },
                        dispose: function () {
                            window.browserAudioState.disposed = true;
                        }
                    };
                });
            }
            """);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");

        // Act
        await page.ClickAsync("[data-telephony-dial]");

        // Assert - the dial registers the adapter with a live captured track, and the microphone stays closed while
        // the call is only connecting.
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForFunctionAsync("() => window.browserAudioState.handledStates.length > 0");
        var connecting = await ReadMicrophoneAsync(page);
        Assert.Equal("live", connecting.GetProperty("readyState").GetString());
        Assert.False(connecting.GetProperty("enabled").GetBoolean(), "The microphone must stay closed until the call connects.");

        // Act
        await PublishLatestCallStateAsync(page);

        // Assert - once the call connects the microphone opens; it was requested exactly once.
        await page.WaitForFunctionAsync(
            "() => window.browserAudioState.localStream.getAudioTracks().some(track => track.enabled)");
        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.browserAudioState.getUserMediaCount"));
        Assert.True(await page.EvaluateAsync<bool>("() => !window.browserAudioState.disposed"));
    }

    [Fact]
    public async Task InboundAutoAnswer_WhenTheOfferWasAcceptedOutsideThePhone_AnswersTheRoutedLegOnceInsteadOfRingingIt()
    {
        // Arrange
        // A Contact Center offer can be accepted from the docked agent bar in the CRM chrome rather than from the
        // phone, and the platform then delivers the routed leg to this browser. Nothing about that accept is
        // pending inside the phone, so without arming, the arriving leg is treated as an unsolicited incoming
        // call and torn down -- the provider reports the refusal as busy and the agent is never connected, with
        // the customer left on a call nobody is on. Arming is one-shot so a later, genuine incoming call still
        // rings the agent instead of being answered silently.
        var page = await Browser.NewPageAsync();
        await page.AddInitScriptAsync(CountMicrophoneRequestsScript);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true");
        await WaitForConnectedAsync(page);
        await page.EvaluateAsync(
            """
            () => {
                window.telephonySoftPhone.getInstance().registerMediaAdapter('in-memory', function (context) {
                    window.browserAudioState.shouldAutoAnswerInbound = context.shouldAutoAnswerInbound;
                    return {
                        handleCallState: function () { },
                        dispose: function () { }
                    };
                });
            }
            """);
        await page.ClickAsync("[data-telephony-toggle]");

        // The offer rings on the phone too (which registers it), and is accepted from the agent bar.
        await SetIncomingOfferAsync(page, "cc-call-bar", "+15551234567", "res-bar");
        await page.WaitForFunctionAsync("() => typeof window.browserAudioState.shouldAutoAnswerInbound === 'function'");

        // Act
        await page.EvaluateAsync(
            """
            () => {
                var api = window.telephonySoftPhone.getInstance();
                var state = window.browserAudioState;

                // Consume any window already armed, so the decisions below describe only the Contact Center accept
                // this test is about.
                state.shouldAutoAnswerInbound();

                state.decisions.push(state.shouldAutoAnswerInbound());
                api.armInboundAutoAnswer('res-bar');
                state.decisions.push(state.shouldAutoAnswerInbound());
                state.decisions.push(state.shouldAutoAnswerInbound());

                // Another agent's accept, which the Contact Center also tells queues and supervisors about.
                api.armInboundAutoAnswer('someone-elses-offer');
                state.decisions.push(state.shouldAutoAnswerInbound());
            }
            """);

        // Assert
        var decisions = await page.EvaluateAsync<JsonElement>("() => window.browserAudioState.decisions");

        Assert.Equal(4, decisions.GetArrayLength());
        Assert.False(decisions[0].GetBoolean(), "An unsolicited inbound leg must ring, not be answered.");
        Assert.True(decisions[1].GetBoolean(), "The leg for an offer accepted outside the phone must be answered.");
        Assert.False(decisions[2].GetBoolean(), "Arming must be one-shot so a later genuine call still rings.");
        Assert.False(decisions[3].GetBoolean(), "An offer this phone was never offered must not arm it.");
    }

    [Fact]
    public async Task BrowserAudio_WhenAdapterIsNotRegistered_FailsClosedWithoutAcquiringTheMicrophone()
    {
        // Arrange
        var page = await Browser.NewPageAsync();
        await page.AddInitScriptAsync(
            """
            window.browserAudioState = { getUserMediaCount: 0 };
            Object.defineProperty(navigator, 'mediaDevices', {
                configurable: true,
                value: {
                    getUserMedia: function () {
                        window.browserAudioState.getUserMediaCount++;
                        return Promise.reject(new Error('The microphone must never be requested.'));
                    }
                }
            });
            """);
        await page.GotoAsync(Server.BaseUrl + "?browserAudio=true");
        await WaitForConnectedAsync(page);

        // No adapter is registered for the configured 'in-memory' name.
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");

        // Act
        await page.ClickAsync("[data-telephony-dial]");

        // Assert - the widget surfaces the unavailable adapter and never reaches the microphone.
        await page.Locator("[data-telephony-error]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var error = await page.Locator("[data-telephony-error]").InnerTextAsync();

        Assert.Equal("The browser audio adapter is unavailable.", error.Trim());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.browserAudioState.getUserMediaCount"));
    }

    private static async Task<JsonElement> ReadMicrophoneAsync(IPage page)
    {
        return await page.EvaluateAsync<JsonElement>(
            """
            () => {
                var track = window.browserAudioState.localStream.getAudioTracks()[0];
                return { readyState: track.readyState, enabled: track.enabled };
            }
            """);
    }
}
