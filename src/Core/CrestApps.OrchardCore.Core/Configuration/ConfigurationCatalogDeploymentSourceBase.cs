using System.Text.Json.Nodes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Exports the registered configuration catalogs of a single group into a deployment plan, emitting one recipe
/// step per catalog in dependency order so the produced recipe can be replayed into an empty tenant.
/// </summary>
/// <typeparam name="TStep">The deployment step type that selects the catalogs.</typeparam>
public abstract class ConfigurationCatalogDeploymentSourceBase<TStep> : DeploymentSourceBase<TStep>
    where TStep : ConfigurationCatalogDeploymentStep
{
    private readonly IEnumerable<IConfigurationCatalog> _catalogs;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationCatalogDeploymentSourceBase{TStep}"/> class.
    /// </summary>
    /// <param name="catalogs">The configuration catalogs registered in the tenant.</param>
    protected ConfigurationCatalogDeploymentSourceBase(IEnumerable<IConfigurationCatalog> catalogs)
    {
        _catalogs = catalogs;
    }

    /// <summary>
    /// Gets the identifier of the catalog group this source exports.
    /// </summary>
    protected abstract string Group { get; }

    /// <inheritdoc/>
    protected override async Task ProcessAsync(TStep step, DeploymentPlanResult result)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(result);

        foreach (var catalog in Select(step))
        {
            var entries = await catalog.ExportAsync();

            if (entries.Count == 0)
            {
                continue;
            }

            result.Steps.Add(new JsonObject
            {
                ["name"] = catalog.StepName,
                [catalog.CollectionName] = entries,
            });
        }
    }

    private IEnumerable<IConfigurationCatalog> Select(TStep step)
    {
        var catalogs = _catalogs
            .Where(catalog => string.Equals(catalog.Group, Group, StringComparison.Ordinal))
            .OrderBy(catalog => catalog.Order)
            .ThenBy(catalog => catalog.StepName, StringComparer.Ordinal);

        if (step.IncludeAll)
        {
            return catalogs;
        }

        var selected = step.CatalogNames ?? [];

        return catalogs.Where(catalog => selected.Contains(catalog.StepName, StringComparer.OrdinalIgnoreCase));
    }
}
