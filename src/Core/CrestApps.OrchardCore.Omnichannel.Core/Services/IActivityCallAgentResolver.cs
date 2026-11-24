namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Finds the agent a call of an activity was placed or connected for, so a follow-up can go to that agent even when the
/// system, not the agent, applied the disposition. The Contact Center implements it; without one, nobody is found.
/// </summary>
public interface IActivityCallAgentResolver
{
    /// <summary>
    /// Finds the user of the agent the activity's call was placed or connected for.
    /// </summary>
    /// <param name="activityItemId">The activity.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The user id, or <see langword="null"/> when no call of the activity names an agent.</returns>
    Task<string> FindAgentUserIdAsync(string activityItemId, CancellationToken cancellationToken = default);
}
