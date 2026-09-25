using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the real soft phone bundle through placing, holding, adding, conferencing, transferring and ending calls
/// against the test hub.
/// </summary>
public sealed class SoftPhoneWidgetTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task Dial_ThenHangup_TransitionsSoftPhoneUi()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");

        // Holds the dial's round trip open, so what the phone shows while it is in flight can be seen.
        await page.EvaluateAsync("() => window.telephonySoftPhone.getInstance().getConnection().invoke('SetDialDelay', 500)");

        // Act
        await page.ClickAsync("[data-telephony-dial]");

        // Assert - while the dial is in flight the phone says it is connecting at once, keeps the (now disabled) dial
        // button where it was, and offers no call controls: there is no call to control yet.
        await AssertStatusAsync(page, "Connecting...");
        Assert.True(await page.Locator("[data-telephony-dial]").IsVisibleAsync());
        Assert.True(await page.Locator("[data-telephony-dial]").IsDisabledAsync());
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());

        // Assert - the provider acknowledges the dial as connecting: the call can be cancelled, but there is no running
        // time and no hold or mute until the far end answers.
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await AssertStatusAsync(page, "Connecting...");
        Assert.True(await page.Locator("[data-telephony-dial]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-hold]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-mute]").IsHiddenAsync());

        // Act - the provider reports the call connected.
        await PublishLatestCallStateAsync(page);

        // Assert
        await page.Locator("[data-telephony-mute]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await AssertTimedStatusAsync(page, "In call");
        Assert.True(await page.Locator("[data-telephony-dial]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-hold]").IsVisibleAsync());
        Assert.Equal(0, await page.Locator("[data-telephony-merge-calls]").CountAsync());
        Assert.Equal("fa-solid fa-phone", await page.Locator("[data-telephony-toggle-icon]").GetAttributeAsync("class"));

        // Act - hang up.
        await page.ClickAsync("[data-telephony-hangup]");

        // Assert - the provider's answer to the hangup says the call is over, and the phone is ready at once rather
        // than waiting for a separate event.
        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
        await AssertStatusAsync(page, "Ready");

        // Act - the provider's own report that the call ended arrives afterwards.
        await PublishLatestCallStateAsync(page);

        // Assert - it changes nothing.
        await AssertStatusAsync(page, "Ready");
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
    }

    [Fact]
    public async Task EnterInPhoneNumber_DialsNumber()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");
        var baselineCount = await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetDialRequestCount')");

        // Act
        await page.PressAsync("[data-telephony-number]", "Enter");

        // Assert
        await page.WaitForFunctionAsync(
            "([count]) => window.telephonySoftPhone.getInstance().getConnection().invoke('GetDialRequestCount').then(value => value === count + 1)",
            new[] { baselineCount });

        // While the dial is in flight the widget mirrors the number being connected in the (now read-only) input,
        // so the display does not jump when the first authoritative call state arrives.
        Assert.Equal("+1 (555) 123-4567", await page.Locator("[data-telephony-number]").InputValueAsync());
    }

    [Fact]
    public async Task ActiveCallNumber_RemainsVisibleAndDisabledAfterReload()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");

        // Act
        await page.ReloadAsync();
        await WaitForConnectedAsync(page);

        // Assert
        var number = page.Locator("[data-telephony-number]");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-number]').value !== ''");
        Assert.Equal("+1 (555) 123-4567", await number.InputValueAsync());
        Assert.True(await number.IsDisabledAsync());
    }

    [Fact]
    public async Task Dial_WhileCommandIsPending_SendsOnlyOneRequest()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");

        var baselineCount = await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetDialRequestCount')");
        await page.EvaluateAsync(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('SetDialDelay', 500)");

        // Act
        await page.ClickAsync("[data-telephony-dial]");
        await page.EvaluateAsync("() => document.querySelector('[data-telephony-dial]').click()");

        // Assert
        Assert.True(await page.Locator("[data-telephony-dial]").IsDisabledAsync());
        await page.WaitForTimeoutAsync(100);

        var pendingCount = await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetDialRequestCount')");

        Assert.Equal(baselineCount + 1, pendingCount);

        // Once the dial is acknowledged the connecting call can be cancelled, and no second call was placed.
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Equal(baselineCount + 1, await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetDialRequestCount')"));
    }

    [Fact]
    public async Task Dial_ThenHold_ShowsResumeControl()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");

        // Act
        await HoldAsync(page);

        // Assert
        await AssertTimedStatusAsync(page, "On hold");
        Assert.Equal("+1 (555) 123-4567", await page.Locator("[data-telephony-number]").InputValueAsync());
        Assert.True(await page.Locator("[data-telephony-active-calls]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-hold]").IsHiddenAsync());

        // The field only shows the held call's number, so the dial button -- which would sit where Hold just was -- is
        // not offered until the agent enters a number to add.
        Assert.True(await page.Locator("[data-telephony-dial]").IsHiddenAsync());
    }

    [Fact]
    public async Task HeldCall_AllowsSecondDial_AndListsBothCalls()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);

        var number = page.Locator("[data-telephony-number]");
        Assert.False(await number.IsDisabledAsync());
        Assert.Equal("+1 (555) 123-4567", await number.InputValueAsync());
        Assert.True(await page.Locator("[data-telephony-dial]").IsHiddenAsync());

        // Act
        await number.FillAsync("+15557654321");

        // Assert - entering a number to add offers the dial button straight away.
        Assert.True(await page.Locator("[data-telephony-dial]").IsVisibleAsync());

        // Act
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);

        // Assert
        var calls = page.Locator("[data-telephony-call-select]");
        await calls.Nth(1).WaitForAsync();
        Assert.Equal(2, await calls.CountAsync());
        var callListText = await page.Locator("[data-telephony-active-calls-list]").InnerTextAsync();
        Assert.Contains("(555) 123-4567", callListText);
        Assert.Contains("(555) 765-4321", callListText);
    }

    // Bug: on hold the agent entered the number to add and the Call button never appeared -- the field's input did not
    // re-render the phone -- and the next render wrote the held call's number back over what they had typed.
    [Fact]
    public async Task HeldCall_KeepsTheNumberTheAgentEntered_WhenThePhoneRendersAgain()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);
        await page.FillAsync("[data-telephony-number]", "+15557654321");

        // Act - another report of the held call re-renders the phone.
        await PublishLatestCallStateAsync(page);
        await page.WaitForTimeoutAsync(100);

        // Assert
        Assert.Equal("+15557654321", await page.Locator("[data-telephony-number]").InputValueAsync());
        Assert.True(await page.Locator("[data-telephony-dial]").IsVisibleAsync());
        await AssertTimedStatusAsync(page, "On hold");
    }

    [Fact]
    public async Task HeldCall_KeypadEntry_StartsANewNumberAndOffersTheDialButton()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);

        // Act
        await page.ClickAsync("[data-telephony-key=\"1\"]");

        // Assert - the digit starts a fresh entry instead of being appended to the held call's number.
        Assert.Equal("1", await page.Locator("[data-telephony-number]").InputValueAsync());
        Assert.True(await page.Locator("[data-telephony-dial]").IsVisibleAsync());
    }

    [Fact]
    public async Task ActiveCallList_WithOneCall_RemainsHidden()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");

        // Act
        await DialAndConnectAsync(page, "2001");

        // Assert
        Assert.True(await page.Locator("[data-telephony-active-calls]").IsHiddenAsync());
    }

    [Fact]
    public async Task MultipleSelectedCalls_CanBeConferencedWithoutEnteringCallIds()
    {
        // Arrange
        var page = await CreateThreeCallPageAsync();
        var baselineCount = await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetMergeRequestCount')");
        var merge = page.Locator("[data-telephony-merge-calls]");
        var summary = page.Locator("[data-telephony-merge-summary]");

        // Assert - with nothing ticked, Merge is offered but waits for the agent to pick the calls.
        Assert.True(await merge.IsVisibleAsync());
        Assert.True(await merge.IsDisabledAsync());
        Assert.Equal(0, CountOf(await summary.InnerTextAsync(), "(555)"));

        // Act - ticking two calls merges just them; ticking the third merges all three.
        var selections = page.Locator("[data-telephony-conference-call]");
        await selections.Nth(0).CheckAsync();
        await selections.Nth(1).CheckAsync();
        Assert.Equal(2, CountOf(await summary.InnerTextAsync(), "(555)"));
        await selections.Nth(2).CheckAsync();
        Assert.Equal(3, CountOf(await summary.InnerTextAsync(), "(555)"));
        await merge.ClickAsync();

        // Assert
        await page.WaitForFunctionAsync(
            "([count]) => window.telephonySoftPhone.getInstance().getConnection().invoke('GetMergeRequestCount').then(value => value === count + 1)",
            new[] { baselineCount });
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-telephony-merge-calls]')");
        await page.WaitForFunctionAsync(
            "() => Array.from(document.querySelectorAll('.telephony-soft-phone__active-call-state')).every(element => element.textContent.trim() === 'In conference')");
    }

    [Fact]
    public async Task Transfer_WhenDirectorySupported_ListsEntriesAndTransfersSelectedCall()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "7024993350");
        var baselineCount = await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount')");

        // Act
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"2001\"]").WaitForAsync();
        Assert.True(await page.Locator("[data-telephony-keypad-panel]").IsHiddenAsync());
        Assert.Equal("Keypad", await page.Locator("[data-telephony-transfer-label]").InnerTextAsync());
        await page.ClickAsync("[data-telephony-directory-destination=\"2001\"]");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        await page.WaitForFunctionAsync(
            "([count]) => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount').then(value => value === count + 1)",
            new[] { baselineCount });
        Assert.True(await page.Locator("[data-telephony-transfer-panel]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-keypad-panel]").IsVisibleAsync());
        Assert.Equal("Transfer", await page.Locator("[data-telephony-transfer-label]").InnerTextAsync());
    }

    [Fact]
    public async Task ConferenceCall_HidesTransferUntilOneInteractionIsSelected()
    {
        // Arrange
        var page = await CreateTwoCallPageAsync();
        var currentCallId = await GetCurrentCallIdAsync(page);
        await page.EvaluateAsync(
            """
            ([callId]) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                {
                    callId,
                    to: '+15557654321',
                    direction: 0,
                    state: 3,
                    providerName: 'InMemory',
                    metadata: { isConference: true }
                })
            """,
            new[] { currentCallId });

        // Assert
        var transfer = page.Locator("[data-telephony-transfer]");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-telephony-transfer]').hidden");

        // Act
        await page.Locator($"[data-telephony-conference-call=\"{currentCallId}\"]").CheckAsync();

        // Assert
        Assert.True(await transfer.IsVisibleAsync());
    }

    [Fact]
    public async Task DisconnectAll_HangupsEveryActiveCall()
    {
        // Arrange
        var page = await CreateTwoCallPageAsync();
        var baselineCount = await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetHangupRequestCount')");

        // Act
        await page.ClickAsync("[data-telephony-hangup-all]");

        // Assert
        await page.WaitForFunctionAsync(
            "([count]) => window.telephonySoftPhone.getInstance().getConnection().invoke('GetHangupRequestCount').then(value => value === count + 2)",
            new[] { baselineCount });
    }

    private static int CountOf(string text, string value)
    {
        return (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
    }

    private async Task<IPage> CreateTwoCallPageAsync()
    {
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");
        await DialAndConnectAsync(page, "+15551234567");
        await HoldAsync(page);
        await DialAndConnectAsync(page, "+15557654321");
        await page.Locator("[data-telephony-call-select]").Nth(1).WaitForAsync();

        return page;
    }

    private async Task<IPage> CreateThreeCallPageAsync()
    {
        var page = await CreateTwoCallPageAsync();

        await HoldAsync(page);
        await DialAndConnectAsync(page, "+15551112222");
        await page.Locator("[data-telephony-call-select]").Nth(2).WaitForAsync();

        return page;
    }
}
