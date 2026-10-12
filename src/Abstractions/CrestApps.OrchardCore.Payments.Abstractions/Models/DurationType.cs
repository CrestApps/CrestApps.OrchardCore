namespace CrestApps.OrchardCore.Payments.Models;

/// <summary>
/// Specifies the unit used to measure a billing duration.
/// </summary>
public enum DurationType
{
    /// <summary>
    /// Measures the billing duration in years.
    /// </summary>
    Year,

    /// <summary>
    /// Measures the billing duration in months.
    /// </summary>
    Month,

    /// <summary>
    /// Measures the billing duration in weeks.
    /// </summary>
    Week,

    /// <summary>
    /// Measures the billing duration in days.
    /// </summary>
    Day,
}
