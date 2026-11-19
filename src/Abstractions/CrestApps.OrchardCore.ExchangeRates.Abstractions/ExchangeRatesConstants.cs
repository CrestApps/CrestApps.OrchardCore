namespace CrestApps.OrchardCore.ExchangeRates;

/// <summary>
/// The feature ids and names of the Exchange Rates module.
/// </summary>
public static class ExchangeRatesConstants
{
    /// <summary>
    /// The settings group of <c>ExchangeRateSettings</c>.
    /// </summary>
    public const string SettingsGroupId = "exchangeRates";

    /// <summary>
    /// The source of a rate staff entered by hand.
    /// </summary>
    public const string ManualSource = "manual";

    /// <summary>
    /// The YesSql collection the rates are kept in.
    /// </summary>
    public const string CollectionName = "ExchangeRates";

    public static class Features
    {
        /// <summary>
        /// The Exchange Rates feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.ExchangeRates";
    }
}
