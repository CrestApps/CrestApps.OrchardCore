using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Core.Deployments;

/// <summary>
/// Exports every entry of a named catalog as one recipe step that holds the entries in one array.
/// </summary>
/// <remarks>
/// The step is named after the deployment step, and each entry is written by <see cref="CatalogDeploymentSerializer"/>,
/// so the members the environment owns are left out. A <see cref="Recipes.NamedCatalogRecipeStep{TEntry}"/> with the
/// same step and array names imports it.
/// </remarks>
/// <typeparam name="TStep">The deployment step type.</typeparam>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class NamedCatalogDeploymentSource<TStep, TEntry> : DeploymentSourceBase<TStep>
    where TStep : DeploymentStep
    where TEntry : CatalogItem, INameAwareModel, new()
{
    private readonly string _entriesName;
    private readonly INamedCatalogManager<TEntry> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedCatalogDeploymentSource{TStep, TEntry}"/> class.
    /// </summary>
    /// <param name="entriesName">The name of the step member that holds the array of entries.</param>
    /// <param name="manager">The manager the entries are read through.</param>
    protected NamedCatalogDeploymentSource(
        string entriesName,
        INamedCatalogManager<TEntry> manager)
    {
        ArgumentException.ThrowIfNullOrEmpty(entriesName);

        _entriesName = entriesName;
        _manager = manager;
    }

    /// <summary>
    /// Orders the entries the way they are written to the plan.
    /// </summary>
    /// <param name="entries">The stored entries.</param>
    /// <returns>The entries in the order they are exported.</returns>
    protected virtual IEnumerable<TEntry> Order(IEnumerable<TEntry> entries)
        => entries;

    /// <inheritdoc/>
    protected override async Task ProcessAsync(TStep step, DeploymentPlanResult result)
    {
        var data = new JsonArray();

        foreach (var entry in Order(await _manager.GetAllAsync()))
        {
            data.Add(CatalogDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [_entriesName] = data,
        });
    }
}
