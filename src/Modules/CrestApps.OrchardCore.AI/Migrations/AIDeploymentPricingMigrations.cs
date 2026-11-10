using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Moves the AI deployment prices that used to be entered on the AI settings page (Costs > AI deployments) onto the
/// deployments themselves, where they now live, and removes them from the site settings.
/// </summary>
internal sealed class AIDeploymentPricingMigrations : DataMigration
{
    private const string CostSettingsName = "AICostSettings";
    private const string DeploymentsName = "Deployments";

    /// <summary>
    /// Moves the prices once the tenant is ready.
    /// </summary>
    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => MovePricesAsync(scope.ServiceProvider));

        return 1;
    }

    internal static async Task MovePricesAsync(IServiceProvider services)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();

        if (site.Properties[CostSettingsName] is not JsonObject costSettings ||
            costSettings[DeploymentsName] is not JsonArray prices)
        {
            return;
        }

        var deploymentManager = services.GetRequiredService<IAIDeploymentManager>();
        var logger = services.GetRequiredService<ILogger<AIDeploymentPricingMigrations>>();
        var currency = costSettings["Currency"]?.GetValue<string>();

        foreach (var entry in prices.OfType<JsonObject>())
        {
            var deploymentName = entry["DeploymentName"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(deploymentName))
            {
                continue;
            }

            var deployment = await deploymentManager.FindByNameAsync(deploymentName);

            if (deployment is null)
            {
                logger.LogWarning("The AI deployment '{DeploymentName}' priced on the AI settings page no longer exists, so its prices were not moved.", deploymentName);

                continue;
            }

            // Prices already set on the deployment are newer than the site's, so they are kept.
            if (deployment.GetPricing() is not null)
            {
                continue;
            }

            deployment.SetPricing(currency, ToPrices(entry));

            if (deployment.GetPricing() is not null)
            {
                await deploymentManager.UpdateAsync(deployment);
            }
        }

        costSettings.Remove(DeploymentsName);
        await siteService.UpdateSiteSettingsAsync(site);
    }

    /// <summary>
    /// Turns one line of the old price list into prices: per million text and audio tokens, and per minute.
    /// </summary>
    internal static IEnumerable<AIDeploymentPrice> ToPrices(JsonObject entry)
    {
        yield return Price(AIUsageMeters.InputTokens, entry["InputPerMillion"], 1_000_000);
        yield return Price(AIUsageMeters.OutputTokens, entry["OutputPerMillion"], 1_000_000);
        yield return Price(AIUsageMeters.InputAudioTokens, entry["AudioInputPerMillion"], 1_000_000);
        yield return Price(AIUsageMeters.OutputAudioTokens, entry["AudioOutputPerMillion"], 1_000_000);
        yield return Price(AIUsageMeters.AudioSeconds, entry["PerMinute"], 60);
    }

    private static AIDeploymentPrice Price(string meter, JsonNode amount, long per)
        => new()
        {
            Meter = meter,
            Amount = ReadAmount(amount),
            Per = per,
        };

    private static decimal ReadAmount(JsonNode amount)
    {
        if (amount is not JsonValue value)
        {
            return 0;
        }

        if (value.TryGetValue<decimal>(out var number))
        {
            return number;
        }

        return value.TryGetValue<string>(out var text) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            ? number
            : 0;
    }
}
