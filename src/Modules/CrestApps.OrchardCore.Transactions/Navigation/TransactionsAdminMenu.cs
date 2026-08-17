using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Transactions.Navigation;

/// <summary>
/// Adds the transactions report and the transaction reminder settings entries to the admin navigation.
/// </summary>
public sealed class TransactionsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _reportRouteValues = new()
    {
        { "area", TransactionsConstants.Features.Area },
        { "controller", "Admin" },
        { "action", "Index" },
    };

    private static readonly RouteValueDictionary _myTransactionsRouteValues = new()
    {
        { "area", TransactionsConstants.Features.Area },
        { "controller", "Transaction" },
        { "action", "Index" },
    };

    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", TransactionsConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionsAdminMenu(IStringLocalizer<TransactionsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Transactions"], S["Transactions"].PrefixPosition("5"), transactions => transactions
                    .AddClass("transactions")
                    .Id("transactions")
                    .Action("Index", "Admin", _reportRouteValues)
                    .Permission(TransactionsPermissions.ManageTransactions)
                    .LocalNav()
                )
                .Add(S["My Transactions"], S["My Transactions"].PrefixPosition("6"), myTransactions => myTransactions
                    .AddClass("my-transactions")
                    .Id("myTransactions")
                    .Action("Index", "Transaction", _myTransactionsRouteValues)
                    .Permission(TransactionsPermissions.ViewOwnTransactions)
                    .LocalNav()
                )
            );

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Transactions"], S["Transactions"].PrefixPosition(), transactions => transactions
                        .AddClass("transactions-settings")
                        .Id("transactionsSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(TransactionsPermissions.ManageTransactionSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
