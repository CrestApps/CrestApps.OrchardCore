using CrestApps.OrchardCore.Products.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Adds the currencies screen to the shared Commerce admin menu.
/// </summary>
internal sealed class CurrencyAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CurrencyAdminMenu(IStringLocalizer<CurrencyAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Currencies"], S["Currencies"].PrefixPosition("1"), currencies => currencies
                    .AddClass("commerce-currencies")
                    .Id("commerceCurrencies")
                .Action("Index", "Currencies", ProductConstants.Feature.ModuleId)
                    .Permission(ProductsConstants.Permissions.ManageCurrencies)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
