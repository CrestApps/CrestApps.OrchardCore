using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Orders.Navigation;

/// <summary>
/// Adds the customer's orders to the storefront account.
/// </summary>
public sealed class OrdersAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public OrdersAccountMenu(IStringLocalizer<OrdersAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Orders"], "10", orders => orders
            .Id("account-orders")
            .AddClass("fa-solid fa-box")
            .Action("Index", "AccountOrders", new { area = "CrestApps.OrchardCore.Orders" })
            .Permission(OrderPermissions.ViewOwnOrders));

        return ValueTask.CompletedTask;
    }
}
