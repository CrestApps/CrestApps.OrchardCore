using CrestApps.OrchardCore.ExchangeRates.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ExchangeRates.Navigation;

/// <summary>
/// Adds <b>Commerce &gt; Exchange rates</b> and <b>Settings &gt; Commerce &gt; Exchange rates</b>.
/// </summary>
public sealed class ExchangeRatesAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", ExchangeRatesConstants.SettingsGroupId },
    };

    internal readonly IStringLocalizer S;

    public ExchangeRatesAdminMenu(IStringLocalizer<ExchangeRatesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Exchange rates"], S["Exchange rates"].PrefixPosition("4.8"), rates => rates
                    .Id("exchangerates")
                    .Action("Index", "ExchangeRates", new RouteValueDictionary { { "area", ExchangeRatesConstants.Features.Area } })
                    .Permission(ExchangeRatesPermissions.ManageExchangeRates)
                    .LocalNav()))
            .Add(S["Settings"], settings => settings
                .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                    .Add(S["Exchange rates"], S["Exchange rates"].PrefixPosition(), rates => rates
                        .Id("exchangeRateSettings")
                        .Action("Index", "Admin", _settingsRouteValues)
                        .Permission(ExchangeRatesPermissions.ManageExchangeRates)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
