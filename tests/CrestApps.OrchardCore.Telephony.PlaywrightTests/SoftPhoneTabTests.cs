using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Drives the real soft phone bundle through its footer tabs: the keypad, recent calls, and a contributed tab.
/// </summary>
public sealed class SoftPhoneTabTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task RecentTab_ShowsCallHistory_AndHidesKeypad()
    {
        // Arrange
        var page = await OpenAsync();
        await page.ClickAsync("[data-telephony-toggle]");

        // Act - switch to the Recent calls tab in the footer.
        await page.ClickAsync("[data-telephony-tab=\"history\"]");

        // Assert - the history view is shown, the keypad view is hidden, and recent calls are listed.
        await page.Locator("[data-telephony-view=\"history\"]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.True(await page.Locator("[data-telephony-view=\"keypad\"]").IsHiddenAsync());

        var historyText = await page.Locator("[data-telephony-history-list]").InnerTextAsync();
        Assert.Contains("+1 (555) 123-4567", historyText);
        Assert.Contains("+1 (555) 987-6543", historyText);

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
        var page = await OpenAsync();
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
        var page = await OpenAsync();
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
            Assert.Contains("+1 (555) 123-4567", historyText);
        }
    }

    [Fact]
    public async Task AllTabs_KeepTheSameBodyHeight()
    {
        // Arrange
        var page = await OpenAsync();
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

    private static async Task<string> GetConfiguredHeightAsync(IPage page)
    {
        return await page.Locator("#telephony-soft-phone").EvaluateAsync<string>(
            "element => element.style.getPropertyValue('--telephony-view-height').trim()");
    }
}
