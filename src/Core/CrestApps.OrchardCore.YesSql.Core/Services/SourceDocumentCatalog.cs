using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.YesSql.Core.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Services;

public class SourceDocumentCatalog<T, TIndex> : DocumentCatalog<T, TIndex>, ISourceCatalog<T>
    where T : CatalogItem, ISourceAwareModel
    where TIndex : CatalogItemIndex, ISourceAwareIndex
{
    public SourceDocumentCatalog(ISession session)
        : base(session)
    {
    }

    internal SourceDocumentCatalog(ISession session, string collectionName)
        : base(session)
    {
        CollectionName = collectionName;
    }

    public async ValueTask<IReadOnlyCollection<T>> GetAsync(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        return (await Session.Query<T, TIndex>(x => x.Source == source, collection: CollectionName).ListAsync()).ToArray();
    }
}
