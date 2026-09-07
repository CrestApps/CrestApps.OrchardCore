using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Transactions.Navigation;

/// <summary>
/// Adds the payments and refunds ledgers to the Commerce admin menu.
/// </summary>
/// <remarks>
/// These entries are registered only alongside the Checkout feature, because the ledgers they open are
/// written by the checkout engine. Showing them without it would be a menu item that leads to an empty page
/// nothing can ever fill.
/// </remarks>
public sealed class PaymentLedgerAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _paymentsRouteValues = new()
    {
        { "area", TransactionsConstants.Features.Area },
        { "controller", "PaymentsAdmin" },
        { "action", "Index" },
    };

    private static readonly RouteValueDictionary _refundsRouteValues = new()
    {
        { "area", TransactionsConstants.Features.Area },
        { "controller", "RefundsAdmin" },
        { "action", "Index" },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentLedgerAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentLedgerAdminMenu(IStringLocalizer<PaymentLedgerAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Payments"], S["Payments"].PrefixPosition("7"), payments => payments
                    .AddClass("payments")
                    .Id("payments")
                    .Action("Index", "PaymentsAdmin", _paymentsRouteValues)
                    .Permission(TransactionsPermissions.ManageRefunds)
                    .LocalNav()
                )
                .Add(S["Refunds"], S["Refunds"].PrefixPosition("8"), refunds => refunds
                    .AddClass("refunds")
                    .Id("refunds")
                    .Action("Index", "RefundsAdmin", _refundsRouteValues)
                    .Permission(TransactionsPermissions.ManageRefunds)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
