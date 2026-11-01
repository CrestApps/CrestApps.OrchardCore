using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Navigation;

/// <summary>
/// Adds the forecasting pages to the Workforce menu.
/// </summary>
public sealed class ForecastingAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForecastingAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ForecastingAdminMenu(IStringLocalizer<ForecastingAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], S["Workforce"].PrefixPosition("6"), workforce => workforce
            .Add(S["Forecasting"], S["Forecasting"].PrefixPosition("50"), forecasting => forecasting
                .Id("workforceForecasting")
                .Add(S["Forecasts"], S["Forecasts"].PrefixPosition("10"), forecasts => forecasts
                    .Id("workforceForecasts")
                    .Action("Index", "Forecasts", WorkforceContactCenterConstants.Features.Forecasting)
                    .Permission(WorkforcePermissions.ManageForecasts)
                    .LocalNav())
                .Add(S["Planning Groups"], S["Planning Groups"].PrefixPosition("20"), groups => groups
                    .Id("workforcePlanningGroups")
                    .Action("Index", "PlanningGroups", WorkforceContactCenterConstants.Features.Forecasting)
                    .Permission(WorkforcePermissions.ManageForecasts)
                    .LocalNav())
                .Add(S["Forecast Events"], S["Forecast Events"].PrefixPosition("30"), events => events
                    .Id("workforceForecastEvents")
                    .Action("Index", "ForecastEvents", WorkforceContactCenterConstants.Features.Forecasting)
                    .Permission(WorkforcePermissions.ManageForecasts)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
