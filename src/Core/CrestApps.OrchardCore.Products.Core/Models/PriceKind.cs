namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// Whether a price is charged once or on a repeating schedule.
/// </summary>
/// <remarks>
/// This is what makes a product subscribable. A product carrying a recurring price can be sold as a
/// subscription without a separate "is a plan" flag, which is why the same product can offer a one-time
/// purchase and a monthly plan side by side.
/// </remarks>
public enum PriceKind
{
    /// <summary>
    /// Charged once.
    /// </summary>
    OneTime = 0,

    /// <summary>
    /// Charged every billing interval until the agreement ends.
    /// </summary>
    Recurring = 1,
}
