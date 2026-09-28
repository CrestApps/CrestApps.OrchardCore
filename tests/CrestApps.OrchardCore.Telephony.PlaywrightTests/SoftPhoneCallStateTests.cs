using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the real soft phone bundle through provider reports that arrive late, out of order, or about a call the
/// phone no longer shows.
/// </summary>
public sealed class SoftPhoneCallStateTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task RemoteProviderDisconnect_ImmediatelyClearsActiveCall()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");

        // Act
        await page.EvaluateAsync(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('DisconnectLatestCall')");

        // Assert
        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
        await AssertStatusAsync(page, "Ready");
    }

    [Fact]
    public async Task StaleDisconnectForPreviousCall_DoesNotClearCurrentCall()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");
        var previousCallId = await GetCurrentCallIdAsync(page);
        await page.EvaluateAsync(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('DisconnectLatestCall')");
        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await DialAndConnectAsync(page, "+15557654321");
        var currentCallId = await GetCurrentCallIdAsync(page);

        // Act
        await page.EvaluateAsync(
            """
            ([callId]) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                {
                    callId,
                    direction: 0,
                    state: 5,
                    providerName: 'InMemory'
                })
            """,
            new[] { previousCallId });

        // Assert
        Assert.NotEqual(previousCallId, currentCallId);
        Assert.Equal(currentCallId, await GetCurrentCallIdAsync(page));
        await AssertTimedStatusAsync(page, "In call");
        Assert.True(await page.Locator("[data-telephony-hangup]").IsVisibleAsync());
    }

    [Fact]
    public async Task TerminalEventForProviderCall_WhenClientHoldsDifferentStaleCall_RefreshesAndClearsState()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await page.EvaluateAsync(
            """
            async () => {
                const connection = window.telephonySoftPhone.getInstance().getConnection();
                await connection.invoke('Dial', { to: '+15551234567' });
                await connection.invoke(
                    'PublishCallState',
                    {
                        callId: 'stale-contact-center-call',
                        direction: 1,
                        state: 3,
                        providerName: 'InMemory'
                    });
            }
            """);
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Act
        await page.EvaluateAsync(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('DisconnectLatestCall')");

        // Assert
        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Null(await page.EvaluateAsync<object>(
            "() => window.telephonySoftPhone.getInstance().getCurrentCall()"));
        await AssertStatusAsync(page, "Ready");
    }

    [Fact]
    public async Task ProviderEventDuringActiveCallRestoration_WinsOverStaleLookup()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");
        var connection = "window.telephonySoftPhone.getInstance().getConnection()";
        var baselineLookupCount = await page.EvaluateAsync<int>(
            $"() => {connection}.invoke('GetCallLookupRequestCount')");
        await page.EvaluateAsync(
            $"() => {connection}.invoke('SetCallLookupDelay', 500)");

        await page.ReloadAsync();
        await WaitForConnectedAsync(page);
        await WaitForPromiseAsync(
            page,
            """
            async baseline => {
                const connection = window.telephonySoftPhone.getInstance().getConnection();
                return await connection.invoke('GetCallLookupRequestCount') > baseline;
            }
            """,
            baselineLookupCount);

        // Act
        await page.EvaluateAsync(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('DisconnectLatestCall')");

        // Assert
        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.WaitForTimeoutAsync(600);
        await AssertStatusAsync(page, "Ready");
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
    }
}
