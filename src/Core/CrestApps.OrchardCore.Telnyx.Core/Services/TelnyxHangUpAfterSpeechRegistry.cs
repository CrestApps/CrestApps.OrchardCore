using Microsoft.Extensions.Caching.Distributed;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Remembers, on the server, the legs that are to be hung up once the message being spoken to them ends.
/// </summary>
/// <remarks>
/// Telnyx stamps a command's <c>client_state</c> on every later event of the leg, replacing the state the leg carried.
/// A last message sent with a client state of its own therefore erased what other code reads from the leg -- a
/// recording's correlation to its interaction, for one, so the saved recording could no longer be filed. The message is
/// sent without a client state, and the leg is named here instead; the <c>call.speak.ended</c> webhook claims it. The
/// store is the tenant's distributed cache, so the webhook may land on any node.
/// </remarks>
public sealed class TelnyxHangUpAfterSpeechRegistry
{
    // Longer than any last message takes to read; a leg whose message never reports its end is dropped after this.
    private static readonly TimeSpan _retention = TimeSpan.FromMinutes(10);

    private readonly IDistributedCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxHangUpAfterSpeechRegistry"/> class.
    /// </summary>
    /// <param name="cache">The tenant's distributed cache.</param>
    public TelnyxHangUpAfterSpeechRegistry(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Records that the leg is hung up when the message being spoken to it ends.
    /// </summary>
    /// <param name="callControlId">The leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task MarkAsync(string callControlId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callControlId);

        return _cache.SetAsync(
            Key(callControlId),
            [1],
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _retention },
            cancellationToken);
    }

    /// <summary>
    /// Forgets the leg, for a message that could not be started.
    /// </summary>
    /// <param name="callControlId">The leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public Task ForgetAsync(string callControlId, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(callControlId)
            ? Task.CompletedTask
            : _cache.RemoveAsync(Key(callControlId), cancellationToken);

    /// <summary>
    /// Claims the leg when its message has ended and it was marked to be hung up.
    /// </summary>
    /// <param name="callControlId">The leg whose message ended.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the leg was marked, and is now to be hung up.</returns>
    public async Task<bool> TryClaimAsync(string callControlId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callControlId))
        {
            return false;
        }

        var key = Key(callControlId);

        if (await _cache.GetAsync(key, cancellationToken) is null)
        {
            return false;
        }

        await _cache.RemoveAsync(key, cancellationToken);

        return true;
    }

    private static string Key(string callControlId)
        => $"telnyx:hangup-after-speech:{callControlId.Trim()}";
}
