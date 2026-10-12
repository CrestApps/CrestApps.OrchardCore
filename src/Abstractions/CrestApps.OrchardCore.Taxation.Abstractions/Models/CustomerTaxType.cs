namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Classifies a customer for taxation purposes.
/// </summary>
public enum CustomerTaxType
{
    /// <summary>
    /// A business-to-consumer customer.
    /// </summary>
    B2C,

    /// <summary>
    /// A business-to-business customer.
    /// </summary>
    B2B,
}
