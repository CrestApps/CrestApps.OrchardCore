using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Markets;

public static class MarketsPermissions
{
    /// <summary>
    /// Adds and changes markets: their countries, currency and exchange rate, rounding, language and tax.
    /// </summary>
    public static readonly Permission ManageMarkets = new(
        "ManageMarkets",
        LocalizationSource.Create("Manage markets", typeof(MarketsPermissions)));
}
