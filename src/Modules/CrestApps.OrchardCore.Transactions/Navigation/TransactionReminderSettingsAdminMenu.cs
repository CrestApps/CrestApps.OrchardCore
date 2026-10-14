using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Transactions.Navigation;

/// <summary>
/// Adds the transaction reminder settings entry to the admin navigation. It is registered by the
/// Transactions notification feature so the reminder settings appear only when reminders are enabled.
/// </summary>
public sealed class TransactionReminderSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", TransactionsConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReminderSettingsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionReminderSettingsAdminMenu(IStringLocalizer<TransactionReminderSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
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
