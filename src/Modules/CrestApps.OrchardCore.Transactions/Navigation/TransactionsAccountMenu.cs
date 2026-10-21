using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Transactions.Navigation;

/// <summary>
/// Adds what the customer owes and has paid, with their invoices and receipts, to the storefront account.
/// </summary>
public sealed class TransactionsAccountMenu : NamedNavigationProvider
{
    internal readonly IStringLocalizer S;

    public TransactionsAccountMenu(IStringLocalizer<TransactionsAccountMenu> stringLocalizer)
        : base(CommerceConstants.Account.MenuName)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Balances and invoices"], "40", transactions => transactions
            .Id("account-transactions")
            .AddClass("fa-solid fa-file-invoice-dollar")
            .Url("~/my-transactions")
            .Permission(TransactionsPermissions.ViewOwnTransactions));

        return ValueTask.CompletedTask;
    }
}
