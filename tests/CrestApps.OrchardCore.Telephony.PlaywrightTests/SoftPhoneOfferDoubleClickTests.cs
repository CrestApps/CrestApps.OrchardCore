using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// An offer's Ignore and Voicemail act once per click the agent means: the buttons are disabled from the first click
/// until the request fails, and a click that lands on the next offer the instant it replaces the first is not taken as
/// the agent's answer to it.
/// </summary>
/// <remarks>
/// Live, Ignore stayed clickable for the second the decline took to return, and the second click of a double-click
/// posted a decline for the next reservation, which had just been offered.
/// </remarks>
public sealed class SoftPhoneOfferDoubleClickTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task DoubleClickingIgnore_DeclinesTheOfferOnce_AndDisablesTheButtonsMeanwhile()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await RecordLifecyclePostsAsync(page, delayMs: 800, succeeded: true);
        await SetOfferAsync(page, "call-a");

        // Act
        await page.DblClickAsync("[data-telephony-incoming-ignore]");

        // Assert
        Assert.True(await page.Locator("[data-telephony-incoming-ignore]").IsDisabledAsync());
        Assert.True(await page.Locator("[data-telephony-incoming-answer]").IsDisabledAsync());
        await page.WaitForTimeoutAsync(1200);
        Assert.Equal(["/decline/call-a"], await PostsAsync(page));
    }

    [Fact]
    public async Task ASecondClick_ThatLandsOnTheNextOffer_DoesNotDeclineIt()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await RecordLifecyclePostsAsync(page, delayMs: 800, succeeded: true);
        await SetOfferAsync(page, "call-a");

        // Act - the first click declines A; B is offered while that decline is in flight, and the second click lands on it.
        await page.ClickAsync("[data-telephony-incoming-ignore]");
        await SetOfferAsync(page, "call-b");
        await page.ClickAsync("[data-telephony-incoming-ignore]", new PageClickOptions { Force = true });
        await page.WaitForTimeoutAsync(1000);

        // Assert
        Assert.Equal(["/decline/call-a"], await PostsAsync(page));
        Assert.True(await page.Locator("[data-telephony-incoming]").IsVisibleAsync());

        // A click on B once the moment has passed is the agent's own, and declines it.
        await page.ClickAsync("[data-telephony-incoming-ignore]");
        await page.WaitForFunctionAsync("() => window.__posts.length === 2");
        Assert.Equal(["/decline/call-a", "/decline/call-b"], await PostsAsync(page));
    }

    [Fact]
    public async Task ADeclineThatFails_GivesTheButtonsBack()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await RecordLifecyclePostsAsync(page, delayMs: 200, succeeded: false);
        await SetOfferAsync(page, "call-a");

        // Act
        await page.ClickAsync("[data-telephony-incoming-ignore]");
        await page.WaitForFunctionAsync("() => window.__posts.length === 1");
        await page.Locator("[data-telephony-incoming-ignore]:enabled").WaitForAsync();

        // Assert - the agent can try again.
        await page.ClickAsync("[data-telephony-incoming-ignore]");
        await page.WaitForFunctionAsync("() => window.__posts.length === 2");
    }

    [Fact]
    public async Task DoubleClickingVoicemail_SendsTheOfferToVoicemailOnce()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await RecordLifecyclePostsAsync(page, delayMs: 800, succeeded: true);
        await SetOfferAsync(page, "call-a");

        // Act
        await page.DblClickAsync("[data-telephony-incoming-voicemail]");
        await page.WaitForTimeoutAsync(1200);

        // Assert
        Assert.Equal(["/voicemail/call-a"], await PostsAsync(page));
    }

    // Records every lifecycle POST and answers it after `delayMs`, as the Contact Center would.
    private static async Task RecordLifecyclePostsAsync(IPage page, int delayMs, bool succeeded)
        => await page.EvaluateAsync(
            """
            ([delayMs, succeeded]) => {
                window.__posts = [];
                window.fetch = async (url) => {
                    window.__posts.push(String(url));
                    await new Promise(resolve => setTimeout(resolve, delayMs));
                    return succeeded
                        ? { ok: true, json: async () => ({ succeeded: true }) }
                        : { ok: false, json: async () => ({ succeeded: false }) };
                };
            }
            """,
            new object[] { delayMs, succeeded });

    private static async Task SetOfferAsync(IPage page, string callId)
        => await page.EvaluateAsync(
            """
            callId => window.telephonySoftPhone.getInstance().setIncomingOffer(
                { callId, from: '+15550001000', direction: 'Inbound', state: 'Ringing', providerName: 'InMemory' },
                {
                    properties: {
                        acceptUrl: '/accept/' + callId,
                        declineUrl: '/decline/' + callId,
                        voicemailUrl: '/voicemail/' + callId,
                        reservationId: 'res-' + callId
                    }
                })
            """,
            callId);

    private static async Task<string[]> PostsAsync(IPage page)
        => await page.EvaluateAsync<string[]>("() => window.__posts.slice()");
}
