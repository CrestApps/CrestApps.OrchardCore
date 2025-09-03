using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Core.Services;

public class SourceCatalog<T> : Catalog<T>, ISourceCatalog<T>
    where T : CatalogEntry, ISourceAwareModel
{
    private readonly IDocumentManager<DictionaryDocument<T>> _documentManager;

    public SourceCatalog(IDocumentManager<DictionaryDocument<T>> documentManager)
        : base(documentManager)
    {
        _documentManager = documentManager;
    }

    public async ValueTask<IReadOnlyCollection<T>> GetAsync(string source)
    {
        var document = await _documentManager.GetOrCreateImmutableAsync();

        return document.Records.Values.Where(x => x.Source.Equals(source, StringComparison.OrdinalIgnoreCase))
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
