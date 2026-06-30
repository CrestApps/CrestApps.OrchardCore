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

        // Assert
        await page.Locator("[data-telephony-hangup]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var status = await page.Locator("[data-telephony-status]").InnerTextAsync();
        Assert.Equal("In call", status.Trim());
        Assert.True(await page.Locator("[data-telephony-dial]").IsHiddenAsync());

        // Act - hang up
        await page.ClickAsync("[data-telephony-hangup]");

        // Assert - back to idle
        await page.Locator("[data-telephony-dial]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-hangup]").IsHiddenAsync());
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
        await page.Locator("[data-telephony-hold]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        // Act
        await page.ClickAsync("[data-telephony-hold]");

        // Assert
        await page.Locator("[data-telephony-resume]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var status = await page.Locator("[data-telephony-status]").InnerTextAsync();
        Assert.Equal("On hold", status.Trim());
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
}
