namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// The unit a recurring price's billing cycle is measured in.
/// </summary>
/// <remarks>
/// The catalog says "every three months" in its own words and stays independent of any purchasing
/// pipeline. Whatever a gateway calls the same idea is a payment concern, and the checkout that bridges the
/// two is where the translation belongs.
/// </remarks>
public enum BillingInterval
{
    /// <summary>
    /// Days.
    /// </summary>
    Day = 0,

    /// <summary>
    /// Weeks.
    /// </summary>
    Week = 1,

    /// <summary>
    /// Months.
    /// </summary>
    Month = 2,

    /// <summary>
    /// Years.
    /// </summary>
    Year = 3,
}
