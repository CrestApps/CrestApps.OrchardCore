namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Records what an authenticated user's soft-phone client reported it can do, against the browser credential it is
/// registered on (see <see cref="TelephonyConstants.SoftPhoneClientCapabilities"/>).
/// </summary>
/// <remarks>
/// A server that dials the agent's browser in a way only a newer client understands must know the client listening
/// on the credential is one of those. An older client -- a cached page, a browser extension that has not updated --
/// reports nothing, and is dialed the way it always was.
/// </remarks>
public interface ISoftPhoneClientCapabilityRegistrar
{
    /// <summary>
    /// Records the capabilities the user's client reported for a credential, replacing any it reported before.
    /// Implementations only act on a credential they own for that user.
    /// </summary>
    /// <param name="userId">The authenticated user the credential must belong to.</param>
    /// <param name="credentialId">The provider credential identifier the client registered on.</param>
    /// <param name="capabilities">The capabilities, already limited to the known ones.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a credential owned by the user was updated; otherwise <see langword="false"/>.</returns>
    Task<bool> ReportCapabilitiesAsync(
        string userId,
        string credentialId,
        IReadOnlyCollection<string> capabilities,
        CancellationToken cancellationToken = default);
}
