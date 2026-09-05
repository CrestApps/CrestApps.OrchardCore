namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Classifies the privacy sensitivity of a Contact Center data category so retention, access, and erasure
/// obligations can be reasoned about per entity.
/// </summary>
public enum ContactCenterDataSensitivity
{
    /// <summary>
    /// The category holds no personal data — only operational, aggregate, or configuration values.
    /// </summary>
    NonPersonal,

    /// <summary>
    /// The category holds personal data that identifies or relates to an individual, such as a phone number
    /// or an agent identity.
    /// </summary>
    Personal,

    /// <summary>
    /// The category holds sensitive personal data whose exposure carries elevated risk, such as call
    /// recordings or free-text notes that may capture special-category information.
    /// </summary>
    SensitivePersonal,
}
