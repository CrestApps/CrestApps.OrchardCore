using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Carries a plain catalog manager into the shape a configuration catalog needs.
/// </summary>
/// <typeparam name="T">The catalog entry type.</typeparam>
public sealed class CatalogManagerConfigurationCatalogWriter<T> : IConfigurationCatalogWriter<T>
    where T : CatalogItem
{
    private readonly ICatalogManager<T> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogManagerConfigurationCatalogWriter{T}"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the entries.</param>
    public CatalogManagerConfigurationCatalogWriter(ICatalogManager<T> manager)
    {
        _manager = manager;
    }

    /// <inheritdoc/>
    public ValueTask<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken)
        => _manager.GetAllAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask<T> FindByIdAsync(string itemId, CancellationToken cancellationToken)
        => _manager.FindByIdAsync(itemId, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<T> NewAsync(JsonNode data, CancellationToken cancellationToken)
        => _manager.NewAsync(data, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<ValidationResultDetails> ValidateAsync(T entry, CancellationToken cancellationToken)
        => _manager.ValidateAsync(entry, cancellationToken);

    /// <inheritdoc/>
    public ValueTask CreateAsync(T entry, CancellationToken cancellationToken)
        => _manager.CreateAsync(entry, cancellationToken);

    /// <inheritdoc/>
    public ValueTask UpdateAsync(T entry, JsonNode data, CancellationToken cancellationToken)
        => _manager.UpdateAsync(entry, data, cancellationToken);
}
