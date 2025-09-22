using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using CrestApps.OrchardCore.YesSql.Core.Indexes;
using YesSql;

namespace CrestApps.OrchardCore.YesSql.Core.Services;

public class NamedDocumentCatalog<T, TIndex> : DocumentCatalog<T, TIndex>, INamedCatalog<T>
    where T : CatalogItem, INameAwareModel
    where TIndex : CatalogItemIndex, INameAwareIndex
{
    public NamedDocumentCatalog(ISession session)
        : base(session)
    {
    }

    public async ValueTask<T> FindByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var item = await Session.Query<T, TIndex>(x => x.Name == name).FirstOrDefaultAsync();

        return item;
    }

    protected override async ValueTask SavingAsync(T record)
    {
        var item = await Session.QueryIndex<TIndex>(x => x.Name == record.Name && x.ItemId != record.ItemId).FirstOrDefaultAsync();

        if (item is not null)
        {
            throw new InvalidOperationException("There is already another model with the same name.");
        }
    }
}
