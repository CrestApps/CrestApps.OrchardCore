namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Reads the agent directory without exposing the Contact Center agent profile, so channel modules (messaging, SMS,
/// carriers) can name the agent a user is without referencing the Contact Center implementation. It is registered by
/// the Agent Services feature, which owns the agent directory.
/// </summary>
public interface IAgentProfileReader
{
    /// <summary>
    /// Returns the identifier of the agent profile linked to a user, or <see langword="null"/> when the user is not an
    /// agent.
    /// </summary>
    /// <param name="userId">The user's identifier.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The agent profile's identifier, or <see langword="null"/>.</returns>
    Task<string> FindAgentIdByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
