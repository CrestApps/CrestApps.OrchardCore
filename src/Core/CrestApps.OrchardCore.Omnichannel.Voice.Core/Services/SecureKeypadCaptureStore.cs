using CrestApps.Core.Support;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The secure keypad entries of a tenant's AI calls, kept where every node finds them, so keys a caller types while
/// the keypad is private are captured whichever node the provider's keypad event lands on, and never become the
/// caller's turn, the transcript or a log.
/// </summary>
internal interface ISecureKeypadCaptureStore
{
    /// <summary>
    /// Makes the caller's keypad private on a call until one entry is finished, cancelled or expired.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="field">What the caller is asked to type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task StartAsync(string activityId, SecureCaptureField field, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offers keys the caller pressed to the capture on their call.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="digits">The keys.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What became of the keys.</returns>
    Task<SecureKeypadEntry> OfferAsync(string activityId, string digits, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops collecting on a call, such as when it ends.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ForgetAsync(string activityId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="ISecureKeypadCaptureStore"/>: the tenant's distributed cache, with the entry encrypted by
/// data protection, offers serialized by a distributed lock per call, and an expiry of
/// <see cref="SecureKeypadCaptures.Expiry"/>. On several nodes it needs the shared cache and lock (Redis) the voice call
/// message bus already needs, and shared data protection keys.
/// </summary>
internal sealed class DistributedSecureKeypadCaptureStore : ISecureKeypadCaptureStore
{
    private const string ProtectorPurpose = "CrestApps.OrchardCore.Omnichannel.Voice.SecureKeypadCapture";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(10);

    private readonly IDistributedCache _cache;
    private readonly IDistributedLock _distributedLock;
    private readonly IDataProtector _protector;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DistributedSecureKeypadCaptureStore(
        IDistributedCache cache,
        IDistributedLock distributedLock,
        IDataProtectionProvider dataProtectionProvider,
        ShellSettings shellSettings,
        IClock clock,
        ILogger<DistributedSecureKeypadCaptureStore> logger)
    {
        _cache = cache;
        _distributedLock = distributedLock;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _shellSettings = shellSettings;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(string activityId, SecureCaptureField field, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityId);

        await WriteAsync(activityId, new Capture { Field = field, StartedUtc = _clock.UtcNow }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SecureKeypadEntry> OfferAsync(string activityId, string digits, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityId))
        {
            return SecureKeypadEntry.NotCapturing;
        }

        var key = GetCacheKey(activityId);
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(key + ":lock", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            // The keys may be secure data: never hand them on as the caller's turn when they can't be checked.
            _logger.LogWarning("The secure keypad capture of AI call activity '{ActivityId}' could not be locked, so the keys were dropped.", activityId.SanitizeLogValue());

            return new SecureKeypadEntry(SecureKeypadEntryState.Collecting, SecureCaptureField.Custom, null);
        }

        await using (locker)
        {
            var (capture, unreadable) = await ReadAsync(key, cancellationToken);

            if (unreadable)
            {
                // An entry this node can't read is still an open capture: the keys stay out of the conversation and the
                // entry ends as cancelled, so the AI asks again.
                await _cache.RemoveAsync(key, cancellationToken);

                return new SecureKeypadEntry(SecureKeypadEntryState.Cancelled, SecureCaptureField.Custom, null);
            }

            if (capture is null)
            {
                return SecureKeypadEntry.NotCapturing;
            }

            if (_clock.UtcNow - capture.StartedUtc > SecureKeypadCaptures.Expiry)
            {
                await _cache.RemoveAsync(key, cancellationToken);

                return SecureKeypadEntry.NotCapturing;
            }

            var maxLength = SecureKeypadCaptures.MaxLength(capture.Field);

            foreach (var pressed in digits ?? string.Empty)
            {
                if (pressed == '*')
                {
                    await _cache.RemoveAsync(key, cancellationToken);

                    return new SecureKeypadEntry(SecureKeypadEntryState.Cancelled, capture.Field, null);
                }

                if (pressed == '#' || capture.Value.Length >= maxLength)
                {
                    return await CompleteAsync(key, capture, cancellationToken);
                }

                if (char.IsAsciiDigit(pressed))
                {
                    capture.Value += pressed;
                }
            }

            if (capture.Value.Length >= maxLength)
            {
                return await CompleteAsync(key, capture, cancellationToken);
            }

            await WriteAsync(activityId, capture, cancellationToken);

            return new SecureKeypadEntry(SecureKeypadEntryState.Collecting, capture.Field, null);
        }
    }

    /// <inheritdoc/>
    public Task ForgetAsync(string activityId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(activityId) ? Task.CompletedTask : _cache.RemoveAsync(GetCacheKey(activityId), cancellationToken);

    private async Task<SecureKeypadEntry> CompleteAsync(string key, Capture capture, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(key, cancellationToken);

        return new SecureKeypadEntry(SecureKeypadEntryState.Completed, capture.Field, capture.Value);
    }

    private async Task WriteAsync(string activityId, Capture capture, CancellationToken cancellationToken)
    {
        var remaining = SecureKeypadCaptures.Expiry - (_clock.UtcNow - capture.StartedUtc);

        if (remaining <= TimeSpan.Zero)
        {
            await _cache.RemoveAsync(GetCacheKey(activityId), cancellationToken);

            return;
        }

        var payload = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(capture));

        await _cache.SetAsync(GetCacheKey(activityId), payload, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining }, cancellationToken);
    }

    private async Task<(Capture Capture, bool Unreadable)> ReadAsync(string key, CancellationToken cancellationToken)
    {
        var payload = await _cache.GetAsync(key, cancellationToken);

        if (payload is null)
        {
            return (null, false);
        }

        try
        {
            return (JsonSerializer.Deserialize<Capture>(_protector.Unprotect(payload)), false);
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or JsonException)
        {
            _logger.LogWarning("A secure keypad capture could not be read; it was ended.");

            return (null, true);
        }
    }

    private string GetCacheKey(string activityId)
        => $"{_shellSettings.Name}:SecureKeypadCapture:{activityId}";

    private sealed class Capture
    {
        public SecureCaptureField Field { get; set; }

        public DateTime StartedUtc { get; set; }

        public string Value { get; set; } = string.Empty;
    }
}
