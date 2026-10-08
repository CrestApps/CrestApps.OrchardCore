using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the real soft phone bundle through Contact Center offers: ringing, answering, and an offer that is
/// revoked or repeated while the call it describes is already up.
/// </summary>
public sealed class SoftPhoneInboundOfferTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task RingingInboundCall_DoesNotShowHangup_UntilConnected()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await SetIncomingOfferAsync(page, "call-inbound-1", "+15550001000", "res-1");
        await page.EvaluateAsync(
            """
            () => {
                window.fetch = async () => ({
                    ok: true,
                    json: async () => ({ succeeded: true, requiresDeviceAnswer: false })
                });
            }
            """);

        // Assert - ringing should not expose hangup yet, and a ringing call has no running time.
        await page.Locator("[data-telephony-incoming]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
        await AssertStatusAsync(page, "Ringing...");

        // Act
        await page.ClickAsync("[data-telephony-incoming-answer]");
        await PublishCallStateAsync(page, "call-inbound-1", "+15550001000");

        // Assert - once the provider event arrives, the call is live.
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await AssertTimedStatusAsync(page, "In call");
        Assert.Equal("+1 (555) 000-1000", await page.Locator("[data-telephony-number]").InputValueAsync());
        Assert.True(await page.Locator("[data-telephony-incoming]").IsHiddenAsync());
    }

    // A call that is already up on this phone cannot start ringing again. A late or repeated offer for it -- the
    // Contact Center describing the call from an interaction it has not moved past ringing -- used to reopen the
    // ringing panel over the live call and hide its hangup.
    [Fact]
    public async Task IncomingOffer_ForACallAlreadyConnectedHere_KeepsTheCallInsteadOfRingingAgain()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await PublishCallStateAsync(page, "call-inbound-pending", "+15550001001");
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Act
        await SetIncomingOfferAsync(page, "call-inbound-pending", "+15550001001", "res-pending");
        await page.WaitForTimeoutAsync(200);

        // Assert
        Assert.True(await page.Locator("[data-telephony-incoming]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-hangup]").IsVisibleAsync());
        await AssertTimedStatusAsync(page, "In call");
        Assert.Equal("+1 (555) 000-1001", await page.Locator("[data-telephony-number]").InputValueAsync());
    }

    [Fact]
    public async Task AcceptedInboundOffer_RemainsActive_WhenOfferIsRevokedDuringAccept()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await SetIncomingOfferAsync(page, "call-inbound-2", "+15550001000", "res-2");
        await page.EvaluateAsync(
            """
            () => {
                window.__completeInboundAccept = null;
                window.fetch = () => new Promise(resolve => {
                    window.__completeInboundAccept = () => resolve({
                        ok: true,
                        json: async () => ({ succeeded: true, requiresDeviceAnswer: false })
                    });
                });
            }
            """);

        // Act
        await page.ClickAsync("[data-telephony-incoming-answer]");
        await page.WaitForFunctionAsync("() => typeof window.__completeInboundAccept === 'function'");
        await page.EvaluateAsync(
            """
            () => {
                const api = window.telephonySoftPhone.getInstance();
                api.clearIncomingOffer({ preserveCurrentCall: true, preservePendingAccept: true });
                window.__completeInboundAccept();
            }
            """);
        await PublishCallStateAsync(page, "call-inbound-2", "+15550001000");

        // Assert
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await AssertTimedStatusAsync(page, "In call");
        Assert.Equal("+1 (555) 000-1000", await page.Locator("[data-telephony-number]").InputValueAsync());
    }
}
