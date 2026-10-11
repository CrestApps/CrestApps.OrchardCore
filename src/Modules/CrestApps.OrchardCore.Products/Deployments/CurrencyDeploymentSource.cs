using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Products.Models;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Products.Deployments;

internal sealed class CurrencyDeploymentSource : DeploymentSourceBase<CurrencyDeploymentStep>
{
    private readonly INamedCatalog<CurrencyEntry> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyDeploymentSource"/> class.
    /// </summary>
    /// <param name="catalog">The currency catalog.</param>
    public CurrencyDeploymentSource(INamedCatalog<CurrencyEntry> catalog)
    {
        _catalog = catalog;
    }

    protected override async Task ProcessAsync(CurrencyDeploymentStep step, DeploymentPlanResult result)
    {
        var currencies = await _catalog.GetAllAsync();
        var currencyObjects = new JsonArray();
        var currencyIds = step.IncludeAll ? [] : step.CurrencyIds ?? [];

        foreach (var currency in currencies
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (currencyIds.Length > 0 && !currencyIds.Contains(currency.ItemId))
            {
                continue;
            }

            currencyObjects.Add(new JsonObject
            {
                ["ItemId"] = currency.ItemId,
                ["Name"] = currency.Name,
                ["DisplayName"] = currency.DisplayName,
                ["CreatedUtc"] = currency.CreatedUtc,
                ["ModifiedUtc"] = currency.ModifiedUtc,
                ["OwnerId"] = currency.OwnerId,
                ["Author"] = currency.Author,
            });
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Currencies"] = currencyObjects,
        });
    }
}
