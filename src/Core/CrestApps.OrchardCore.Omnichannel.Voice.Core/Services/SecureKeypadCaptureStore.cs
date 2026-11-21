using CrestApps.Core;
using CrestApps.Core.Support;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The secure keypad entries of a tenant's AI calls, kept where every node finds them, so keys a caller types while
/// the keypad is private are captured whichever node the provider's keypad event lands on, and never become the
/// caller's turn, the transcript or a log.
/// </summary>
public interface ISecureKeypadCaptureStore
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
/// <see cref="SecureKeypadCaptures.Expiry"/>.
/// </summary>
/// <remarks>
/// An entry finishes on another node only when the nodes share the distributed cache (<c>OrchardCore.Redis.Cache</c>)
/// and the data protection keys. Whether they do or not, the keys never leak: while an entry is open a
/// <see cref="SecureCaptureWindow"/> is kept on the call's activity, which every node reads, and keys that find no entry
/// this node can use while the window is open are dropped. A node with its own memory cache, a restart in the middle of
/// an entry, an evicted entry or one this node cannot decrypt all end there, never in the caller's turn.
/// <para>
/// The window is opened in a scope of its own and committed at once, on a fresh read of the activity, and tried again
/// when another write wins. On a live call the entry is asked for from the call's scope, which holds the copy of the
/// activity it read when the call began; a second entry saved onto that copy lost to the keypad event that closed the
/// first one, and the call swallowed the conflict, so the second entry had no window. On a turn-based call the request
/// that asks is committed first, so the window's own write never waits on it.
/// </para>
/// </remarks>
internal sealed class DistributedSecureKeypadCaptureStore : ISecureKeypadCaptureStore
{
    private const string ProtectorPurpose = "CrestApps.OrchardCore.Omnichannel.Voice.SecureKeypadCapture";
    private const int WindowWriteAttempts = 3;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(10);

    private readonly IDistributedCache _cache;
    private readonly IOmnichannelActivityStore _activityStore;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IDataProtector _protector;
    private readonly Func<Func<IServiceProvider, Task>, Task> _inScopeOfItsOwn;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DistributedSecureKeypadCaptureStore(
        IDistributedCache cache,
        IOmnichannelActivityStore activityStore,
        ISession session,
        IDistributedLock distributedLock,
        IDataProtectionProvider dataProtectionProvider,
        IShellHost shellHost,
        ShellSettings shellSettings,
        IClock clock,
        ILogger<DistributedSecureKeypadCaptureStore> logger)
        : this(cache, activityStore, session, distributedLock, dataProtectionProvider, InShellScope(shellHost, shellSettings), shellSettings, clock, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance with the way a write of its own gets a scope, for tests.
    /// </summary>
    internal DistributedSecureKeypadCaptureStore(
        IDistributedCache cache,
        IOmnichannelActivityStore activityStore,
        ISession session,
        IDistributedLock distributedLock,
        IDataProtectionProvider dataProtectionProvider,
        Func<Func<IServiceProvider, Task>, Task> inScopeOfItsOwn,
        ShellSettings shellSettings,
        IClock clock,
        ILogger<DistributedSecureKeypadCaptureStore> logger)
    {
        _cache = cache;
        _activityStore = activityStore;
        _session = session;
        _distributedLock = distributedLock;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _inScopeOfItsOwn = inScopeOfItsOwn;
        _shellSettings = shellSettings;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(string activityId, SecureCaptureField field, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(activityId);

        var now = _clock.UtcNow;

        await CommitTheAskingScopeAsync(activityId, cancellationToken);

        // The window goes on the activity first, committed: once it is there no node hands the keys on, whatever its
        // cache holds.
        await OpenWindowAsync(activityId, new SecureCaptureWindow { Field = field, UntilUtc = now + SecureKeypadCaptures.Expiry }, cancellationToken);
        await WriteAsync(activityId, new Capture { Field = field, StartedUtc = now }, cancellationToken);
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
                // entry ends as cancelled, so the AI asks again. The window on the activity stays open, so the keys
                // that follow, which find no entry, are dropped too until the AI asks again or the window expires.
                await _cache.RemoveAsync(key, cancellationToken);

                return new SecureKeypadEntry(SecureKeypadEntryState.Cancelled, SecureCaptureField.Custom, null);
            }

            if (capture is null)
            {
                // No entry here: none was started, or it is in another node's cache, was lost or could not be read.
                // The window on the activity tells them apart, and while it is open the keys are dropped.
                if (await IsWindowOpenAsync(activityId, cancellationToken))
                {
                    _logger.LogWarning("A keypad event on AI call activity '{ActivityId}' found no secure entry on this node while the keypad is private, so the keys were dropped.", activityId.SanitizeLogValue());

                    return new SecureKeypadEntry(SecureKeypadEntryState.Collecting, SecureCaptureField.Custom, null);
                }

                return SecureKeypadEntry.NotCapturing;
            }

            if (_clock.UtcNow - capture.StartedUtc > SecureKeypadCaptures.Expiry)
            {
                await _cache.RemoveAsync(key, cancellationToken);
                await SetWindowAsync(activityId, window: null, cancellationToken);

                return SecureKeypadEntry.NotCapturing;
            }

            var maxLength = SecureKeypadCaptures.MaxLength(capture.Field);

            foreach (var pressed in digits ?? string.Empty)
            {
                if (pressed == '*')
                {
                    await _cache.RemoveAsync(key, cancellationToken);
                    await SetWindowAsync(activityId, window: null, cancellationToken);

                    return new SecureKeypadEntry(SecureKeypadEntryState.Cancelled, capture.Field, null);
                }

                if (pressed == '#' || capture.Value.Length >= maxLength)
                {
                    return await CompleteAsync(activityId, key, capture, cancellationToken);
                }

                if (char.IsAsciiDigit(pressed))
                {
                    capture.Value += pressed;
                }
            }

            if (capture.Value.Length >= maxLength)
            {
                return await CompleteAsync(activityId, key, capture, cancellationToken);
            }

            await WriteAsync(activityId, capture, cancellationToken);

            return new SecureKeypadEntry(SecureKeypadEntryState.Collecting, capture.Field, null);
        }
    }

    /// <inheritdoc/>
    public async Task ForgetAsync(string activityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityId))
        {
            return;
        }

        await _cache.RemoveAsync(GetCacheKey(activityId), cancellationToken);

        // Written only when a window is still open, so a call that never asked for a secure entry costs nothing more.
        await SetWindowAsync(activityId, window: null, cancellationToken);
    }

    private async Task<SecureKeypadEntry> CompleteAsync(string activityId, string key, Capture capture, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(key, cancellationToken);
        await SetWindowAsync(activityId, window: null, cancellationToken);

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

    private async Task<bool> IsWindowOpenAsync(string activityId, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        return activity is not null &&
            activity.TryGet<SecureCaptureWindow>(out var window) &&
            window.UntilUtc > _clock.UtcNow;
    }

    // Opens the window on a fresh read of the activity in a scope of its own, committed before the caller is asked to
    // type, so it doesn't depend on the scope that asked: a live call's scope holds an old copy of the activity, and its
    // commit of that copy loses to any write since. A write that loses a race is tried again in a new scope, since a
    // session that lost one can't be used again; one that keeps losing fails the entry rather than leave it unguarded.
    private async Task OpenWindowAsync(string activityId, SecureCaptureWindow window, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _inScopeOfItsOwn(async services =>
                {
                    var activityStore = services.GetRequiredService<IOmnichannelActivityStore>();
                    var activity = await activityStore.FindByIdAsync(activityId, cancellationToken);

                    if (activity is null)
                    {
                        return;
                    }

                    activity.Put(window);
                    await activityStore.UpdateAsync(activity, cancellationToken);
                    await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);
                });

                return;
            }
            catch (ConcurrencyException) when (attempt < WindowWriteAttempts)
            {
                // Another write won; the next attempt reads what it wrote.
            }
        }
    }

    // A turn-based call asks for an entry from the request of the caller's turn, and that request may hold a write it has
    // not committed: the keypad event that finished the entry before closed its window there, and the turn stored the
    // caller's words. Its transaction then holds the activity's row (on SQLite, the whole database), and the window's
    // write in a scope of its own would wait on the request that is waiting for it. So what the request did so far is
    // committed first. A live call is left alone: its scope commits after each turn under a lock of its own, from other
    // threads, and holds no write across the response.
    private async Task CommitTheAskingScopeAsync(string activityId, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        if (activity is not null && activity.TryGet<AILiveCall>(out var liveCall) && liveCall.IsRealtime)
        {
            return;
        }

        await _session.SaveChangesAsync(cancellationToken);
    }

    // Closes the window, through the scope that finished the entry: the keypad event's request, which commits when it
    // ends. A window that can't be closed only keeps later keys out of the conversation until it expires.
    private async Task SetWindowAsync(string activityId, SecureCaptureWindow window, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        if (activity is null || (window is null && !activity.Has<SecureCaptureWindow>()))
        {
            return;
        }

        if (window is null)
        {
            activity.Remove<SecureCaptureWindow>();
        }
        else
        {
            activity.Put(window);
        }

        await _activityStore.UpdateAsync(activity, cancellationToken);
    }

    // A tenant scope of its own, which outlives the request or the call that asks for it.
    private static Func<Func<IServiceProvider, Task>, Task> InShellScope(IShellHost shellHost, ShellSettings shellSettings)
        => async work =>
        {
            var scope = await shellHost.GetScopeAsync(shellSettings);

            await scope.UsingAsync(childScope => work(childScope.ServiceProvider));
        };

    private string GetCacheKey(string activityId)
        => $"{_shellSettings.Name}:SecureKeypadCapture:{activityId}";

    private sealed class Capture
    {
        public SecureCaptureField Field { get; set; }

        public DateTime StartedUtc { get; set; }

        public string Value { get; set; } = string.Empty;
    }
}
