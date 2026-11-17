using System.Text.Json;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Keeps, for one request, a copy of each data source as it was stored when the catalog first read it, so a source's
/// checks can tell a setting a save changes from one it keeps as it is.
/// </summary>
/// <remarks>
/// The API and the recipe step change the record they read before they validate it, and the record a store returns may
/// be the one being changed, so a later read would show the change. The copy is taken when the catalog loads the record.
/// </remarks>
public sealed class AIDataSourceStoredSettings
{
    private readonly Dictionary<string, AIDataSource> _stored = new(StringComparer.Ordinal);

    /// <summary>
    /// Keeps a copy of a data source the catalog read, unless one of it is already kept.
    /// </summary>
    /// <param name="dataSource">The data source as it was read.</param>
    public void Remember(AIDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        if (string.IsNullOrEmpty(dataSource.ItemId) || _stored.ContainsKey(dataSource.ItemId))
        {
            return;
        }

        _stored[dataSource.ItemId] = JsonSerializer.SerializeToNode(dataSource).Deserialize<AIDataSource>();
    }

    /// <summary>
    /// Finds the copy of a data source as it was stored.
    /// </summary>
    /// <param name="itemId">The data source's id.</param>
    /// <returns>The copy, or <see langword="null"/> when the data source was not read in this request, as a new one.</returns>
    public AIDataSource Find(string itemId)
        => !string.IsNullOrEmpty(itemId) && _stored.TryGetValue(itemId, out var stored) ? stored : null;
}
