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
}
