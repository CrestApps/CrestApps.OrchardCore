namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Tells whether a user has a phone open that a call can ring. A voice provider that knows where its users' phones
/// are registered answers; the agent's screens use it to tell an agent who is signed in to take calls that no phone of
/// theirs is open, before a caller is offered to nobody.
/// </summary>
public interface IAgentPhoneStatusProvider
{
    /// <summary>
    /// Returns whether a phone of the user's is open and registered with the provider.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when one is open, <see langword="false"/> when none is, and <see langword="null"/>
    /// when this provider cannot tell, for example because it is not configured.</returns>
    Task<bool?> HasOpenPhoneAsync(string userId, CancellationToken cancellationToken = default);
}
