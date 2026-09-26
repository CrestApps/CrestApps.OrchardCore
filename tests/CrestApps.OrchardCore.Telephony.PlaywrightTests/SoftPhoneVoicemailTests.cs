using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the real soft phone bundle through the Voicemail tab's bulk delete.
/// </summary>
public sealed class SoftPhoneVoicemailTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task BulkDelete_WhenTheServerRefusesOne_ReportsItAndKeepsItSelected()
    {
        // Arrange
        // The incident: the agent selected every voicemail and pressed Delete. The server refused one with a redirect
        // the browser followed to a page that answered 200, and the phone reloaded the list without a word, leaving
        // that voicemail behind as if the click had missed it.
        Server.VoicemailInbox.Add("vm-1", "+15550000001");
        Server.VoicemailInbox.Add("vm-2", "+15550000002", refuseWithRedirect: true);
        Server.VoicemailInbox.Add("vm-3", "+15550000003");

        var page = await OpenAsync("?voicemail");
        await page.ClickAsync("[data-telephony-toggle]");
        await page.ClickAsync("[data-telephony-tab=\"voicemail\"]");

        var rows = page.Locator("[data-telephony-voicemail-list] .telephony-soft-phone__voicemail-item");
        await Assertions.Expect(rows).ToHaveCountAsync(3);

        // Act
        await page.CheckAsync("[data-telephony-voicemail-select-all]");
        await page.ClickAsync("[data-telephony-voicemail-delete]");

        // Assert
        await Assertions.Expect(rows).ToHaveCountAsync(1);

        var remaining = rows.First;
        Assert.Equal("vm-2", await remaining.Locator("[data-telephony-voicemail-select]").GetAttributeAsync("data-telephony-voicemail-id"));
        Assert.True(await remaining.Locator("[data-telephony-voicemail-select]").IsCheckedAsync());
        await Assertions.Expect(remaining).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("voicemail-item--delete-failed"));
        await Assertions.Expect(remaining.Locator(".telephony-soft-phone__voicemail-error")).ToContainTextAsync("refused");

        var summary = page.Locator("[data-telephony-voicemail-error]");
        await Assertions.Expect(summary).ToBeVisibleAsync();
        await Assertions.Expect(summary).ToContainTextAsync("1 of 3 voicemails could not be deleted");
        Assert.True(await page.Locator("[data-telephony-voicemail-delete]").IsEnabledAsync());

        // One delete at a time, so a single inbox is never written by several at once.
        Assert.Equal(1, Server.VoicemailInbox.MaxConcurrentDeletes);
    }

    [Fact]
    public async Task BulkDelete_WhenEveryVoicemailIsDeleted_ShowsNoError()
    {
        // Arrange
        Server.VoicemailInbox.Add("vm-1", "+15550000001");
        Server.VoicemailInbox.Add("vm-2", "+15550000002");

        var page = await OpenAsync("?voicemail");
        await page.ClickAsync("[data-telephony-toggle]");
        await page.ClickAsync("[data-telephony-tab=\"voicemail\"]");
        await Assertions.Expect(page.Locator("[data-telephony-voicemail-list] .telephony-soft-phone__voicemail-item")).ToHaveCountAsync(2);

        // Act
        await page.CheckAsync("[data-telephony-voicemail-select-all]");
        await page.ClickAsync("[data-telephony-voicemail-delete]");

        // Assert
        await Assertions.Expect(page.Locator("[data-telephony-voicemail-list] .telephony-soft-phone__voicemail-item")).ToHaveCountAsync(0);
        Assert.True(await page.Locator("[data-telephony-voicemail-error]").IsHiddenAsync());
        Assert.Empty(Server.VoicemailInbox.List());
    }
}
