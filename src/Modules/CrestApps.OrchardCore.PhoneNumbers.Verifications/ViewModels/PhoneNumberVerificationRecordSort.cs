namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// Defines the sort orders available on the phone number verification queue.
/// </summary>
public enum PhoneNumberVerificationRecordSort
{
    /// <summary>
    /// Orders records by the most recent verification attempt first.
    /// </summary>
    RecentlyAttempted = 0,

    /// <summary>
    /// Orders records by the least recent verification attempt first.
    /// </summary>
    LeastRecentlyAttempted = 1,

    /// <summary>
    /// Orders records by the most recently created content item first.
    /// </summary>
    RecentlyCreated = 2,

    /// <summary>
    /// Orders records by the oldest created content item first.
    /// </summary>
    OldestCreated = 3,
}
