using CrestApps.Core.Services;
using CrestApps.OrchardCore.Companies.Indexes;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Companies.Services;

public interface IPriceListStore : ICatalog<PriceList>
{
    Task<(int Count, IReadOnlyList<PriceList> PriceLists)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every price list, by name, such as for a company to choose one.
    /// </summary>
    Task<IReadOnlyList<PriceList>> ListAllAsync(CancellationToken cancellationToken = default);
}

public sealed class PriceListStore : DocumentCatalog<PriceList, PriceListIndex>, IPriceListStore
{
    public PriceListStore(ISession session)
        : base(session)
    {
        CollectionName = CompaniesConstants.CollectionName;
    }

    public async Task<(int Count, IReadOnlyList<PriceList> PriceLists)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<PriceList, PriceListIndex>(collection: CollectionName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();

            query = query.Where(index => index.Name.Contains(text));
        }

        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderBy(index => index.Name).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }

    public async Task<IReadOnlyList<PriceList>> ListAllAsync(CancellationToken cancellationToken = default)
        => [.. await Session.Query<PriceList, PriceListIndex>(collection: CollectionName).OrderBy(index => index.Name).ListAsync(cancellationToken)];
}
