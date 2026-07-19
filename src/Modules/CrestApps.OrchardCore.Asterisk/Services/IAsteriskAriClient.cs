using CrestApps.OrchardCore.Asterisk.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Defines tenant-scoped Asterisk ARI operations needed by Contact Center orchestration.
/// </summary>
internal interface IAsteriskAriClient
{
    /// <summary>
    /// Originates a channel into the tenant's configured ARI application.
    /// </summary>
    /// <param name="request">The originate request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The originated or existing ARI channel.</returns>
    Task<AsteriskAriChannel> OriginateAsync(AsteriskAriOriginateRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a bridge with the specified deterministic identifier, or returns the existing bridge.
    /// </summary>
    /// <param name="bridgeId">The deterministic ARI bridge identifier.</param>
    /// <param name="bridgeType">The bridge type to create.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The created or existing ARI bridge.</returns>
    Task<AsteriskAriBridge> CreateBridgeAsync(string bridgeId, string bridgeType, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a channel to a bridge.
    /// </summary>
    /// <param name="bridgeId">The ARI bridge identifier.</param>
    /// <param name="channelId">The ARI channel identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task AddChannelToBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a channel from a bridge.
    /// </summary>
    /// <param name="bridgeId">The ARI bridge identifier.</param>
    /// <param name="channelId">The ARI channel identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RemoveChannelFromBridgeAsync(string bridgeId, string channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Answers a channel.
    /// </summary>
    /// <param name="channelId">The ARI channel identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task AnswerAsync(string channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Hangs up a channel if it exists.
    /// </summary>
    /// <param name="channelId">The ARI channel identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task HangupAsync(string channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether a channel exists.
    /// </summary>
    /// <param name="channelId">The ARI channel identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the channel exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> ChannelExistsAsync(string channelId, CancellationToken cancellationToken);

    /// <summary>
    /// Destroys a bridge if it exists.
    /// </summary>
    /// <param name="bridgeId">The ARI bridge identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task DestroyBridgeAsync(string bridgeId, CancellationToken cancellationToken);

    /// <summary>
    /// Starts recording the media mixed on a bridge, or returns the already-running recording when a recording with
    /// the same name is already in progress.
    /// </summary>
    /// <param name="bridgeId">The ARI bridge identifier whose mixed media should be recorded.</param>
    /// <param name="recordingName">The deterministic recording name that addresses the live recording.</param>
    /// <param name="format">The media format to record in.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The started or already-running live recording.</returns>
    Task<AsteriskAriLiveRecording> StartBridgeRecordingAsync(
        string bridgeId,
        string recordingName,
        string format,
        CancellationToken cancellationToken);

    /// <summary>
    /// Pauses a live recording.
    /// </summary>
    /// <param name="recordingName">The recording name that addresses the live recording.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task PauseBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken);

    /// <summary>
    /// Resumes a paused live recording.
    /// </summary>
    /// <param name="recordingName">The recording name that addresses the live recording.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task UnpauseBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken);

    /// <summary>
    /// Stops a live recording if it exists and returns the resulting stored recording when it can be read.
    /// </summary>
    /// <param name="recordingName">The recording name that addresses the live recording.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The stored recording captured for the name, or <see langword="null"/> when no live or stored recording exists
    /// (a stop of an already-gone recording is treated as an idempotent no-op success).
    /// </returns>
    Task<AsteriskAriStoredRecording> StopBridgeRecordingAsync(string recordingName, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a snoop channel that spies on a live channel and enters the tenant's configured Stasis application,
    /// so an originated supervisor endpoint can be bridged to it to hear (and, for a whisper engagement, speak into)
    /// the spied channel.
    /// </summary>
    /// <param name="channelId">The identifier of the live channel to snoop (the agent leg).</param>
    /// <param name="spy">The direction of audio the snoop channel receives from the spied channel.</param>
    /// <param name="whisper">The direction of audio the snoop channel injects into the spied channel.</param>
    /// <param name="snoopId">The deterministic identifier to assign to the created snoop channel.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The created snoop channel.</returns>
    Task<AsteriskAriChannel> SnoopChannelAsync(
        string channelId,
        string spy,
        string whisper,
        string snoopId,
        CancellationToken cancellationToken);
}
