using CrestApps.OrchardCore.Markets.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Markets.Navigation;

/// <summary>
/// Commerce › Markets.
/// </summary>
public sealed class MarketsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _markets = new()
    {
        { "area", MarketsConstants.Feature },
        { "controller", typeof(MarketsAdminController).ControllerName() },
        { "action", nameof(MarketsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public MarketsAdminMenu(IStringLocalizer<MarketsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
                .Add(S["Markets"], S["Markets"].PrefixPosition("2"), markets => markets
                    .AddClass("commerce-markets")
                    .Id("commerceMarkets")
                    .Action(_markets)
                    .Permission(MarketsPermissions.ManageMarkets)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
