namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Names the distributed lock that serializes writes to one agent's profile.
/// </summary>
/// <remarks>
/// Presence, entitlements, reservations and session cleanup all write the same profile, and they run in
/// different services and in different packages. The key convention is published here so every one of them
/// takes the same lock; a caller that invented its own key would hold a lock nobody else respects.
/// </remarks>
public static class AgentProfileLock
{
    /// <summary>
    /// Returns the lock key for the supplied agent.
    /// </summary>
    /// <param name="userId">The identifier of the agent whose profile is being written.</param>
    /// <returns>The distributed lock key.</returns>
    public static string GetKey(string userId)
    {
        return $"ContactCenterAgentProfile:{userId}";
    }
}
