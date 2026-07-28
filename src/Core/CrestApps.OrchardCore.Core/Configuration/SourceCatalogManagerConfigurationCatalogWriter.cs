using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Carries a source-backed catalog manager into the shape a configuration catalog needs.
/// </summary>
/// <remarks>
/// A source-backed manager cannot build an entry without being told which source implements it, so the source is read
/// from the data the recipe or deployment plan carries. An entry authored without one is refused rather than defaulted:
/// the source decides which implementation runs, and guessing it would store configuration that names one behavior and
/// performs another.
/// </remarks>
/// <typeparam name="T">The catalog entry type.</typeparam>
public sealed class SourceCatalogManagerConfigurationCatalogWriter<T> : IConfigurationCatalogWriter<T>
    where T : SourceCatalogEntry
{
    private readonly ISourceCatalogManager<T> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceCatalogManagerConfigurationCatalogWriter{T}"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the entries.</param>
    public SourceCatalogManagerConfigurationCatalogWriter(ISourceCatalogManager<T> manager)
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
    {
        var source = data?[nameof(SourceCatalogEntry.Source)]?.GetValue<string>();

        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidOperationException(
                $"A '{typeof(T).Name}' entry cannot be imported without a '{nameof(SourceCatalogEntry.Source)}', because the source decides which implementation the entry runs.");
        }

        return _manager.NewAsync(source, data, cancellationToken);
    }

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
