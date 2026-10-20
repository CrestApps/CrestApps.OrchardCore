using CrestApps.Core.Services;
using CrestApps.OrchardCore.Markets.Indexes;
using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Markets.Services;

public interface IMarketStore : ICatalog<Market>
{
    Task<(int Count, IReadOnlyList<Market> Markets)> PageAsync(int skip, int take, CancellationToken cancellationToken = default);
}

public sealed class MarketStore : DocumentCatalog<Market, MarketIndex>, IMarketStore
{
    public MarketStore(ISession session)
        : base(session)
    {
        CollectionName = MarketsConstants.CollectionName;
    }

    public async Task<(int Count, IReadOnlyList<Market> Markets)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Market, MarketIndex>(collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderBy(index => index.Name).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }
}
