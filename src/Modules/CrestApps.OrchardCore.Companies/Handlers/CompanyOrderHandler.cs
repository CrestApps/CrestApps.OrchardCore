using CrestApps.OrchardCore.Companies.Services;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Companies.Handlers;

/// <summary>
/// Records the company an order is placed for, when its buyer buys for one, so the company sees it and pays it on its
/// terms.
/// </summary>
public sealed class CompanyOrderHandler : OrderEventHandlerBase
{
    private readonly CompanyService _companies;

    public CompanyOrderHandler(CompanyService companies)
    {
        _companies = companies;
    }

    public override async Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var order = context.Order;

        if (string.IsNullOrEmpty(order.CompanyId) && await _companies.GetForUserAsync(order.UserId, cancellationToken) is { } company)
        {
            order.CompanyId = company.ItemId;
        }
    }
}
