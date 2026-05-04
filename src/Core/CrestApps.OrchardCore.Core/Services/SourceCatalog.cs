using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

/// <summary>
/// Document-backed implementation of <see cref="ISourceCatalog{T}"/> that extends <see cref="Catalog{T}"/>
/// with source-based filtering.
/// </summary>
/// <typeparam name="T">The type of source-aware catalog item managed by this catalog.</typeparam>
public class SourceCatalog<T> : Catalog<T>, ISourceCatalog<T>
    where T : CatalogItem, ISourceAwareModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SourceCatalog{T}"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for accessing the backing document.</param>
    public SourceCatalog(IDocumentManager<DictionaryDocument<T>> documentManager)
    : base(documentManager)
    {
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<T>> GetAsync(string source, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        var document = await DocumentManager.GetOrCreateImmutableAsync();

        return document.Records.Values.Where(x => x.Source.Equals(source, StringComparison.OrdinalIgnoreCase))
            .Select(Clone)
            .ToArray();
    }

    protected override IEnumerable<T> GetSortable(QueryContext context, IEnumerable<T> records)
    {
        if (!string.IsNullOrEmpty(context.Source))
        {
            records = records.Where(x => x.Source.Equals(context.Source, StringComparison.OrdinalIgnoreCase));
        }

        return base.GetSortable(context, records);
    }
}
