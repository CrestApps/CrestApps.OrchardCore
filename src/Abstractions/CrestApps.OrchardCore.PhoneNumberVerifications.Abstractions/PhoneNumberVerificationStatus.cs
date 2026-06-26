namespace CrestApps.OrchardCore.PhoneNumberVerifications;

/// <summary>
/// Represents the verification status of a phone number.
/// </summary>
public enum PhoneNumberVerificationStatus
{
    /// <summary>
    /// The phone number has not been verified yet.
    /// </summary>
    Unverified = 0,

    /// <summary>
    /// The phone number was verified and is considered valid.
    /// </summary>
    Verified = 1,

    /// <summary>
    /// The phone number was verified and is considered invalid.
    /// </summary>
    Invalid = 2,

    /// <summary>
    /// A verification attempt was made but the provider could not complete it.
    /// </summary>
    Failed = 3,
}
