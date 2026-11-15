using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Limits how often a person may try an enrollment key: after five wrong keys for one course within fifteen minutes,
/// that person waits fifteen minutes. Each try costs a key derivation, so the limit also bounds the work a caller can
/// make the server do.
/// </summary>
public sealed class EnrollmentKeyAttempts
{
    /// <summary>
    /// The wrong keys allowed within the window.
    /// </summary>
    public const int MaxFailures = 5;

    /// <summary>
    /// The window and the lockout.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly IDistributedCache _cache;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentKeyAttempts"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache, shared by every node of the tenant.</param>
    /// <param name="clock">The clock.</param>
    public EnrollmentKeyAttempts(IDistributedCache cache, IClock clock)
    {
        _cache = cache;
        _clock = clock;
    }

    /// <summary>
    /// Gets whether a person must wait before trying a key of a course again.
    /// </summary>
    /// <param name="userId">The person.</param>
    /// <param name="courseId">The course.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when locked out.</returns>
    public async Task<bool> IsLockedOutAsync(string userId, string courseId, CancellationToken cancellationToken = default)
    {
        var state = await ReadAsync(userId, courseId, cancellationToken);

        return state?.LockedUntilUtc is { } until && _clock.UtcNow < until;
    }

    /// <summary>
    /// Records a wrong key.
    /// </summary>
    /// <param name="userId">The person.</param>
    /// <param name="courseId">The course.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task RecordFailureAsync(string userId, string courseId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var state = await ReadAsync(userId, courseId, cancellationToken);

        if (state is null || now - state.WindowStartUtc > Window || (state.LockedUntilUtc is { } until && now >= until))
        {
            state = new AttemptState { WindowStartUtc = now };
        }

        state.Failures++;

        if (state.Failures >= MaxFailures)
        {
            state.LockedUntilUtc = now + Window;
        }

        await _cache.SetAsync(Key(userId, courseId), JsonSerializer.SerializeToUtf8Bytes(state), new DistributedCacheEntryOptions { SlidingExpiration = Window * 4 }, cancellationToken);
    }

    /// <summary>
    /// Clears the wrong keys of a person after a right one.
    /// </summary>
    /// <param name="userId">The person.</param>
    /// <param name="courseId">The course.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task ResetAsync(string userId, string courseId, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(Key(userId, courseId), cancellationToken);

    private async Task<AttemptState> ReadAsync(string userId, string courseId, CancellationToken cancellationToken)
    {
        var bytes = await _cache.GetAsync(Key(userId, courseId), cancellationToken);

        return bytes is null ? null : JsonSerializer.Deserialize<AttemptState>(bytes);
    }

    private static string Key(string userId, string courseId)
        => "Learning:EnrollmentKeyAttempts:" + userId + ":" + courseId;

    private sealed class AttemptState
    {
        public int Failures { get; set; }

        public DateTime WindowStartUtc { get; set; }

        public DateTime? LockedUntilUtc { get; set; }
    }
}
