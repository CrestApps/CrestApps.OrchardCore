using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Core.Deployments;

/// <summary>
/// Exports every record of a plain catalog store as one recipe step that holds the records in one array.
/// </summary>
/// <remarks>
/// The step is named after the deployment step, and each record is written by <see cref="CatalogRecordSerializer"/>,
/// so only the database document id of the storing site is left out. A
/// <see cref="Recipes.CatalogStoreRecipeStep{TEntry}"/> with the same step and array names imports it. Override
/// <see cref="CatalogDeploymentSourceBase{TStep, TEntry}.Export(TEntry)"/> to leave out a secret.
/// </remarks>
/// <typeparam name="TStep">The deployment step type.</typeparam>
/// <typeparam name="TEntry">The record type.</typeparam>
public abstract class CatalogStoreDeploymentSource<TStep, TEntry> : CatalogDeploymentSourceBase<TStep, TEntry>
    where TStep : DeploymentStep
    where TEntry : CatalogItem
{
    private readonly ICatalog<TEntry> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogStoreDeploymentSource{TStep, TEntry}"/> class.
    /// </summary>
    /// <param name="entriesName">The name of the array the records are written to.</param>
    /// <param name="store">The store that holds the records.</param>
    protected CatalogStoreDeploymentSource(
        string entriesName,
        ICatalog<TEntry> store)
        : base(entriesName)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    /// <summary>
    /// Orders the records the way they are written to the plan.
    /// </summary>
    /// <param name="entries">The stored records.</param>
    /// <returns>The records in the order they are exported.</returns>
    protected virtual IEnumerable<TEntry> Order(IEnumerable<TEntry> entries)
        => entries;

    /// <inheritdoc/>
    protected override async Task<IEnumerable<TEntry>> GetEntriesAsync()
        => Order(await _store.GetAllAsync());

    /// <inheritdoc/>
    protected override JsonObject Export(TEntry entry)
        => CatalogRecordSerializer.Export(entry);
}
