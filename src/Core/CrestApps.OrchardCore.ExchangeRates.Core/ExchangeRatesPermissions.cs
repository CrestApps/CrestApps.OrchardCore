using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ExchangeRates.Core;

/// <summary>
/// The permissions of the Exchange Rates feature.
/// </summary>
public static class ExchangeRatesPermissions
{
    /// <summary>
    /// Enter, change and delete exchange rates, and change their settings.
    /// </summary>
    public static readonly Permission ManageExchangeRates = new(
        "ManageExchangeRates",
        LocalizationSource.Create("Manage exchange rates", typeof(ExchangeRatesPermissions)));

    /// <summary>
    /// Every permission of the feature.
    /// </summary>
    public static readonly IReadOnlyList<Permission> All = [ManageExchangeRates];
}
