using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Migrations;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony;

/// <summary>
/// The carrier's price of a minute, once entered on the AI settings page, moves to the telephony settings.
/// </summary>
public sealed class TelephonyCostSettingsMigrationTests
{
    [Fact]
    public void MoveCarrierPrices_MovesThePerMinutePriceAndItsCurrency()
    {
        // Arrange
        var site = new SiteSettings();
        site.Properties["AICostSettings"] = JsonNode.Parse("""{ "Currency": "eur", "TelephonyPerMinute": 0.012, "SmsPerMessage": 0.008 }""");

        // Act
        var changed = TelephonyCostSettingsMigrations.MoveCarrierPrices(site);

        // Assert
        Assert.True(changed);
        var settings = Stored(site);
        Assert.Equal("EUR", settings.Currency);
        Assert.Equal(0.012m, settings.PerMinute);
        Assert.False(site.Properties.ContainsKey("AICostSettings"));
    }

    [Fact]
    public void MoveCarrierPrices_KeepsAPriceAlreadySetOnTheTelephonySettings()
    {
        var site = new SiteSettings();
        site.Put(new TelephonyCostSettings { Currency = "USD", PerMinute = 0.02m });
        site.Properties["AICostSettings"] = JsonNode.Parse("""{ "Currency": "EUR", "TelephonyPerMinute": 0.01 }""");

        TelephonyCostSettingsMigrations.MoveCarrierPrices(site);

        var settings = Stored(site);
        Assert.Equal("USD", settings.Currency);
        Assert.Equal(0.02m, settings.PerMinute);
        Assert.False(site.Properties.ContainsKey("AICostSettings"));
    }

    [Fact]
    public void MoveCarrierPrices_LeavesDeploymentPricesAndTheirCurrencyForTheAIModule()
    {
        var site = new SiteSettings();
        site.Properties["AICostSettings"] = JsonNode.Parse("""{ "Currency": "USD", "TelephonyPerMinute": "0.01", "Deployments": [ { "DeploymentName": "gpt" } ] }""");

        TelephonyCostSettingsMigrations.MoveCarrierPrices(site);

        Assert.Equal(0.01m, Stored(site).PerMinute);
        var oldSettings = Assert.IsType<JsonObject>(site.Properties["AICostSettings"]);
        Assert.Equal("USD", oldSettings["Currency"]?.GetValue<string>());
        Assert.NotNull(oldSettings["Deployments"]);
        Assert.Null(oldSettings["TelephonyPerMinute"]);
    }

    [Fact]
    public void MoveCarrierPrices_DoesNothingWithoutTheOldSettings()
    {
        var site = new SiteSettings();

        Assert.False(TelephonyCostSettingsMigrations.MoveCarrierPrices(site));
        Assert.False(site.Properties.ContainsKey(nameof(TelephonyCostSettings)));
    }

    // Read from the stored JSON, as the next request does, rather than from the entity's in-memory copy.
    private static TelephonyCostSettings Stored(SiteSettings site)
        => site.Properties[nameof(TelephonyCostSettings)].Deserialize<TelephonyCostSettings>();
}
