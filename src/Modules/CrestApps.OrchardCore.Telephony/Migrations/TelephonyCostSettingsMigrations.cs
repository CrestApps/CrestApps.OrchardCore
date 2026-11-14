using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Migrations;

/// <summary>
/// Moves the carrier's price of a call minute, which used to be entered on the AI settings page (Costs), to the
/// telephony settings page, and removes the carrier's prices from the old AI cost settings.
/// </summary>
internal sealed class TelephonyCostSettingsMigrations : DataMigration
{
    private const string OldSettingsName = "AICostSettings";
    private const string OldCurrencyName = "Currency";
    private const string OldPerMinuteName = "TelephonyPerMinute";
    private const string OldPerMessageName = "SmsPerMessage";
    private const string OldDeploymentsName = "Deployments";

    /// <summary>
    /// Moves the price once the tenant is ready.
    /// </summary>
    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => MoveCarrierPricesAsync(scope.ServiceProvider));

        return 1;
    }

    internal static async Task MoveCarrierPricesAsync(IServiceProvider services)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();

        if (MoveCarrierPrices(site))
        {
            await siteService.UpdateSiteSettingsAsync(site);
        }
    }

    /// <summary>
    /// Moves the carrier's price of a minute from the old AI cost settings to the telephony cost settings.
    /// </summary>
    /// <param name="site">The site settings.</param>
    /// <returns><see langword="true"/> when the site settings changed.</returns>
    internal static bool MoveCarrierPrices(ISite site)
    {
        if (site.Properties[OldSettingsName] is not JsonObject oldSettings)
        {
            return false;
        }

        var perMinute = ReadAmount(oldSettings[OldPerMinuteName]);
        var settings = site.GetOrCreate<TelephonyCostSettings>();

        // A price already set on the telephony settings is newer than the old one, so it is kept.
        if (perMinute > 0 && settings.PerMinute <= 0)
        {
            if (oldSettings[OldCurrencyName] is JsonValue currency &&
                currency.TryGetValue<string>(out var code) &&
                !string.IsNullOrWhiteSpace(code))
            {
                settings.Currency = code.Trim().ToUpperInvariant();
            }

            settings.PerMinute = perMinute;
            site.Put(settings);
        }

        oldSettings.Remove(OldPerMinuteName);
        oldSettings.Remove(OldPerMessageName);

        // The AI module may still have to move deployment prices out of the old settings, in their currency, so the
        // currency stays with them until it does.
        if (!oldSettings.ContainsKey(OldDeploymentsName))
        {
            oldSettings.Remove(OldCurrencyName);
        }

        if (oldSettings.Count == 0)
        {
            site.Properties.Remove(OldSettingsName);
        }

        return true;
    }

    private static decimal ReadAmount(JsonNode amount)
    {
        if (amount is not JsonValue value)
        {
            return 0;
        }

        if (value.TryGetValue<decimal>(out var number))
        {
            return Math.Max(0, number);
        }

        return value.TryGetValue<string>(out var text) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? Math.Max(0, number)
            : 0;
    }
}
