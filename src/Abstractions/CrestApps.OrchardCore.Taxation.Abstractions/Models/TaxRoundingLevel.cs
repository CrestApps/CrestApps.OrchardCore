namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Determines the level at which tax amounts are rounded during a calculation.
/// </summary>
public enum TaxRoundingLevel
{
    /// <summary>
    /// Round every tax line independently.
    /// </summary>
    Line,

    /// <summary>
    /// Round the accumulated amount for each tax type.
    /// </summary>
    Tax,

    /// <summary>
    /// Round the accumulated amount for each jurisdiction.
    /// </summary>
    Jurisdiction,

    /// <summary>
    /// Round only the final transaction total.
    /// </summary>
    Transaction,
}
