namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a coverage requirement comes from.
/// </summary>
public enum CoverageSource
{
    /// <summary>
    /// Entered by hand.
    /// </summary>
    Manual,

    /// <summary>
    /// Worked out from a contact center forecast.
    /// </summary>
    Forecast,

    /// <summary>
    /// Worked out from field work waiting to be done.
    /// </summary>
    Workload,
}
