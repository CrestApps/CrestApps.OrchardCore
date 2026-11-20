namespace CrestApps.OrchardCore.Agreements.Models;

/// <summary>
/// Where an agreement stands.
/// </summary>
public enum AgreementStatus
{
    /// <summary>
    /// Being written; not in force.
    /// </summary>
    Draft,

    /// <summary>
    /// In force.
    /// </summary>
    Active,

    /// <summary>
    /// In force but not used for now.
    /// </summary>
    Paused,

    /// <summary>
    /// Past its end date.
    /// </summary>
    Expired,

    /// <summary>
    /// Ended before its end date.
    /// </summary>
    Canceled,
}
