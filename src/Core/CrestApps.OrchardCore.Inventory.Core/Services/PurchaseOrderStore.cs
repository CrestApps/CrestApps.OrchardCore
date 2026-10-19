using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Inventory.Core.Services;

public interface IPurchaseOrderStore
{
    ValueTask<PurchaseOrder> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<(int Count, IReadOnlyList<PurchaseOrder> Orders)> PageAsync(PurchaseOrderQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the suppliers of recent orders, to suggest when another is started.
    /// </summary>
    Task<IReadOnlyList<string>> ListSuppliersAsync(CancellationToken cancellationToken = default);

    ValueTask CreateAsync(PurchaseOrder order, CancellationToken cancellationToken = default);

    ValueTask UpdateAsync(PurchaseOrder order, CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(PurchaseOrder order, CancellationToken cancellationToken = default);
}

public sealed class PurchaseOrderQuery
{
    /// <summary>
    /// Gets or sets the start of an order number or a supplier's name to find.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status to show, or <see langword="null"/> for orders not received or canceled yet.
    /// </summary>
    public PurchaseOrderStatus? Status { get; set; }

    public bool All { get; set; }
}

public sealed class PurchaseOrderStore : DocumentCatalog<PurchaseOrder, PurchaseOrderIndex>, IPurchaseOrderStore
{
    private const int SupplierLookback = 200;

    public PurchaseOrderStore(ISession session)
        : base(session)
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    protected override bool CheckConcurrency => true;

    public async Task<(int Count, IReadOnlyList<PurchaseOrder> Orders)> PageAsync(PurchaseOrderQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        query ??= new PurchaseOrderQuery();

        var orders = Session.Query<PurchaseOrder, PurchaseOrderIndex>(collection: CollectionName);

        if (query.Status is { } status)
        {
            orders = orders.Where(index => index.Status == status);
        }
        else if (!query.All)
        {
            orders = orders.Where(index => index.Status != PurchaseOrderStatus.Received && index.Status != PurchaseOrderStatus.Canceled);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();

            orders = orders.Where(index => index.Number.StartsWith(search) || index.NormalizedSupplier.StartsWith(search));
        }

        var count = await orders.CountAsync(cancellationToken);
        var page = await orders.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }

    public async Task<IReadOnlyList<string>> ListSuppliersAsync(CancellationToken cancellationToken = default)
        => (await Session.Query<PurchaseOrder, PurchaseOrderIndex>(collection: CollectionName)
                .OrderByDescending(index => index.CreatedUtc)
                .Take(SupplierLookback)
                .ListAsync(cancellationToken))
            .Select(order => order.Supplier?.Trim())
            .Where(supplier => !string.IsNullOrEmpty(supplier))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
