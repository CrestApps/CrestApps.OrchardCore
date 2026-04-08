using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using YesSql;

namespace CrestApps.Core.Data.YesSql.Services;

public class NamedDocumentCatalog<T, TIndex> : DocumentCatalog<T, TIndex>, INamedCatalog<T>
    where T : CatalogItem, INameAwareModel
    where TIndex : CatalogItemIndex, INameAwareIndex
{
    public NamedDocumentCatalog(ISession session)
    : base(session)
    {
    }

    internal NamedDocumentCatalog(ISession session, string collectionName)
    : base(session)
    {
        CollectionName = collectionName;
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var item = await Session.Query<T, TIndex>(x => x.Name == name, collection: CollectionName).FirstOrDefaultAsync();

        return item;
    }

    protected override async ValueTask SavingAsync(T record)
    {
        var item = await Session.QueryIndex<TIndex>(x => x.Name == record.Name && x.ItemId != record.ItemId, collection: CollectionName).FirstOrDefaultAsync();

        if (item is not null)
        {
            throw new InvalidOperationException("There is already another model with the same name.");
        }
    }
}
