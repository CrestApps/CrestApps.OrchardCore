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
    /// Makes the caller's keypad private on a call until one entry is finished, cancelled or timed out.
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
/// data protection, offers serialized by a distributed lock per call, and a time limit of
/// <see cref="SecureKeypadCaptures.Expiry"/>.
/// </summary>
/// <remarks>
/// An entry finishes on another node only when the nodes share the distributed cache (<c>OrchardCore.Redis.Cache</c>)
/// and the data protection keys. Whether they do or not, the keys never leak: while an entry is open a
/// <see cref="SecureCaptureWindow"/> is kept on the call's activity, which every node reads, and keys that find no entry
/// this node can use while the window is open are dropped. A node with its own memory cache, a restart in the middle of
/// an entry, an evicted entry or one this node cannot decrypt all end there, never in the caller's turn.
/// <para>
/// An entry not finished within <see cref="SecureKeypadCaptures.Expiry"/> times out rather than ending quietly: the
/// first keys after that tell the AI it timed out, nothing typed is kept, and the window stays until the caller has
/// stopped typing for <see cref="SecureKeypadCaptures.Grace"/>, so the rest of a number typed too late never becomes
/// the caller's turn either. Keys are handed on as the caller's turn only when no window is on the activity.
/// </para>
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
                // entry ends as cancelled, so the AI asks again. The window on the activity stays, so the keys that
                // follow, which find no entry, are dropped too until the AI asks again or the entry times out.
                await _cache.RemoveAsync(key, cancellationToken);

                return new SecureKeypadEntry(SecureKeypadEntryState.Cancelled, SecureCaptureField.Custom, null);
            }

            if (capture is null)
            {
                // No entry here: none was started, or it is in another node's cache, was lost, evicted or could not be
                // read. The window on the activity tells them apart, and while it is there the keys are dropped.
                return await OfferToWindowAsync(activityId, key, cancellationToken);
            }

            if (capture.TimedOutUntilUtc is { } quietUntilUtc)
            {
                // The entry timed out: the keys are dropped until the caller has stopped typing for the grace period.
                if (_clock.UtcNow <= quietUntilUtc)
                {
                    return await TimeOutAsync(activityId, key, capture.Field, alreadyTold: true, cancellationToken);
                }

                // Quiet here, but keys may have landed on a node that doesn't share this cache since: the window, which
                // every node moves on, decides.
                await _cache.RemoveAsync(key, cancellationToken);

                return await OfferToWindowAsync(activityId, key, cancellationToken);
            }

            if (_clock.UtcNow - capture.StartedUtc > SecureKeypadCaptures.Expiry)
            {
                // Too late for this entry, but the keys are still secure data: the entry times out, the AI is told, and
                // the rest of the number is dropped too.
                return await TimeOutAsync(activityId, key, capture.Field, alreadyTold: false, cancellationToken);
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

    // Keys that found no entry this node can use. While a window is on the activity they are dropped: before the entry's
    // time is up as part of the entry, and after it as the entry timing out, which the AI is told about once.
    private async Task<SecureKeypadEntry> OfferToWindowAsync(string activityId, string key, CancellationToken cancellationToken)
    {
        var window = await FindWindowAsync(activityId, cancellationToken);

        if (window is null)
        {
            return SecureKeypadEntry.NotCapturing;
        }

        if (!window.TimedOut && window.UntilUtc >= _clock.UtcNow)
        {
            _logger.LogWarning("A keypad event on AI call activity '{ActivityId}' found no secure entry on this node while the keypad is private, so the keys were dropped.", activityId.SanitizeLogValue());

            return new SecureKeypadEntry(SecureKeypadEntryState.Collecting, window.Field, null);
        }

        if (window.TimedOut && window.UntilUtc < _clock.UtcNow)
        {
            return await CloseAsync(activityId, key, cancellationToken);
        }

        return await TimeOutAsync(activityId, key, window.Field, alreadyTold: window.TimedOut, cancellationToken);
    }

    // Ends an entry that was not finished in time without keeping a digit. The keypad stays private for the grace
    // period from this key, both in the cache, which the next key on a node that shares it finds at once, and in the
    // window on the activity for the nodes that don't; each key typed in that time starts the grace period again. The
    // first keys after the entry's time is up are the ones the AI is told about.
    private async Task<SecureKeypadEntry> TimeOutAsync(string activityId, string key, SecureCaptureField field, bool alreadyTold, CancellationToken cancellationToken)
    {
        var quietUntilUtc = _clock.UtcNow + SecureKeypadCaptures.Grace;
        var marker = new Capture { Field = field, StartedUtc = _clock.UtcNow, TimedOutUntilUtc = quietUntilUtc };

        await _cache.SetAsync(key, _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(marker)), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = SecureKeypadCaptures.Grace }, cancellationToken);
        await SetWindowAsync(activityId, new SecureCaptureWindow { Field = field, UntilUtc = quietUntilUtc, TimedOut = true }, cancellationToken);

        if (!alreadyTold)
        {
            _logger.LogWarning("The secure keypad entry on AI call activity '{ActivityId}' was not finished in time; nothing was kept.", activityId.SanitizeLogValue());
        }

        return new SecureKeypadEntry(alreadyTold ? SecureKeypadEntryState.Collecting : SecureKeypadEntryState.TimedOut, field, null);
    }

    // The caller stopped typing for the whole grace period after a timed-out entry: the keypad is theirs again.
    private async Task<SecureKeypadEntry> CloseAsync(string activityId, string key, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(key, cancellationToken);
        await SetWindowAsync(activityId, window: null, cancellationToken);

        return SecureKeypadEntry.NotCapturing;
    }

    // The entry is kept for the grace period past its own time, so the node that holds it sees it time out even when the
    // window on the activity was lost.
    private async Task WriteAsync(string activityId, Capture capture, CancellationToken cancellationToken)
    {
        var keepFor = SecureKeypadCaptures.Expiry + SecureKeypadCaptures.Grace - (_clock.UtcNow - capture.StartedUtc);

        if (keepFor <= TimeSpan.Zero)
        {
            await _cache.RemoveAsync(GetCacheKey(activityId), cancellationToken);

            return;
        }

        var payload = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(capture));

        await _cache.SetAsync(GetCacheKey(activityId), payload, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = keepFor }, cancellationToken);
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

    private async Task<SecureCaptureWindow> FindWindowAsync(string activityId, CancellationToken cancellationToken)
    {
        var activity = await _activityStore.FindByIdAsync(activityId, cancellationToken);

        return activity is not null && activity.TryGet<SecureCaptureWindow>(out var window) ? window : null;
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

        // Set once the entry timed out: it holds no digits, and keys are dropped until then.
        public DateTime? TimedOutUntilUtc { get; set; }
    }
}
