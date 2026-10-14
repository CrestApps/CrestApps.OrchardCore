namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Indicates whether an amount already includes tax or excludes it.
/// </summary>
public enum TaxPriceType
{
    /// <summary>
    /// The amount excludes tax; tax is added on top of the amount.
    /// </summary>
    Exclusive,

    /// <summary>
    /// The amount already includes tax; tax is extracted from the amount.
    /// </summary>
    Inclusive,
}
