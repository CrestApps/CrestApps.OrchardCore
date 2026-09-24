using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Represents an active bidirectional media session attached to a provider call.
/// </summary>
public interface IContactCenterVoiceMediaSession : IAsyncDisposable
{
    /// <summary>
    /// Gets the provider-assigned media-session identifier.
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Gets the provider call identifier associated with the session.
    /// </summary>
    string ProviderCallId { get; }

    /// <summary>
    /// Gets the format used for caller audio received from the provider.
    /// </summary>
    ContactCenterVoiceMediaFormat IncomingFormat { get; }

    /// <summary>
    /// Gets the format required for audio written back to the provider.
    /// </summary>
    ContactCenterVoiceMediaFormat OutgoingFormat { get; }

    /// <summary>
    /// Reads incoming caller-audio frames until the session ends or cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The incoming audio-frame stream.</returns>
    IAsyncEnumerable<ContactCenterVoiceMediaFrame> ReadIncomingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes an application-generated audio frame to the live call.
    /// </summary>
    /// <param name="frame">The audio frame to write.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask WriteOutgoingAsync(
        ContactCenterVoiceMediaFrame frame,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards application audio already written to the call that the caller has not heard yet.
    /// </summary>
    /// <remarks>
    /// A speech model produces its voice faster than it plays, so several seconds of it can be waiting at the
    /// provider when the caller starts talking over it. Stopping the model does not take back what is already
    /// queued there, and the caller hears the rest of the sentence they tried to interrupt. A provider that buffers
    /// outgoing audio and can discard it overrides this; one that plays audio as it arrives keeps the default, which
    /// does nothing. Audio written after this call plays as usual.
    /// </remarks>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask ClearOutgoingAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Stops the media session without ending the underlying provider call.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
