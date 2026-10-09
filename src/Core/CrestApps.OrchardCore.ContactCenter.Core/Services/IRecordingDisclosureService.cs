namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Supplies the recording disclosure and records that a caller has been given it.
/// </summary>
/// <remarks>
/// Giving the disclosure is what captures the caller's consent to the recording, so a tenant that will not record
/// until consent is captured starts the recording here once the caller has heard it.
/// </remarks>
public interface IRecordingDisclosureService : IRecordingDisclosureProvider
{
    /// <summary>
    /// Records that the caller on an interaction has been told the call is recorded, and captures their consent.
    /// </summary>
    /// <param name="interactionId">The interaction whose caller was given the disclosure.</param>
    /// <param name="method">How it was given: a <see cref="ContactCenterConstants.RecordingDisclosureMethod"/> value.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the disclosure was recorded; <see langword="false"/> when the interaction is gone or already had it.</returns>
    Task<bool> RecordDisclosedAsync(string interactionId, string method, CancellationToken cancellationToken = default);
}
