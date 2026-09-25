using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The default <see cref="ITelephonyVoicemailSendGuard"/>. A claim is a marker in the tenant's distributed cache, set
/// under the tenant's distributed lock so two requests on any nodes cannot both see the call unclaimed.
/// </summary>
/// <remarks>
/// The guard fails open: a lock or cache that cannot be reached lets the request through, because a caller greeted
/// twice is better than a caller who can never leave a message.
/// </remarks>
public sealed class TelephonyVoicemailSendGuard : ITelephonyVoicemailSendGuard
{
    /// <summary>
    /// How long a call is remembered as sent to voicemail: longer than any greeting and message, so a request that
    /// arrives while the caller is still leaving it is refused.
    /// </summary>
    public static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(15);

    // The lock is held only while the marker is read and written, never across the provider call.
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);
    private static readonly byte[] _marker = [1];

    private readonly IDistributedLock _distributedLock;
    private readonly IDistributedCache _cache;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyVoicemailSendGuard"/> class.
    /// </summary>
    /// <param name="distributedLock">The tenant's distributed lock, which makes a claim atomic across nodes.</param>
    /// <param name="cache">The tenant's distributed cache, which remembers the calls already sent to voicemail.</param>
    /// <param name="logger">The logger.</param>
    public TelephonyVoicemailSendGuard(
        IDistributedLock distributedLock,
        IDistributedCache cache,
        ILogger<TelephonyVoicemailSendGuard> logger)
    {
        _distributedLock = distributedLock;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> TryClaimAsync(string callId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);

        var key = BuildKey(callId);

        try
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key + ":lock", _lockTimeout, _lockExpiration);

            if (!locked)
            {
                _logger.LogWarning(
                    "Could not take the voicemail claim lock for call {CallId}; sending it to voicemail without the duplicate guard.",
                    callId.SanitizeLogValue());

                return true;
            }

            await using (locker)
            {
                if (await _cache.GetAsync(key, cancellationToken) is not null)
                {
                    return false;
                }

                await _cache.SetAsync(key, _marker, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ClaimLifetime,
                }, cancellationToken);

                return true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not record the voicemail claim for call {CallId}; sending it to voicemail without the duplicate guard.",
                callId.SanitizeLogValue());

            return true;
        }
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(string callId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);

        try
        {
            await _cache.RemoveAsync(BuildKey(callId), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The claim then simply lapses with its lifetime; until it does, a retry for this call is refused.
            _logger.LogWarning(ex, "Could not release the voicemail claim for call {CallId}.", callId.SanitizeLogValue());
        }
    }

    private static string BuildKey(string callId)
        => "telephony:voicemail-sent:" + callId;
}
