namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Orchestrates call recording state for interactions. It owns the recording lifecycle and audit events;
/// provider modules execute the media capture.
/// </summary>
public interface IContactCenterRecordingService
{
    /// <summary>
    /// Starts recording the interaction.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the recording state changed; otherwise, <see langword="false"/>.</returns>
    Task<bool> StartAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses recording (for example while sensitive data is captured).
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the recording state changed; otherwise, <see langword="false"/>.</returns>
    Task<bool> PauseAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused recording.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the recording state changed; otherwise, <see langword="false"/>.</returns>
    Task<bool> ResumeAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops recording the interaction.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the recording state changed; otherwise, <see langword="false"/>.</returns>
    Task<bool> StopAsync(string interactionId, CancellationToken cancellationToken = default);
}
