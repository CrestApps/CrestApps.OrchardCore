using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.PayLater.Navigation;

/// <summary>
/// Adds the Pay Later settings entry to the admin navigation.
/// </summary>
public sealed class PayLaterAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", PayLaterConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayLaterAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayLaterAdminMenu(IStringLocalizer<PayLaterAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Pay Later"], S["Pay Later"].PrefixPosition(), payLater => payLater
                        .AddClass("paylater-settings")
                        .Id("payLaterSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(TransactionsPermissions.ManageTransactionSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
