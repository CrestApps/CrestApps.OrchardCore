using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// A conference after the merge that made it, on a provider that keeps no conference on its calls (Telnyx keeps none):
/// it stays one conference on screen, it is never merged a second time, and each participant is hung up on their own.
/// </summary>
/// <remarks>
/// Live, a merge worked, and a few seconds later the phone read its calls again: the provider's reports said nothing of
/// a conference, so the calls went back to separate lines, each with a checkbox, and Merge was offered once more. Pressed,
/// it asked the provider to join calls already in the conference, which it refused. And the conference had been made
/// from an extension call: that call's leg is the agent's own way into the conference, so hanging it up to drop the
/// colleague ended the conference for everybody.
/// </remarks>
public sealed class SoftPhoneConferenceFollowUpTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task AfterAMerge_TheCallsStayOneConference_WhenTheProviderReportsThemAgain_AndAreNotOfferedForMergeAgain()
    {
        // Arrange
        var (page, first, second) = await MergeTwoCallsAsync();

        // Act - the provider reports each call again, as it does on every refresh, with nothing of the conference.
        await ReportConnectedAsync(page, first);
        await ReportConnectedAsync(page, second);
        await page.WaitForTimeoutAsync(300);

        // Assert
        Assert.Equal(2, await page.Locator("[data-telephony-conference-participant]").CountAsync());
        Assert.Contains("2 participants", await page.Locator("[data-telephony-conference]").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await page.Locator("[data-telephony-conference-call]").CountAsync());
        Assert.Equal(0, await page.Locator("[data-telephony-merge-calls]").CountAsync());
        await CaptureAsync(page, "conference-after-refresh");

        // A merge asked for anyway changes nothing and asks the provider for nothing.
        await page.EvaluateAsync("() => window.telephonySoftPhone.getInstance().merge()");
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(1, Server.Provider.GetMergeRequestCount());
    }

    [Fact]
    public async Task HangingUpOneParticipant_DropsOnlyThatParty_AndTheLastOneEndsTheConference()
    {
        // Arrange - the conference was made from `primary`, whose leg is the agent's own way into it.
        var (page, first, second) = await MergeTwoCallsAsync();
        var primary = (await LastMergeAsync(page)).GetProperty("callIds")[0].GetString();
        var other = primary == first ? second : first;

        // Act - drop the conference's first party.
        await page.ClickAsync($"[data-telephony-participant-hangup=\"{primary}\"]");

        // Assert - that party alone: its leg stays up, the other party stays in the conference.
        await WaitForAsync(() => Server.Provider.HangupCommands.Contains($"{primary}:participant"));
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-telephony-conference-participant]').length === 1");
        Assert.Equal(1, await page.Locator($"[data-telephony-conference-participant=\"{other}\"]").CountAsync());
        Assert.Contains("1 participant", await page.Locator("[data-telephony-conference]").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Single(Server.Provider.HangupCommands);
        await ReportConnectedAsync(page, primary);
        await page.WaitForTimeoutAsync(200);
        Assert.Equal(0, await page.Locator($"[data-telephony-conference-participant=\"{primary}\"]").CountAsync());
        await CaptureAsync(page, "conference-one-participant-left");

        // Act - drop the last party.
        await page.ClickAsync($"[data-telephony-participant-hangup=\"{other}\"]");

        // Assert - with nobody left, the agent's leg into the conference is hung up too.
        await WaitForAsync(() => Server.Provider.HangupCommands.Contains($"{other}:participant") && Server.Provider.HangupCommands.Contains(primary));
        await page.WaitForFunctionAsync("() => window.telephonySoftPhone.getInstance().getActiveCalls().length === 0");
    }

    // Live, the agent merged a dialed number with extension 2 and pressed Hang up, and everybody was disconnected. Hang up
    // in a conference now leaves it: each of the agent's calls in it is hung up as leaving, and the parties stay connected.
    [Fact]
    public async Task HangingUpAConferenceTwoOthersAreIn_LeavesIt_AndTheOthersStayConnected()
    {
        // Arrange
        var (page, first, second) = await MergeTwoCallsAsync();
        Assert.Equal("Leave", await page.Locator("[data-telephony-hangup]").GetAttributeAsync("aria-label"));

        // Act
        await page.ClickAsync("[data-telephony-hangup]");

        // Assert
        await WaitForAsync(() => Server.Provider.HangupCommands.Contains($"{first}:leave") && Server.Provider.HangupCommands.Contains($"{second}:leave"));
        Assert.Equal(new[] { first, second }.Order(), Server.Provider.PartiesStillConnected.Keys.Order());
        Assert.DoesNotContain(Server.Provider.HangupCommands, command => command == first || command == second || command.EndsWith(":end", StringComparison.Ordinal));
        await page.WaitForFunctionAsync("() => window.telephonySoftPhone.getInstance().getActiveCalls().length === 0");
    }

    // With one party left, leaving would strand them alone in a conference: Hang up ends the call.
    [Fact]
    public async Task HangingUpAConferenceWithOnlyOnePartyLeft_EndsTheCall()
    {
        // Arrange - the conference's first party is dropped from its row.
        var (page, first, second) = await MergeTwoCallsAsync();
        var primary = (await LastMergeAsync(page)).GetProperty("callIds")[0].GetString();
        var other = primary == first ? second : first;
        await page.ClickAsync($"[data-telephony-participant-hangup=\"{primary}\"]");
        await WaitForAsync(() => Server.Provider.HangupCommands.Contains($"{primary}:participant"));
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-telephony-conference-participant]').length === 1");
        Assert.Equal("Hang up", await page.Locator("[data-telephony-hangup]").GetAttributeAsync("aria-label"));

        // Act
        await page.ClickAsync("[data-telephony-hangup]");

        // Assert
        await WaitForAsync(() => Server.Provider.HangupCommands.Contains(primary) && Server.Provider.HangupCommands.Contains(other));
        Assert.Empty(Server.Provider.PartiesStillConnected);
        Assert.DoesNotContain(Server.Provider.HangupCommands, command => command.EndsWith(":leave", StringComparison.Ordinal));
    }

    // Ending the conference for everyone is a separate, confirmed action; the confirmation starts on keeping the call.
    [Fact]
    public async Task EndForAll_AsksFirst_ThenEndsTheConferenceForEveryone()
    {
        // Arrange
        var (page, first, second) = await MergeTwoCallsAsync();
        var primary = (await LastMergeAsync(page)).GetProperty("callIds")[0].GetString();
        var endAll = page.Locator("[data-telephony-hangup-all]");
        Assert.Equal("End for all", await endAll.GetAttributeAsync("aria-label"));

        // Act - asked, and kept.
        await endAll.ClickAsync();
        var confirm = page.Locator("[data-telephony-confirm]");
        await confirm.WaitForAsync();
        Assert.Contains("All 2 participants", await confirm.InnerTextAsync());
        Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement && document.activeElement.hasAttribute('data-telephony-confirm-cancel')"));
        await CaptureAsync(page, "conference-end-for-all-confirm");
        await page.ClickAsync("[data-telephony-confirm-cancel]");
        await page.WaitForTimeoutAsync(200);

        // Assert - nothing was hung up.
        Assert.Empty(Server.Provider.HangupCommands);

        // Act - asked again, and confirmed.
        await endAll.ClickAsync();
        await page.ClickAsync("[data-telephony-confirm-accept]");

        // Assert - the conference is ended from the call it was made from, and every call is hung up.
        await WaitForAsync(() => Server.Provider.HangupCommands.Contains($"{primary}:end") && Server.Provider.HangupCommands.Contains(primary == first ? second : first));
        Assert.Empty(Server.Provider.PartiesStillConnected);
        await page.WaitForFunctionAsync("() => window.telephonySoftPhone.getInstance().getActiveCalls().length === 0");
    }

    [Fact]
    public async Task TheConferenceRows_NameEachParty_ByNumberOrByThePersonAnExtensionRings()
    {
        // Arrange - a call held, and an extension call up, as a Contact Center caller and a colleague would be.
        Server.Provider.KeepNoConferenceState();
        var page = await OpenAsync("?styled", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+17024993350");
        var cell = await GetCurrentCallIdAsync(page);
        await HoldAsync(page);
        await page.EvaluateAsync(
            """
            () => window.telephonySoftPhone.getInstance().getConnection().invoke('PublishTrackedCallState', {
                callId: 'ext-call-1', to: 'jdoe', direction: 0, state: 3, providerName: 'InMemory', startedUtc: new Date().toISOString(),
                metadata: { extensionNumber: '2' }
            })
            """);
        await page.Locator("[data-telephony-merge-select-all]").WaitForAsync();

        // Act
        await page.Locator("[data-telephony-merge-select-all]").CheckAsync();
        await page.ClickAsync("[data-telephony-merge-calls]");
        await page.Locator("[data-telephony-conference]").WaitForAsync();

        // Assert - every row says who it is, and its hang-up says whom it hangs up.
        var extensionRow = page.Locator("[data-telephony-conference-participant=\"ext-call-1\"]");
        var cellRow = page.Locator($"[data-telephony-conference-participant=\"{cell}\"]");
        Assert.Contains("Jane Doe · ext 2", await extensionRow.InnerTextAsync());
        Assert.Contains("(702) 499-3350", await cellRow.InnerTextAsync());
        Assert.Equal("Hang up Jane Doe · ext 2", await extensionRow.Locator("[data-telephony-participant-hangup]").GetAttributeAsync("aria-label"));
        Assert.Contains("499-3350", await cellRow.Locator("[data-telephony-participant-hangup]").GetAttributeAsync("aria-label"));
        await CaptureAsync(page, "conference-rows-named");
    }

    // Two calls up and merged, on a provider that keeps no conference on them.
    private async Task<(IPage Page, string First, string Second)> MergeTwoCallsAsync()
    {
        Server.Provider.KeepNoConferenceState();
        var page = await OpenAsync("?styled", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");
        var first = await GetCurrentCallIdAsync(page);
        await HoldAsync(page);
        await DialAndConnectAsync(page, "+15557654321");
        var second = await GetCurrentCallIdAsync(page);
        await page.Locator("[data-telephony-merge-select-all]").CheckAsync();
        await page.ClickAsync("[data-telephony-merge-calls]");
        await page.Locator("[data-telephony-conference]").WaitForAsync();
        await WaitForAsync(() => Server.Provider.GetMergeRequestCount() == 1);

        return (page, first, second);
    }

    private static async Task ReportConnectedAsync(IPage page, string callId)
        => await page.EvaluateAsync(
            """
            callId => window.telephonySoftPhone.getInstance().getConnection().invoke('PublishCallState', {
                callId, direction: 0, state: 3, providerName: 'InMemory'
            })
            """,
            callId);

    private static Task<JsonElement> LastMergeAsync(IPage page)
        => page.EvaluateAsync<JsonElement>("() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetLastMerge')");

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(100);
        }

        Assert.True(condition());
    }
}
