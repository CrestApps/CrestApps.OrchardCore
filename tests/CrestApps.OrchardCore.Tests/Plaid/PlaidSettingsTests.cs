using CrestApps.OrchardCore.Plaid.Drivers;
using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.Plaid.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Plaid;

public sealed class PlaidSettingsTests
{
    // Regression: the secrets store records the full type name, so a text secret that existed was listed as
    // "not created yet" on the settings page.
    [Theory]
    [InlineData("OrchardCore.Secrets.TextSecret", true)]
    [InlineData("TextSecret", true)]
    [InlineData("OrchardCore.Secrets.RsaKeySecret", false)]
    [InlineData("OrchardCore.Secrets.X509Secret", false)]
    [InlineData(null, false)]
    public void SecretPicker_ListsTextSecretsByTheirStoredTypeName(string type, bool listed)
    {
        // Act
        var isText = PlaidSettingsDisplayDriver.IsTextSecret(type);

        // Assert
        Assert.Equal(listed, isText);
    }

    [Fact]
    public void Settings_AreConfiguredOnlyWithAClientIdAndASecretName()
    {
        // Arrange
        var empty = new PlaidSettings();
        var noSecret = new PlaidSettings { ClientId = "client" };
        var full = new PlaidSettings { ClientId = "client", SecretName = "Plaid.Secret" };

        // Act and assert
        Assert.False(empty.IsConfigured());
        Assert.False(noSecret.IsConfigured());
        Assert.True(full.IsConfigured());
    }

    [Fact]
    public void SettingsPage_SaysTheHostSuppliesTheKeys_OnlyWhenAnotherProviderReplacedTheSettings()
    {
        // Arrange
        var settingsBased = new PlaidCredentialsProvider(siteService: null, secretManager: null, NullLogger<PlaidCredentialsProvider>.Instance);
        var hostKeys = new FixedPlaidCredentials();

        // Act and assert
        Assert.False(PlaidSettingsDisplayDriver.HostSuppliesKeys(settingsBased));
        Assert.True(PlaidSettingsDisplayDriver.HostSuppliesKeys(hostKeys));
    }

    [Theory]
    [InlineData("https://shop.example.com", "https://shop.example.com/plaid/webhook")]
    [InlineData("https://example.com/tenant1/", "https://example.com/tenant1/plaid/webhook")]
    [InlineData("https://example.com/tenant1?x=1", "https://example.com/tenant1/plaid/webhook")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("not a url", null)]
    public void WebhookUrl_OutsideARequest_IsUnderTheSitesBaseUrl(string baseUrl, string expected)
    {
        // Act
        var url = PlaidLinkSite.WebhookUrlFrom(baseUrl);

        // Assert
        Assert.Equal(expected, url);
    }

    [Fact]
    public void ClientUserId_IsAnOpaqueHashOfTheTenantAndUser_WithoutTheUserId()
    {
        // Act
        var first = PlaidLinkSite.ClientUserId("Default", "4v58xdnk5h39x0h127ktz50xwf");
        var otherTenant = PlaidLinkSite.ClientUserId("Other", "4v58xdnk5h39x0h127ktz50xwf");

        // Assert
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain("4v58xdnk5h39x0h127ktz50xwf", first, StringComparison.Ordinal);
        Assert.NotEqual(first, otherTenant);
        Assert.Equal(first, PlaidLinkSite.ClientUserId("Default", "4v58xdnk5h39x0h127ktz50xwf"));
    }
}
