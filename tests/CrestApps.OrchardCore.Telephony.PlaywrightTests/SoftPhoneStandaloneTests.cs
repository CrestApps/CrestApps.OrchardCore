using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// End-to-end tests for the standalone <c>/softphone</c> page hosted by the CrestApps Soft Phone browser
/// extension. They drive the real soft phone client, wrapped exactly as the standalone view wraps it
/// (<c>data-softphone-embedded</c> plus an optional <c>data-softphone-answer-call-id</c>), and verify the
/// embedded rendering and the one-shot "answer from the OS notification" handoff.
/// </summary>
public sealed class SoftPhoneStandaloneTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task Embedded_OpensThePanelOnLoad_WithoutAToggleClick()
    {
        // Arrange & Act - load the phone the way the standalone page renders it, with no interaction.
        var page = await Browser.NewPageAsync();
        await page.GotoAsync(Server.BaseUrl + "?embedded=true");
        await WaitForConnectedAsync(page);

        // Assert - the floating panel is expanded on load, because the standalone page renders the phone full
        // window rather than as a collapsed bubble the agent must click open.
        Assert.True(await page.Locator("[data-telephony-panel]").IsVisibleAsync());
    }

    [Fact]
    public async Task Embedded_WithMatchingAnswerCallId_AutoAnswersThatOffer()
    {
        // Arrange - the agent answered this exact call from the OS notification while the window was closed, so
        // the extension opened /softphone?answerCallId=call-answer-1.
        var page = await Browser.NewPageAsync();
        await page.GotoAsync(Server.BaseUrl + "?embedded=true&answerCallId=call-answer-1");
        await WaitForConnectedAsync(page);
        await RecordAcceptFetchAsync(page);

        // Act - surface the matching offer exactly as a live IncomingCall push or a Contact Center offer restore
        // would (both funnel through setIncomingOffer).
        await SetIncomingOfferAsync(page, "call-answer-1");

        // Assert - the client answered it on its own: it accepted the reservation without the agent clicking
        // Answer, and the ringing prompt cleared.
        await page.WaitForFunctionAsync("() => window.__acceptCalls.length > 0");
        await page.Locator("[data-telephony-incoming]")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }

    [Fact]
    public async Task Embedded_WithMatchingAnswerCallId_DoesNotAnswerADifferentOffer()
    {
        // Arrange - the handoff named call-answer-1, but a different call is what rings.
        var page = await Browser.NewPageAsync();
        await page.GotoAsync(Server.BaseUrl + "?embedded=true&answerCallId=call-answer-1");
        await WaitForConnectedAsync(page);
        await RecordAcceptFetchAsync(page);

        // Act
        await SetIncomingOfferAsync(page, "call-different-2");

        // Assert - a stale/unrelated call is never auto-answered: no accept was posted and the ringing prompt
        // stays up for the agent to decide.
        await page.Locator("[data-telephony-incoming]")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Equal("Ringing...", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__acceptCalls.length"));
    }

    private static async Task RecordAcceptFetchAsync(IPage page)
    {
        // Record every lifecycle POST (the Answer/accept round-trip) and answer it as succeeded, so a fired
        // auto-answer is observable and never blocks on a real endpoint.
        await page.EvaluateAsync(
            """
            () => {
                window.__acceptCalls = [];
                window.fetch = async (url) => {
                    window.__acceptCalls.push(String(url));
                    return { ok: true, json: async () => ({ succeeded: true, requiresDeviceAnswer: false }) };
                };
            }
            """);
    }

    private static async Task SetIncomingOfferAsync(IPage page, string callId)
    {
        await page.EvaluateAsync(
            """
            ([callId]) => window.telephonySoftPhone.getInstance().setIncomingOffer(
                {
                    callId,
                    from: '+15550001000',
                    direction: 'Inbound',
                    state: 'Ringing',
                    providerName: 'InMemory'
                },
                {
                    properties: {
                        acceptUrl: '/accept',
                        reservationId: 'res-' + callId
                    }
                })
            """,
            new[] { callId });
    }
}
