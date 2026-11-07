using CrestApps.Core.Models;
using CrestApps.Core.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Core.Deployments;

/// <summary>
/// Exports every entry of a catalog as one recipe step that holds the entries in one array.
/// </summary>
/// <remarks>
/// The step is named after the deployment step, and each entry is written by <see cref="CatalogDeploymentSerializer"/>,
/// so the members the environment owns are left out. A <see cref="Recipes.CatalogRecipeStep{TEntry}"/> with the same
/// step and array names imports it.
/// </remarks>
/// <typeparam name="TStep">The deployment step type.</typeparam>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class CatalogDeploymentSource<TStep, TEntry> : CatalogDeploymentSourceBase<TStep, TEntry>
    where TStep : DeploymentStep
    where TEntry : CatalogItem
{
    private readonly IReadCatalogManager<TEntry> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogDeploymentSource{TStep, TEntry}"/> class.
    /// </summary>
    /// <param name="entriesName">The name of the array the entries are written to.</param>
    /// <param name="manager">The catalog manager that reads the entries.</param>
    protected CatalogDeploymentSource(
        string entriesName,
        IReadCatalogManager<TEntry> manager)
        : base(entriesName)
    {
        ArgumentNullException.ThrowIfNull(manager);

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
    protected override async Task<IEnumerable<TEntry>> GetEntriesAsync()
        => Order(await _manager.GetAllAsync());
}
