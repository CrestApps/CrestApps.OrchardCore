namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// A volume discount: from <see cref="MinQuantity"/> units on, each unit costs <see cref="PercentOff"/> percent less.
/// </summary>
public sealed class ProductPriceTier
{
    /// <summary>
    /// Gets or sets the quantity the discount starts at.
    /// </summary>
    public int MinQuantity { get; set; }

    /// <summary>
    /// Gets or sets the percentage taken off each unit, from 0 to 100.
    /// </summary>
    public decimal PercentOff { get; set; }
}

/// <summary>
/// A discount the buyers in a role get on the product, such as wholesale pricing.
/// </summary>
public sealed class ProductRoleDiscount
{
    /// <summary>
    /// Gets or sets the role's name.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the percentage taken off each unit, from 0 to 100.
    /// </summary>
    public decimal PercentOff { get; set; }
}
