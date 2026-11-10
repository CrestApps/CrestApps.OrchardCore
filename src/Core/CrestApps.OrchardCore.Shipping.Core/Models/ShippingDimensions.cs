namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// The size of one unit, packed: its length, width and height, in centimeters for a store weighing in kilograms and
/// inches otherwise.
/// </summary>
public sealed class ShippingDimensions
{
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    public decimal Length { get; set; }

    /// <summary>
    /// Gets or sets the width.
    /// </summary>
    public decimal Width { get; set; }

    /// <summary>
    /// Gets or sets the height.
    /// </summary>
    public decimal Height { get; set; }

    /// <summary>
    /// Returns the unit dimensions are in for a store's weight unit: <c>cm</c> for kilograms, <c>in</c> for pounds.
    /// </summary>
    /// <param name="weightUnit">The store's weight unit.</param>
    public static string GetUnit(WeightUnit weightUnit)
        => weightUnit == WeightUnit.Pound ? "in" : "cm";
}
