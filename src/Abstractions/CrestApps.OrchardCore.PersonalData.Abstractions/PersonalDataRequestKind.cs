namespace CrestApps.OrchardCore.PersonalData;

/// <summary>
/// Why a person's records are anonymized.
/// </summary>
public enum PersonalDataRequestKind
{
    /// <summary>
    /// An explicit erasure request, such as an admin erasing a customer. Every contributor takes part.
    /// </summary>
    Erasure = 0,

    /// <summary>
    /// A retention window ran out, such as the nightly expiry of inactive guests. Only the contributors whose
    /// <see cref="IPersonalDataContributor.TakesPartInRetentionExpiry"/> is <see langword="true"/> take part.
    /// </summary>
    RetentionExpiry = 1,
}
