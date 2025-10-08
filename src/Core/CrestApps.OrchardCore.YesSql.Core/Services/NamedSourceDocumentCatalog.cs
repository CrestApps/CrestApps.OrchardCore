using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.YesSql.Core.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Services;

public class NamedSourceDocumentCatalog<T, TIndex> : SourceDocumentCatalog<T, TIndex>, INamedSourceCatalog<T>, ISourceCatalog<T>
    where T : CatalogItem, INameAwareModel, ISourceAwareModel
    where TIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
{
    public NamedSourceDocumentCatalog(ISession session)
        : base(session)
    {
    }

    internal NamedSourceDocumentCatalog(ISession session, string collectionName)
        : this(session)
    {
        CollectionName = collectionName;
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<T, TIndex>(x => x.DisplayText == name, collection: CollectionName).FirstOrDefaultAsync();
    }

    public async ValueTask<T> GetAsync(string name, string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return await Session.Query<T, TIndex>(x => x.DisplayText == name && x.Source == source, collection: CollectionName).FirstOrDefaultAsync();
    }

    protected override async ValueTask SavingAsync(T record)
    {
        var item = await Session.QueryIndex<TIndex>(x => x.DisplayText == record.Name && x.ItemId != record.ItemId).FirstOrDefaultAsync();

        if (item is not null)
        {
            throw new InvalidOperationException("There is already another model with the same name.");
        }
    }
}
