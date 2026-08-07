using CrestApps.OrchardCore.ContactCenter.Core.Models;

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
    /// <returns>The outcome of the recording state change, including an explicit indeterminate result when the command was interrupted.</returns>
    Task<RecordingCommandResult> StartAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses recording (for example while sensitive data is captured).
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the recording state change, including an explicit indeterminate result when the command was interrupted.</returns>
    Task<RecordingCommandResult> PauseAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused recording.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the recording state change, including an explicit indeterminate result when the command was interrupted.</returns>
    Task<RecordingCommandResult> ResumeAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops recording the interaction.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome of the recording state change, including an explicit indeterminate result when the command was interrupted.</returns>
    Task<RecordingCommandResult> StopAsync(string interactionId, CancellationToken cancellationToken = default);
}
