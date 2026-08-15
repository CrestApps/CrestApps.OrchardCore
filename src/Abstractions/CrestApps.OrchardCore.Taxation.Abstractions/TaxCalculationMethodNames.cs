namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Provides the well-known, extensible tax calculation method identifiers.
/// </summary>
/// <remarks>
/// Calculation methods are resolved by name so that third-party modules can register additional
/// strategies without modifying the taxation framework.
/// </remarks>
public static class TaxCalculationMethodNames
{
    /// <summary>
    /// A percentage applied to the taxable base.
    /// </summary>
    public const string Percentage = "Percentage";

    /// <summary>
    /// A fixed amount charged per taxable line, independent of quantity.
    /// </summary>
    public const string FixedAmount = "FixedAmount";

    /// <summary>
    /// A fixed amount charged for every unit of quantity.
    /// </summary>
    public const string PerUnit = "PerUnit";

    /// <summary>
    /// A fixed amount charged for every unit of weight.
    /// </summary>
    public const string PerWeight = "PerWeight";

    /// <summary>
    /// A fixed amount charged for every unit of volume.
    /// </summary>
    public const string PerVolume = "PerVolume";

    /// <summary>
    /// A progressive, tiered calculation driven by a tax table.
    /// </summary>
    public const string Progressive = "Progressive";

    /// <summary>
    /// A calculation that only applies once a threshold is reached.
    /// </summary>
    public const string Threshold = "Threshold";

    /// <summary>
    /// A lookup calculation that resolves a rate or amount from a tax table row.
    /// </summary>
    public const string TaxTable = "TaxTable";
}
