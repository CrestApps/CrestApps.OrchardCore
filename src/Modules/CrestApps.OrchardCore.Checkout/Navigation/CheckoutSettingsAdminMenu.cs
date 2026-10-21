using CrestApps.OrchardCore.Checkout.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Checkout.Navigation;

/// <summary>
/// Settings › Commerce › Checkout: how the checkout is laid out for the buyer.
/// </summary>
public sealed class CheckoutSettingsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", CheckoutConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutSettingsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckoutSettingsAdminMenu(IStringLocalizer<CheckoutSettingsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Checkout"], S["Checkout"].PrefixPosition(), checkout => checkout
                        .Id("checkoutSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(CheckoutPermissions.ManageCheckoutSettings)
                        .LocalNav()
                    )
                )
            );

        return ValueTask.CompletedTask;
    }
}
