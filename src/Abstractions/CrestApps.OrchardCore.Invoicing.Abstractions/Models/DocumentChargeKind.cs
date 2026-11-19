namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// What a <see cref="DocumentCharge"/> is.
/// </summary>
public enum DocumentChargeKind
{
    /// <summary>
    /// A discount on the whole document, spread over its lines in proportion so the tax of each rate stays right.
    /// </summary>
    Discount = 0,

    /// <summary>
    /// Shipping, taxed with its own tax category.
    /// </summary>
    Shipping = 1,

    /// <summary>
    /// Another charge on the whole document, taxed with its own tax category.
    /// </summary>
    Surcharge = 2,
}
