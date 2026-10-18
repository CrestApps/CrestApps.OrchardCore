namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// Settings › Commerce › Shipping.
/// </summary>
public sealed class ShippingSettings
{
    /// <summary>
    /// Gets or sets the unit product weights and weight tables are in.
    /// </summary>
    public WeightUnit WeightUnit { get; set; } = WeightUnit.Kilogram;

    /// <summary>
    /// Gets or sets the shipping classes products can be in, such as <em>Bulky</em>, which methods can charge extra for.
    /// </summary>
    public List<string> ShippingClasses { get; set; } = [];
}

public enum WeightUnit
{
    Kilogram = 0,
    Pound = 1,
}
