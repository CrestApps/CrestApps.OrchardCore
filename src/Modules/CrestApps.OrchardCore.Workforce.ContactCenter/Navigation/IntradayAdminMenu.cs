using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Navigation;

/// <summary>
/// Adds the intraday page to the Workforce menu.
/// </summary>
public sealed class IntradayAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntradayAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public IntradayAdminMenu(IStringLocalizer<IntradayAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], S["Workforce"].PrefixPosition("6"), workforce => workforce
            .Add(S["Forecasting"], S["Forecasting"].PrefixPosition("50"), forecasting => forecasting
                .Add(S["Intraday"], S["Intraday"].PrefixPosition("05"), intraday => intraday
                    .Id("workforceIntraday")
                    .Action("Index", "Intraday", WorkforceContactCenterConstants.Features.Intraday)
                    .Permission(WorkforcePermissions.ManageForecasts)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
