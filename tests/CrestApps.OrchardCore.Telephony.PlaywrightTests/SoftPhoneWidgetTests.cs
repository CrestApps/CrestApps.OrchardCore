using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// End-to-end tests that drive the real soft phone client in a headless browser against a test hub,
/// verifying the SignalR contract and the widget's call state transitions.
/// </summary>
public sealed class SoftPhoneWidgetTests : IAsyncLifetime
{
    private SoftPhoneTestServer _server = null!;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public async ValueTask InitializeAsync()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}.");
        }

        _server = new SoftPhoneTestServer();
        await _server.StartAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dial_ThenHangup_TransitionsSoftPhoneUi()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);

        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");

        // Act
        await page.ClickAsync("[data-telephony-dial]");

        // Assert - the command response alone does not change the call state.
        Assert.Equal("Ready", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
        Assert.True(await page.Locator("[data-telephony-dial]").IsVisibleAsync());

        // Act - publish the provider-authoritative state.
        await PublishLatestCallStateAsync(page);

        // Assert
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var status = await page.Locator("[data-telephony-status]").InnerTextAsync();
        Assert.Equal("In call", status.Trim());
        Assert.True(await page.Locator("[data-telephony-dial]").IsHiddenAsync());
        Assert.Equal("fa-solid fa-phone", await page.Locator("[data-telephony-toggle-icon]").GetAttributeAsync("class"));
        Assert.True(await page.Locator("[data-telephony-mute]").IsVisibleAsync());
        Assert.True(await page.Locator("[data-telephony-merge]").IsHiddenAsync());

        // Act - hang up
        await page.ClickAsync("[data-telephony-hangup]");

        // Assert - the command acknowledgement does not change the state.
        Assert.Equal("In call", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());

        // Act - publish the provider-authoritative terminal state.
        await PublishLatestCallStateAsync(page);

        // Assert - back to idle after the provider event.
        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
    }

    [Fact]
    public async Task EnterInPhoneNumber_DialsNumber()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
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
        Assert.Equal(string.Empty, await page.Locator("[data-telephony-number]").InputValueAsync());
    }

    [Fact]
    public async Task ActiveCallNumber_RemainsVisibleAndDisabledAfterReload()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Act
        await page.ReloadAsync();
        await WaitForConnectedAsync(page);

        // Assert
        var number = page.Locator("[data-telephony-number]");
        await number.WaitForAsync();
        Assert.Equal("+1 (555) 123-4567", await number.InputValueAsync());
        Assert.True(await number.IsDisabledAsync());
    }

    [Fact]
    public async Task Dial_WhileCommandIsPending_SendsOnlyOneRequest()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
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

        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
        });
        await page.WaitForFunctionAsync(
            "() => !document.querySelector('[data-telephony-dial]').disabled");
        await page.EvaluateAsync(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('SetDialDelay', 0)");
    }

    [Fact]
    public async Task Dial_ThenHold_ShowsResumeControl()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);

        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        await page.Locator("[data-telephony-hold]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Act
        await page.ClickAsync("[data-telephony-hold]");
        await PublishLatestCallStateAsync(page);

        // Assert
        await page.Locator("[data-telephony-resume]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var status = await page.Locator("[data-telephony-status]").InnerTextAsync();
        Assert.Equal("On hold", status.Trim());
    }

    [Fact]
    public async Task HeldCall_AllowsSecondDial_AndListsBothCalls()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        await page.ClickAsync("[data-telephony-hold]");
        await PublishLatestCallStateAsync(page);

        // Act
        var number = page.Locator("[data-telephony-number]");
        Assert.False(await number.IsDisabledAsync());
        Assert.Equal(string.Empty, await number.InputValueAsync());
        await number.FillAsync("+15557654321");
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

    [Fact]
    public async Task ActiveCallList_ShortExtension_RemainsUnformatted()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "2001");

        // Act
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);

        // Assert
        Assert.Contains(
            "2001",
            await page.Locator("[data-telephony-active-calls-list]").InnerTextAsync());
    }

    [Fact]
    public async Task TwoSelectedCalls_CanBeConferencedWithoutEnteringCallIds()
    {
        // Arrange
        var page = await CreateTwoCallPageAsync();
        var baselineCount = await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetMergeRequestCount')");
        var merge = page.Locator("[data-telephony-merge]");
        Assert.True(await merge.IsHiddenAsync());

        // Act
        var selections = page.Locator("[data-telephony-conference-call]");
        await selections.Nth(0).CheckAsync();
        Assert.True(await merge.IsHiddenAsync());
        await selections.Nth(1).CheckAsync();
        Assert.True(await merge.IsVisibleAsync());
        await merge.ClickAsync();

        // Assert
        await page.WaitForFunctionAsync(
            "([count]) => window.telephonySoftPhone.getInstance().getConnection().invoke('GetMergeRequestCount').then(value => value === count + 1)",
            new[] { baselineCount });
        Assert.True(await merge.IsHiddenAsync());
    }

    [Fact]
    public async Task Transfer_WhenDirectorySupported_ListsEntriesAndTransfersSelectedCall()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "7024993350");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        var baselineCount = await page.EvaluateAsync<int>(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount')");

        // Act
        await page.ClickAsync("[data-telephony-transfer]");
        await page.Locator("[data-telephony-directory-destination=\"2001\"]").WaitForAsync();
        await page.ClickAsync("[data-telephony-directory-destination=\"2001\"]");
        await page.ClickAsync("[data-telephony-transfer-confirm]");

        // Assert
        await page.WaitForFunctionAsync(
            "([count]) => window.telephonySoftPhone.getInstance().getConnection().invoke('GetTransferRequestCount').then(value => value === count + 1)",
            new[] { baselineCount });
        Assert.True(await page.Locator("[data-telephony-transfer-panel]").IsHiddenAsync());
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
        Assert.True(await transfer.IsHiddenAsync());

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

    [Fact]
    public async Task RemoteProviderDisconnect_ImmediatelyClearsActiveCall()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Act
        await page.EvaluateAsync(
            """
            () => window.telephonySoftPhone.getInstance().getConnection().invoke('DisconnectLatestCall')
            """);

        // Assert
        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
        Assert.Equal("Ready", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
    }

    [Fact]
    public async Task StaleDisconnectForPreviousCall_DoesNotClearCurrentCall()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        var previousCallId = await GetCurrentCallIdAsync(page);
        await page.EvaluateAsync(
            "() => window.telephonySoftPhone.getInstance().getConnection().invoke('DisconnectLatestCall')");
        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        await page.FillAsync("[data-telephony-number]", "+15557654321");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
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
        Assert.Equal("In call", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
        Assert.True(await page.Locator("[data-telephony-hangup]").IsVisibleAsync());
    }

    [Fact]
    public async Task TerminalEventForProviderCall_WhenClientHoldsDifferentStaleCall_RefreshesAndClearsState()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
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
        Assert.Equal("Ready", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
    }

    [Fact]
    public async Task ProviderEventDuringActiveCallRestoration_WinsOverStaleLookup()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        var connection = "window.telephonySoftPhone.getInstance().getConnection()";
        var baselineLookupCount = await page.EvaluateAsync<int>(
            $"() => {connection}.invoke('GetCallLookupRequestCount')");
        await page.EvaluateAsync(
            $"() => {connection}.invoke('SetCallLookupDelay', 500)");

        await page.ReloadAsync();
        await WaitForConnectedAsync(page);
        await page.WaitForFunctionAsync(
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
        Assert.Equal("Ready", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
    }

    [Fact]
    public async Task RecentTab_ShowsCallHistory_AndHidesKeypad()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);

        await page.ClickAsync("[data-telephony-toggle]");

        // Act - switch to the Recent calls tab in the footer.
        await page.ClickAsync("[data-telephony-tab=\"history\"]");

        // Assert - the history view is shown, the keypad view is hidden, and recent calls are listed.
        await page.Locator("[data-telephony-view=\"history\"]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-view=\"keypad\"]").IsHiddenAsync());

        var historyText = await page.Locator("[data-telephony-history-list]").InnerTextAsync();
        Assert.Contains("15551234567", historyText);
        Assert.Contains("15559876543", historyText);

        // Act - switch back to the keypad tab.
        await page.ClickAsync("[data-telephony-tab=\"keypad\"]");

        // Assert - the keypad view is shown again.
        await page.Locator("[data-telephony-view=\"keypad\"]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-view=\"history\"]").IsHiddenAsync());
    }

    [Fact]
    public async Task ExtensionTab_ShowsExtensionView_AndHidesBuiltInViews()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);

        await page.ClickAsync("[data-telephony-toggle]");

        // Act
        await page.ClickAsync("[data-telephony-tab=\"contact-center\"]");

        // Assert
        await page.Locator("[data-telephony-view=\"contact-center\"]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-view=\"keypad\"]").IsHiddenAsync());
        Assert.True(await page.Locator("[data-telephony-view=\"history\"]").IsHiddenAsync());
    }

    [Theory]
    [InlineData("keypad")]
    [InlineData("history")]
    [InlineData("contact-center")]
    public async Task SelectedTab_PersistsAcrossReload(string tab)
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);

        await page.ClickAsync("[data-telephony-toggle]");
        await page.ClickAsync($"[data-telephony-tab=\"{tab}\"]");

        // Act
        await page.ReloadAsync();
        await WaitForConnectedAsync(page);

        // Assert
        await page.Locator($"[data-telephony-view=\"{tab}\"]")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Equal("true", await page.Locator($"[data-telephony-tab=\"{tab}\"]").GetAttributeAsync("aria-selected"));

        if (tab == "history")
        {
            var historyText = await page.Locator("[data-telephony-history-list]").InnerTextAsync();
            Assert.Contains("15551234567", historyText);
        }
    }

    [Fact]
    public async Task AllTabs_KeepTheSameBodyHeight()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");

        // Act
        await page.ClickAsync("[data-telephony-tab=\"keypad\"]");
        await page.Locator("[data-telephony-view=\"keypad\"]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var keypadHeight = await GetConfiguredHeightAsync(page);
        await page.ClickAsync("[data-telephony-tab=\"history\"]");
        await page.Locator("[data-telephony-view=\"history\"]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var historyHeight = await GetConfiguredHeightAsync(page);
        await page.ClickAsync("[data-telephony-tab=\"contact-center\"]");
        await page.Locator("[data-telephony-view=\"contact-center\"]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var extensionHeight = await GetConfiguredHeightAsync(page);

        // Assert
        Assert.NotEqual(string.Empty, keypadHeight);
        Assert.Equal(keypadHeight, historyHeight);
        Assert.Equal(keypadHeight, extensionHeight);
    }

    [Fact]
    public async Task RingingInboundCall_DoesNotShowHangup_UntilConnected()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");

        await page.EvaluateAsync(
            """
            () => {
                const api = window.telephonySoftPhone.getInstance();
                api.setIncomingOffer(
                    {
                        callId: 'call-inbound-1',
                        from: '+15550001000',
                        direction: 'Inbound',
                        state: 'Ringing',
                        providerName: 'InMemory'
                    },
                    {
                        properties: {
                            acceptUrl: '/accept',
                            reservationId: 'res-1'
                        }
                    });

                window.fetch = async () => ({
                    ok: true,
                    json: async () => ({ succeeded: true, requiresDeviceAnswer: false })
                });
            }
            """);

        // Assert - ringing should not expose hangup yet.
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
        Assert.Equal("Ringing...", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());

        // Act
        await page.ClickAsync("[data-telephony-incoming-answer]");
        await PublishCallStateAsync(page, "call-inbound-1", "+15550001000");

        // Assert - once the provider event arrives, the call is live.
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.Equal("In call", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
        Assert.Equal("+15550001000", (await page.Locator("[data-telephony-peer]").InnerTextAsync()).Trim());
    }

    [Fact]
    public async Task PendingInboundOffer_OverridesProviderConnectedState_ForTheSameCall()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await PublishCallStateAsync(page, "call-inbound-pending", "+15550001001");
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Act
        await page.EvaluateAsync(
            """
            () => window.telephonySoftPhone.getInstance().setIncomingOffer(
                {
                    callId: 'call-inbound-pending',
                    from: '+15550001001',
                    direction: 'Inbound',
                    state: 'Ringing',
                    providerName: 'InMemory'
                },
                {
                    properties: {
                        acceptUrl: '/accept',
                        reservationId: 'res-pending'
                    }
                })
            """);

        // Assert
        await page.Locator("[data-telephony-incoming]")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
        Assert.Equal("Ringing...", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
        Assert.Equal("+15550001001", (await page.Locator("[data-telephony-peer]").InnerTextAsync()).Trim());
    }

    [Fact]
    public async Task AcceptedInboundOffer_RemainsActive_WhenOfferIsRevokedDuringAccept()
    {
        // Arrange
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");

        await page.EvaluateAsync(
            """
            () => {
                const api = window.telephonySoftPhone.getInstance();
                api.setIncomingOffer(
                    {
                        callId: 'call-inbound-2',
                        from: '+15550001000',
                        direction: 'Inbound',
                        state: 'Ringing',
                        providerName: 'InMemory'
                    },
                    {
                        properties: {
                            acceptUrl: '/accept',
                            reservationId: 'res-2'
                        }
                    });

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
        Assert.Equal("In call", (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
        Assert.Equal("+15550001000", (await page.Locator("[data-telephony-peer]").InnerTextAsync()).Trim());
    }

    private static async Task WaitForConnectedAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            """
            () => {
                const el = document.querySelector('#telephony-soft-phone');
                const api = el && el.__telephonySoftPhone;
                const connection = api && api.getConnection && api.getConnection();
                return connection && connection.state === 'Connected';
            }
            """);
    }

    private async Task<IPage> CreateTwoCallPageAsync()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_server.BaseUrl);
        await WaitForConnectedAsync(page);
        await page.ClickAsync("[data-telephony-toggle]");
        await page.FillAsync("[data-telephony-number]", "+15551234567");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        await page.ClickAsync("[data-telephony-hold]");
        await PublishLatestCallStateAsync(page);
        await page.FillAsync("[data-telephony-number]", "+15557654321");
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        await page.Locator("[data-telephony-call-select]").Nth(1).WaitForAsync();

        return page;
    }

    private static async Task PublishLatestCallStateAsync(IPage page)
    {
        await page.EvaluateAsync(
            """
            async () => {
                const connection = window.telephonySoftPhone.getInstance().getConnection();

                for (let attempt = 0; attempt < 20; attempt++) {
                    const published = await connection.invoke('PublishLatestCallState');

                    if (published) {
                        return;
                    }

                    await new Promise(resolve => setTimeout(resolve, 25));
                }

                throw new Error('The test provider did not create a call.');
            }
            """);
    }

    private static async Task PublishCallStateAsync(IPage page, string callId, string from)
    {
        await page.EvaluateAsync(
            """
            ([callId, from]) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                {
                    callId,
                    from,
                    direction: 1,
                    state: 3,
                    providerName: 'InMemory'
                })
            """,
            new[] { callId, from });
    }

    private static async Task<string> GetConfiguredHeightAsync(IPage page)
    {
        return await page.Locator("#telephony-soft-phone").EvaluateAsync<string>(
            "element => element.style.getPropertyValue('--telephony-view-height').trim()");
    }

    private static async Task<string> GetCurrentCallIdAsync(IPage page)
    {
        return await page.EvaluateAsync<string>(
            "() => window.telephonySoftPhone.getInstance().getCurrentCall().callId");
    }
}
