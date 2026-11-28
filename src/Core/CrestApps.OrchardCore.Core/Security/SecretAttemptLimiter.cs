using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Security;

/// <summary>
/// Limits how often someone may try a secret they type (an enrollment key, an access code, a link's password): after
/// <see cref="MaxFailures"/> wrong tries for one secret within <see cref="Window"/>, that person waits for the same
/// time. Each module names its secrets with a scope (such as <c>Learning:EnrollmentKey</c> or <c>Surveys:Password</c>),
/// the secret's owner (the record it belongs to) and the one trying (a user id, or a visitor's IP address). The state
/// is kept in the distributed cache, so every node of the tenant counts the same tries. A right try clears the count.
/// </summary>
public sealed class SecretAttemptLimiter
{
    /// <summary>
    /// The wrong tries allowed within the window.
    /// </summary>
    public const int MaxFailures = 5;

    /// <summary>
    /// The window the wrong tries are counted in, and how long a locked-out person waits.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly IDistributedCache _cache;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretAttemptLimiter"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache, shared by every node of the tenant.</param>
    /// <param name="clock">The clock.</param>
    public SecretAttemptLimiter(IDistributedCache cache, IClock clock)
    {
        _cache = cache;
        _clock = clock;
    }

    /// <summary>
    /// Gets whether someone must wait before trying a secret again.
    /// </summary>
    /// <param name="scope">The kind of secret, prefixed by the module that owns it.</param>
    /// <param name="ownerId">The record the secret belongs to.</param>
    /// <param name="subject">Who is trying: a user id, or a visitor's IP address.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when locked out.</returns>
    public async Task<bool> IsLockedOutAsync(string scope, string ownerId, string subject, CancellationToken cancellationToken = default)
    {
        var state = await ReadAsync(scope, ownerId, subject, cancellationToken);

        return state?.LockedUntilUtc is { } until && _clock.UtcNow < until;
    }

    /// <summary>
    /// Records a wrong try.
    /// </summary>
    /// <param name="scope">The kind of secret, prefixed by the module that owns it.</param>
    /// <param name="ownerId">The record the secret belongs to.</param>
    /// <param name="subject">Who is trying: a user id, or a visitor's IP address.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when this try locked the person out.</returns>
    public async Task<bool> RecordFailureAsync(string scope, string ownerId, string subject, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var state = await ReadAsync(scope, ownerId, subject, cancellationToken);

        if (state is null || now - state.WindowStartUtc > Window || (state.LockedUntilUtc is { } until && now >= until))
        {
            state = new AttemptState { WindowStartUtc = now };
        }

        state.Failures++;

        if (state.Failures >= MaxFailures)
        {
            state.LockedUntilUtc = now + Window;
        }

        await _cache.SetAsync(Key(scope, ownerId, subject), JsonSerializer.SerializeToUtf8Bytes(state), new DistributedCacheEntryOptions { SlidingExpiration = Window * 4 }, cancellationToken);

        return state.LockedUntilUtc is not null;
    }

    /// <summary>
    /// Clears someone's wrong tries after a right one.
    /// </summary>
    /// <param name="scope">The kind of secret, prefixed by the module that owns it.</param>
    /// <param name="ownerId">The record the secret belongs to.</param>
    /// <param name="subject">Who is trying: a user id, or a visitor's IP address.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task ResetAsync(string scope, string ownerId, string subject, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(Key(scope, ownerId, subject), cancellationToken);

    private async Task<AttemptState> ReadAsync(string scope, string ownerId, string subject, CancellationToken cancellationToken)
    {
        var bytes = await _cache.GetAsync(Key(scope, ownerId, subject), cancellationToken);

        return bytes is null ? null : JsonSerializer.Deserialize<AttemptState>(bytes);
    }

    private static string Key(string scope, string ownerId, string subject)
        => "SecretAttempts:" + scope + ":" + ownerId + ":" + subject;

    private sealed class AttemptState
    {
        public int Failures { get; set; }

        public DateTime WindowStartUtc { get; set; }

        public DateTime? LockedUntilUtc { get; set; }
    }
}
