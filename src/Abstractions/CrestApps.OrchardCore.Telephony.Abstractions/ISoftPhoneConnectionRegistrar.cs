namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Ties a soft phone's credential registration to the hub connection that reported it, so the platform can tell a
/// registration whose window is still open from one whose window has closed.
/// </summary>
/// <remarks>
/// One user can have the soft phone open in several windows, each registered on a credential of its own. Calls go to
/// the most recently registered one, so closing that window left calls going to a credential nothing was listening
/// on until another window happened to register again. Implemented alongside <see cref="ISoftPhoneCredentialRegistrar"/>
/// by a registrar that can use it.
/// </remarks>
public interface ISoftPhoneConnectionRegistrar
{
    /// <summary>
    /// Records that the user's client completed registration on the specified credential, through the specified
    /// soft-phone connection. Implementations only act on a credential they own for that user.
    /// </summary>
    /// <param name="userId">The authenticated user the credential must belong to.</param>
    /// <param name="credentialId">The provider credential identifier the client registered on.</param>
    /// <param name="connectionId">The soft-phone hub connection that reported the registration.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a credential owned by the user was marked registered; otherwise <see langword="false"/>.</returns>
    Task<bool> ReportRegisteredAsync(
        string userId,
        string credentialId,
        string connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a soft-phone hub connection of the user's closed, so the credentials it registered on are
    /// preferred after those registered by a connection still open.
    /// </summary>
    /// <param name="userId">The authenticated user the connection belonged to.</param>
    /// <param name="connectionId">The connection that closed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReportConnectionClosedAsync(
        string userId,
        string connectionId,
        CancellationToken cancellationToken = default);
}
