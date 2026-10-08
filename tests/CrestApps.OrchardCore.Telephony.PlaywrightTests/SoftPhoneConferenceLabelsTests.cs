using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// What the active-call list calls each line, and which lines it offers to merge.
/// </summary>
/// <remarks>
/// <para>
/// Live, a conference row read "v3:UyLVvJ3o7qQklQFQnZpvgJylmV..." -- the provider's id for the agent's own leg -- where
/// the dialed number belonged. The provider's later report of the call named nobody, and the row fell back to the id.
/// </para>
/// <para>
/// Live, Merge was pressed while extension 2 was still ringing, and Telnyx refused it half way. A line still connecting or
/// ringing is not offered for a merge; its checkbox is disabled and the list says why.
/// </para>
/// </remarks>
public sealed class SoftPhoneConferenceLabelsTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task AConferenceRow_KeepsItsNumber_WhenTheProvidersReportNamesNobody()
    {
        // Arrange
        Server.Provider.KeepNoConferenceState();
        var page = await OpenAsync("?widget&embedded", DesktopAppViewport);
        await DialAndConnectAsync(page, "+15551234567");
        var first = await GetCurrentCallIdAsync(page);
        await HoldAsync(page);
        await DialAndConnectAsync(page, "+15557654321");
        var second = await GetCurrentCallIdAsync(page);
        await page.Locator("[data-telephony-merge-select-all]").CheckAsync();
        await page.ClickAsync("[data-telephony-merge-calls]");
        await page.Locator("[data-telephony-conference]").WaitForAsync();

        // Act - the provider reports each call again, with neither number.
        foreach (var callId in new[] { first, second })
        {
            await page.EvaluateAsync(
                """
                callId => window.telephonySoftPhone.getInstance().getConnection().invoke('PublishCallState', {
                    callId, direction: 0, state: 3, providerName: 'InMemory'
                })
                """,
                callId);
        }

        await page.WaitForTimeoutAsync(300);

        // Assert
        var rows = await page.Locator("[data-telephony-conference-participant]").AllInnerTextsAsync();
        Assert.Contains(rows, row => row.Contains("(555) 123-4567", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("(555) 765-4321", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains(first, StringComparison.Ordinal) || row.Contains(second, StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains("conf-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARowWhoseCallNamesNobody_IsCalledAParticipant_NeverItsId()
    {
        // Arrange
        var page = await OpenAsync("?widget&embedded", DesktopAppViewport);
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);

        // Act - a call the phone has never seen a number for, identified only by the provider's id.
        await page.EvaluateAsync(
            """
            () => window.telephonySoftPhone.getInstance().getConnection().invoke('PublishCallState', {
                callId: 'v3:UyLVvJ3o7qQklQFQnZpvgJylmVeQF5xcRczOzXt9eWt8O2VOc5B7ig', direction: 0, state: 3, providerName: 'InMemory'
            })
            """);
        await page.Locator("[data-telephony-active-calls]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Assert
        var list = await page.Locator("[data-telephony-active-calls-list]").InnerTextAsync();
        Assert.Contains("Participant", list, StringComparison.Ordinal);
        Assert.DoesNotContain("v3:", list, StringComparison.Ordinal);
        Assert.DoesNotContain("v3:", await page.Locator("[data-telephony-number]").InputValueAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALineStillConnecting_IsNotOfferedForAMerge_AndTheListSaysWhy()
    {
        // Arrange - a call on hold, and a second one dialed that has not connected.
        var page = await OpenAsync("?widget&embedded", DesktopAppViewport);
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);
        await page.FillAsync("[data-telephony-number]", "+15557654321");
        await page.ClickAsync("[data-telephony-dial]");
        await page.WaitForFunctionAsync("() => window.telephonySoftPhone.getInstance().getActiveCalls().length === 2");
        var second = await GetCurrentCallIdAsync(page);

        // Act
        var waiting = page.Locator("[data-telephony-merge-waiting]");
        await waiting.WaitForAsync();

        // Assert
        var check = page.Locator($"[data-telephony-conference-call=\"{second}\"]");
        Assert.True(await check.IsDisabledAsync());
        Assert.Contains("answer", await check.GetAttributeAsync("title"), StringComparison.Ordinal);
        Assert.Contains("answer", await waiting.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(0, await page.Locator("[data-telephony-merge-calls]").CountAsync());
    }
}
