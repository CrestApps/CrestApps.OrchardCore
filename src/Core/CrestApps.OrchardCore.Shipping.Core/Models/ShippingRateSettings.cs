namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// The settings of a <see cref="ShippingConstants.Providers.Flat"/> method.
/// </summary>
public sealed class FlatRateSettings
{
    /// <summary>
    /// Gets or sets the amount charged once per order.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the amount added for each item shipped.
    /// </summary>
    public decimal PerItemAmount { get; set; }
}

/// <summary>
/// The settings of a <see cref="ShippingConstants.Providers.Table"/> method.
/// </summary>
public sealed class TableRateSettings
{
    public TableRateBasis Basis { get; set; }

    /// <summary>
    /// Gets or sets the rows. The row with the largest <see cref="TableRateRow.From"/> at or below the order's value
    /// applies; an order below every row isn't offered the method.
    /// </summary>
    public List<TableRateRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the value above which the method isn't offered, such as the most a carrier takes, or
    /// <see langword="null"/> for no limit.
    /// </summary>
    public decimal? Maximum { get; set; }
}

public sealed class TableRateRow
{
    /// <summary>
    /// Gets or sets the value, inclusive, from which the row applies.
    /// </summary>
    public decimal From { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// What a table looks the rate up by.
/// </summary>
public enum TableRateBasis
{
    /// <summary>
    /// The total weight of the items shipped, in the store's weight unit.
    /// </summary>
    Weight = 0,

    /// <summary>
    /// The subtotal of the items shipped.
    /// </summary>
    Subtotal = 1,

    /// <summary>
    /// The number of items shipped.
    /// </summary>
    Quantity = 2,
}

/// <summary>
/// The settings of a <see cref="ShippingConstants.Providers.Free"/> method.
/// </summary>
public sealed class FreeShippingSettings
{
    /// <summary>
    /// Gets or sets the subtotal, inclusive, from which shipping is free; zero for always.
    /// </summary>
    public decimal MinimumSubtotal { get; set; }
}
