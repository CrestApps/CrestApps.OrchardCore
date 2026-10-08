using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The reasons recorded when queued work is withdrawn because its activity stopped being routable.
/// </summary>
public static class QueuedWorkWithdrawalReasons
{
    /// <summary>
    /// The activity was deleted.
    /// </summary>
    public const string ActivityDeleted = "ActivityDeleted";

    /// <summary>
    /// Gets the reason for an activity that reached <paramref name="status"/>, such as <c>ActivityPurged</c>.
    /// </summary>
    /// <param name="status">The activity's status.</param>
    /// <returns>The reason.</returns>
    public static string For(ActivityStatus status) => $"Activity{status}";
}
