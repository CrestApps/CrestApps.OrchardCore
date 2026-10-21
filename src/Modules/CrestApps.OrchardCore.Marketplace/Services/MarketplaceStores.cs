using CrestApps.Core.Services;
using CrestApps.OrchardCore.Marketplace.Indexes;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Marketplace.Services;

public interface IVendorStore : ICatalog<Vendor>
{
    Task<IReadOnlyList<Vendor>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vendor>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<(int Count, IReadOnlyList<Vendor> Vendors)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default);
}

public sealed class VendorStore : DocumentCatalog<Vendor, VendorIndex>, IVendorStore
{
    public VendorStore(ISession session)
        : base(session)
    {
        CollectionName = MarketplaceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<Vendor>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId)
            ? []
            : [.. await Session.Query<Vendor, VendorUserIndex>(index => index.UserId == userId, collection: CollectionName).ListAsync(cancellationToken)];

    public async Task<IReadOnlyList<Vendor>> ListAllAsync(CancellationToken cancellationToken = default)
        => [.. await Session.Query<Vendor, VendorIndex>(collection: CollectionName).OrderBy(index => index.Name).Take(1000).ListAsync(cancellationToken)];

    public async Task<(int Count, IReadOnlyList<Vendor> Vendors)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Vendor, VendorIndex>(collection: CollectionName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var start = search.Trim();

            query = query.Where(index => index.Name.StartsWith(start));
        }

        var count = await query.CountAsync(cancellationToken);

        return (count, [.. await query.OrderBy(index => index.Name).ThenBy(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken)]);
    }
}

public interface IVendorEarningStore : ICatalog<VendorEarning>
{
    Task<IReadOnlyList<VendorEarning>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendorEarning>> ListByVendorAsync(string vendorId, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendorEarning>> ListByStatusAsync(VendorEarningStatus status, DateTime? heldBeforeUtc, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendorEarning>> ListAvailableAsync(string vendorId, CancellationToken cancellationToken = default);
}

public sealed class VendorEarningStore : DocumentCatalog<VendorEarning, VendorEarningIndex>, IVendorEarningStore
{
    public VendorEarningStore(ISession session)
        : base(session)
    {
        CollectionName = MarketplaceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<VendorEarning>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(orderId)
            ? []
            : [.. await Session.Query<VendorEarning, VendorEarningIndex>(index => index.OrderId == orderId, collection: CollectionName).ListAsync(cancellationToken)];

    public async Task<IReadOnlyList<VendorEarning>> ListByVendorAsync(string vendorId, int take, CancellationToken cancellationToken = default)
        => [.. await Session.Query<VendorEarning, VendorEarningIndex>(index => index.VendorId == vendorId, collection: CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken)];

    public async Task<IReadOnlyList<VendorEarning>> ListByStatusAsync(VendorEarningStatus status, DateTime? heldBeforeUtc, int take, CancellationToken cancellationToken = default)
    {
        var query = heldBeforeUtc is { } before
            ? Session.Query<VendorEarning, VendorEarningIndex>(index => index.Status == status && index.HoldUntilUtc <= before, collection: CollectionName)
            : Session.Query<VendorEarning, VendorEarningIndex>(index => index.Status == status, collection: CollectionName);

        return [.. await query.OrderBy(index => index.HoldUntilUtc).Take(take).ListAsync(cancellationToken)];
    }

    public async Task<IReadOnlyList<VendorEarning>> ListAvailableAsync(string vendorId, CancellationToken cancellationToken = default)
        => [.. await Session.Query<VendorEarning, VendorEarningIndex>(index => index.VendorId == vendorId && index.Status == VendorEarningStatus.Available, collection: CollectionName).ListAsync(cancellationToken)];
}

public interface IVendorPayoutStore : ICatalog<VendorPayout>
{
    Task<IReadOnlyList<VendorPayout>> ListByVendorAsync(string vendorId, int take, CancellationToken cancellationToken = default);
}

public sealed class VendorPayoutStore : DocumentCatalog<VendorPayout, VendorPayoutIndex>, IVendorPayoutStore
{
    public VendorPayoutStore(ISession session)
        : base(session)
    {
        CollectionName = MarketplaceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<VendorPayout>> ListByVendorAsync(string vendorId, int take, CancellationToken cancellationToken = default)
        => [.. await Session.Query<VendorPayout, VendorPayoutIndex>(index => index.VendorId == vendorId, collection: CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken)];
}
