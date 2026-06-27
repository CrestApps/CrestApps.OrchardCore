namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// Defines the status filters available on the phone number verification records queue.
/// </summary>
public enum PhoneNumberVerificationRecordFilter
{
    /// <summary>
    /// Shows all records that carry a phone number.
    /// </summary>
    All = 0,

    /// <summary>
    /// Shows records whose phone number was verified and is valid.
    /// </summary>
    Verified = 1,

    /// <summary>
    /// Shows records whose phone number was verified and is invalid.
    /// </summary>
    Invalid = 2,

    /// <summary>
    /// Shows records whose most recent verification request failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Shows records that have never completed a verification.
    /// </summary>
    Pending = 4,

    /// <summary>
    /// Shows records that have reached the maximum failed attempts and are no longer retried automatically.
    /// </summary>
    NeedsAttention = 5,
}
