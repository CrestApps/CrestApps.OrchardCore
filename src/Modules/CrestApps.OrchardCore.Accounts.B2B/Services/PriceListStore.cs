using CrestApps.Core.Services;
using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

public interface IPriceListStore : ICatalog<PriceList>
{
    Task<(int Count, IReadOnlyList<PriceList> PriceLists)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every price list, by name, such as for an account to choose one.
    /// </summary>
    Task<IReadOnlyList<PriceList>> ListAllAsync(CancellationToken cancellationToken = default);
}

public sealed class PriceListStore : DocumentCatalog<PriceList, PriceListIndex>, IPriceListStore
{
    public PriceListStore(ISession session)
        : base(session)
    {
        CollectionName = B2BAccountsConstants.PriceListsCollectionName;
    }

    public Task<(int Count, IReadOnlyList<PriceList> PriceLists)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
        => PageByNameAsync(index => index.Name, search, skip, take, cancellationToken);

    public Task<IReadOnlyList<PriceList>> ListAllAsync(CancellationToken cancellationToken = default)
        => ListByNameAsync(index => index.Name, cancellationToken);
}
