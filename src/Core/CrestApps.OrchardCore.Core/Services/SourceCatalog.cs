using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

public class SourceCatalog<T> : Catalog<T>, ISourceCatalog<T>
    where T : CatalogItem, ISourceAwareModel
{
    public SourceCatalog(IDocumentManager<DictionaryDocument<T>> documentManager)
    : base(documentManager)
    {
    }

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
