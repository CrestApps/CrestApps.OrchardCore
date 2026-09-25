using System.Text.Json;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the transfer panel on a Contact Center call, where the Contact Center carries the transfer: the panel lists
/// the agents with their presence and the queues with who is waiting, a blind transfer goes to the Contact Center's
/// transfer endpoint rather than the provider, and a warm transfer is a consult the agent completes or cancels.
/// </summary>
public sealed class SoftPhoneContactCenterTransferTests : SoftPhoneBrowserTest
{
    private const string InteractionId = "interaction-1";

    [Fact]
    public async Task TransferPanel_OnAContactCenterCall_ListsAgentsWithPresence_AndQueuesWithWhoIsWaiting()
    {
        var page = await OpenOnAContactCenterCallAsync("?transferService&styled");

        await page.ClickAsync("[data-telephony-transfer]");

        var bea = page.Locator("[data-telephony-directory-destination=\"agent:agent-bea\"]");
        await bea.WaitForAsync();
        Assert.Contains("Bea Baker", await bea.InnerTextAsync());
        Assert.Contains("Ext 201", await bea.InnerTextAsync());
        Assert.Equal("Available", await bea.GetAttributeAsync("data-telephony-presence"));

        // A colleague who cannot take a call is listed, so the agent sees why, but cannot be picked.
        var cal = page.Locator("[data-telephony-directory-destination=\"agent:agent-cal\"]");
        Assert.Equal("Busy", await cal.GetAttributeAsync("data-telephony-presence"));
        Assert.Equal("true", await cal.GetAttributeAsync("aria-disabled"));

        var sales = page.Locator("[data-telephony-directory-destination=\"queue:queue-sales\"]");
        Assert.Contains("Sales", await sales.InnerTextAsync());
        Assert.Contains("3 waiting", await sales.InnerTextAsync());
        Assert.Equal(1, await page.Locator("[data-telephony-directory-destination=\"external:dest-billing\"]").CountAsync());

        // Asked for this call's directory, not the provider's.
        var commands = await CommandsAsync(page);
        Assert.Contains(commands, command => command.GetProperty("kind").GetString() == "targets" &&
            command.GetProperty("interactionId").GetString() == InteractionId);
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount')"));

        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth"));
        await CaptureAsync(page, "cc-transfer-directory");
    }

    [Fact]
    public async Task BlindTransfer_ToAnAgent_GoesThroughTheContactCenter_NotTheProvider()
    {
        var page = await OpenOnAContactCenterCallAsync("?transferService");
        await page.ClickAsync("[data-telephony-transfer]");
        await page.ClickAsync("[data-telephony-directory-destination=\"agent:agent-bea\"]");

        await page.ClickAsync("[data-telephony-transfer-confirm]");

        var transfer = await WaitForCommandAsync(page, "transfer");
        Assert.Equal(InteractionId, transfer.GetProperty("interactionId").GetString());
        Assert.Equal("agent", transfer.GetProperty("targetType").GetString());
        Assert.Equal("agent-bea", transfer.GetProperty("targetId").GetString());
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-transfer-panel]').hidden");
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount')"));
    }

    [Fact]
    public async Task Transfer_ToAnAgentWhoIsBusy_IsRefusedInThePanel()
    {
        var page = await OpenOnAContactCenterCallAsync("?transferService");
        await page.ClickAsync("[data-telephony-transfer]");
        var cal = page.Locator("[data-telephony-directory-destination=\"agent:agent-cal\"]");
        await cal.WaitForAsync();

        // The entry reads as disabled, so an agent cannot pick it; forced, the panel still refuses to send the call.
        Assert.False(await cal.IsEnabledAsync());
        await cal.ClickAsync(new LocatorClickOptions { Force = true });
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        var error = page.Locator("[data-telephony-transfer-error]");
        await error.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.False(string.IsNullOrWhiteSpace(await error.InnerTextAsync()));
        Assert.DoesNotContain(await CommandsAsync(page), command => command.GetProperty("kind").GetString() == "transfer");
    }

    [Fact]
    public async Task WarmTransfer_ConsultsTheAgent_AndCompletesOnceTheyAnswer()
    {
        var page = await OpenOnAContactCenterCallAsync("?transferService&styled");
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"agent:agent-bea\"]").WaitForAsync();

        await page.ClickAsync("[data-telephony-transfer-mode=\"warm\"]");
        await page.ClickAsync("[data-telephony-directory-destination=\"agent:agent-bea\"]");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        var start = await WaitForCommandAsync(page, "consult-start");
        Assert.Equal("agent-bea", start.GetProperty("targetId").GetString());

        // While Bea's phone rings the agent can take the caller back, but not hand them over.
        var consult = page.Locator("[data-telephony-consult]");
        await consult.WaitForAsync();
        Assert.Contains("Calling Bea Baker", await page.Locator("[data-telephony-consult-status]").InnerTextAsync());
        Assert.True(await page.Locator("[data-telephony-consult-complete]").IsDisabledAsync());
        Assert.True(await page.Locator("[data-telephony-consult-cancel]").IsEnabledAsync());
        await CaptureAsync(page, "cc-consult-ringing");

        // Bea answers.
        await SetConsultAsync(page, "connected");
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-telephony-consult-complete]').disabled");
        Assert.Contains("Talking to Bea Baker", await page.Locator("[data-telephony-consult-status]").InnerTextAsync());
        await CaptureAsync(page, "cc-consult-connected");

        await page.ClickAsync("[data-telephony-consult-complete]");

        var complete = await WaitForCommandAsync(page, "consult-complete");
        Assert.Equal("consult-1", complete.GetProperty("consultId").GetString());
        Assert.Equal(InteractionId, complete.GetProperty("interactionId").GetString());
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-transfer-panel]').hidden");
    }

    [Fact]
    public async Task WarmTransfer_Cancel_DropsTheConsult_AndReturnsToTheCall()
    {
        var page = await OpenOnAContactCenterCallAsync("?transferService");
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"agent:agent-bea\"]").WaitForAsync();
        await page.ClickAsync("[data-telephony-transfer-mode=\"warm\"]");
        await page.ClickAsync("[data-telephony-directory-destination=\"agent:agent-bea\"]");
        await page.ClickAsync("[data-telephony-transfer-confirm]");
        await page.Locator("[data-telephony-consult]").WaitForAsync();
        await SetConsultAsync(page, "connected");
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-telephony-consult-complete]').disabled");

        await page.ClickAsync("[data-telephony-consult-cancel]");

        await WaitForCommandAsync(page, "consult-cancel");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-transfer-panel]').hidden");
        Assert.DoesNotContain(await CommandsAsync(page), command => command.GetProperty("kind").GetString() == "consult-complete");
    }

    [Fact]
    public async Task WarmTransfer_WhenTheColleagueHangsUp_SaysTheCallerIsBack()
    {
        var page = await OpenOnAContactCenterCallAsync("?transferService");
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"agent:agent-bea\"]").WaitForAsync();
        await page.ClickAsync("[data-telephony-transfer-mode=\"warm\"]");
        await page.ClickAsync("[data-telephony-directory-destination=\"agent:agent-bea\"]");
        await page.ClickAsync("[data-telephony-transfer-confirm]");
        await page.Locator("[data-telephony-consult]").WaitForAsync();

        await SetConsultAsync(page, "cancelled");

        await page.WaitForFunctionAsync("() => (document.querySelector('[data-telephony-consult-status]') || {}).textContent?.includes('back with you')");
        Assert.True(await page.Locator("[data-telephony-consult-complete]").IsDisabledAsync());
        Assert.True(await page.Locator("[data-telephony-consult-done]").IsVisibleAsync());
    }

    [Fact]
    public async Task TransferPanel_OnACallWithNoInteraction_StillUsesTheProvidersDirectory()
    {
        var page = await OpenAsync("?transferService", DesktopAppViewport);
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");

        await page.ClickAsync("[data-telephony-transfer]");

        await page.Locator("[data-telephony-directory-destination=\"2001\"]").WaitForAsync();
        Assert.DoesNotContain(await CommandsAsync(page), command => command.GetProperty("kind").GetString() == "targets");
    }

    // Live: on a Contact Center call over Telnyx, the transfer panel offered only the phone system's extensions, and a
    // search for a queue found nobody. The phone reads its calls again every few seconds, and the provider's report of
    // the call names no interaction; that report replaced the Contact Center's, the call no longer named its
    // interaction, and the panel took the provider's path.
    [Fact]
    public async Task TransferPanel_OnAContactCenterCall_KeepsTheContactCentersDirectory_AfterTheProviderReportsTheCallAgain()
    {
        // Arrange - a provider with no directory of its own, whose phone system has extensions, as Telnyx.
        Server.Provider.RemoveDirectory();
        var page = await OpenOnAContactCenterCallAsync("?transferService&styled");

        // The provider's own report of the call, which knows nothing of the Contact Center.
        await page.EvaluateAsync(
            """
            () => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                { callId: 'cc-call-1', from: '+15557000001', direction: 1, state: 3, providerName: 'InMemory' })
            """);
        await page.WaitForTimeoutAsync(200);

        // Act
        await page.ClickAsync("[data-telephony-transfer]");

        // Assert - the agents and queues, from this call's interaction, and a transfer through the Contact Center.
        var bea = page.Locator("[data-telephony-directory-destination=\"agent:agent-bea\"]");
        await bea.WaitForAsync();
        Assert.Equal(1, await page.Locator("[data-telephony-directory-destination=\"queue:queue-sales\"]").CountAsync());
        Assert.Contains(await CommandsAsync(page), command => command.GetProperty("kind").GetString() == "targets" &&
            command.GetProperty("interactionId").GetString() == InteractionId);
        await CaptureAsync(page, "cc-transfer-directory-after-provider-report");

        await bea.ClickAsync();
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        var transfer = await WaitForCommandAsync(page, "transfer");
        Assert.Equal(InteractionId, transfer.GetProperty("interactionId").GetString());
        Assert.Equal("agent-bea", transfer.GetProperty("targetId").GetString());
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount')"));
    }

    private async Task<IPage> OpenOnAContactCenterCallAsync(string query)
    {
        var page = await OpenAsync(query, DesktopAppViewport);
        page.Dialog += async (_, dialog) => await dialog.DismissAsync();

        await page.ClickAsync("[data-telephony-toggle]");

        // A connected inbound call the Contact Center routed to this agent: it names its interaction.
        await page.EvaluateAsync(
            """
            (interactionId) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                {
                    callId: 'cc-call-1',
                    from: '+15557000001',
                    direction: 1,
                    state: 3,
                    providerName: 'InMemory',
                    metadata: { interactionId }
                })
            """,
            InteractionId);

        await page.Locator("[data-telephony-transfer]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        return page;
    }

    private static async Task SetConsultAsync(IPage page, string status)
        => await page.EvaluateAsync("(status) => fetch('/test/consult/' + status, { method: 'POST' }).then(() => true)", status);

    private static async Task<JsonElement[]> CommandsAsync(IPage page)
        => (await page.EvaluateAsync<JsonElement>("() => fetch('/test/transfer-commands').then(response => response.json())"))
            .EnumerateArray()
            .ToArray();

    private static async Task<JsonElement> WaitForCommandAsync(IPage page, string kind)
    {
        await page.WaitForFunctionAsync(
            "(kind) => fetch('/test/transfer-commands').then(response => response.json()).then(commands => commands.some(command => command.kind === kind))",
            kind);

        return (await CommandsAsync(page)).Last(command => command.GetProperty("kind").GetString() == kind);
    }
}
