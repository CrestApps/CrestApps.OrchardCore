namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Identifies the administrative level of a tax jurisdiction. The hierarchy is intentionally open so
/// that jurisdictions in different countries can model different levels.
/// </summary>
public enum JurisdictionLevel
{
    /// <summary>
    /// A country level jurisdiction.
    /// </summary>
    Country,

    /// <summary>
    /// A state jurisdiction.
    /// </summary>
    State,

    /// <summary>
    /// A province jurisdiction.
    /// </summary>
    Province,

    /// <summary>
    /// A region jurisdiction.
    /// </summary>
    Region,

    /// <summary>
    /// A county jurisdiction.
    /// </summary>
    County,

    /// <summary>
    /// A city jurisdiction.
    /// </summary>
    City,

    /// <summary>
    /// A special or district jurisdiction (for example a transit district).
    /// </summary>
    Special,

    /// <summary>
    /// Any other jurisdiction level not covered by the well-known values.
    /// </summary>
    Other,
}
