namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Extensions over <see cref="ActivityStatus"/> that give every caller one definition of "this activity is
/// finished", so a status added later cannot be treated as still running by some code paths and as finished by
/// others.
/// </summary>
public static class ActivityStatusExtensions
{
    /// <summary>
    /// Determines whether the status is terminal: the activity has finished and will not progress again, whether
    /// it completed, was cancelled, failed, or was purged.
    /// </summary>
    /// <param name="status">The activity status.</param>
    /// <returns><see langword="true"/> when the activity has finished.</returns>
    public static bool IsTerminal(this ActivityStatus status)
        => status is ActivityStatus.Completed
            or ActivityStatus.Cancelled
            or ActivityStatus.Failed
            or ActivityStatus.Purged;
}
