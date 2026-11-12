using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Throttling;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Throttling;

/// <summary>
/// The attempt limiter over <see cref="IDistributedCache"/> and <see cref="IDistributedLock"/>. Counting happens under a
/// short lock, and a lock that can't be taken refuses. Keys are hashed, so no address, e-mail or code is stored in a cache
/// key, and they carry the tenant's name, so tenants never share a counter. Counters hold across nodes only when the
/// tenant uses a shared cache and lock (Orchard Core's Redis cache and lock features).
/// </summary>
public sealed class DistributedAttemptLimiter : IAttemptLimiter
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(10);

    private readonly IDistributedCache _cache;
    private readonly IDistributedLock _distributedLock;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly AttemptLimitOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedAttemptLimiter"/> class.
    /// </summary>
    public DistributedAttemptLimiter(
        IDistributedCache cache,
        IDistributedLock distributedLock,
        ShellSettings shellSettings,
        IClock clock,
        IOptions<AttemptLimitOptions> options,
        ILogger<DistributedAttemptLimiter> logger)
    {
        _cache = cache;
        _distributedLock = distributedLock;
        _shellSettings = shellSettings;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<AttemptLimitResult> AcquireAsync(string policyName, string key, CancellationToken cancellationToken = default)
        => CountAsync(policyName, key, isFailure: false, cancellationToken);

    /// <inheritdoc/>
    public Task<AttemptLimitResult> RecordFailureAsync(string policyName, string key, CancellationToken cancellationToken = default)
        => CountAsync(policyName, key, isFailure: true, cancellationToken);

    /// <inheritdoc/>
    public async Task<AttemptLimitResult> PeekAsync(string policyName, string key, CancellationToken cancellationToken = default)
    {
        var policy = GetPolicy(policyName);

        if (policy.PermitLimit <= 0)
        {
            return AttemptLimitResult.Unlimited;
        }

        var state = await ReadAsync(GetCacheKey(policy, key), cancellationToken);

        return Evaluate(policy, state, _clock.UtcNow);
    }

    /// <inheritdoc/>
    public async Task ResetAsync(string policyName, string key, CancellationToken cancellationToken = default)
    {
        var policy = GetPolicy(policyName);

        await _cache.RemoveAsync(GetCacheKey(policy, key), cancellationToken);
    }

    private async Task<AttemptLimitResult> CountAsync(string policyName, string key, bool isFailure, CancellationToken cancellationToken)
    {
        var policy = GetPolicy(policyName);

        if (policy.PermitLimit <= 0)
        {
            return AttemptLimitResult.Unlimited;
        }

        var cacheKey = GetCacheKey(policy, key);
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(cacheKey + ":lock", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            // Not being able to account for an attempt most likely coincides with contention or abuse, so it is refused.
            Refused(policy);

            return new AttemptLimitResult(false, 0, policy.Window);
        }

        await using (locker)
        {
            var now = _clock.UtcNow;
            var state = await ReadAsync(cacheKey, cancellationToken);

            if (state is not null && state.BlockedUntilUtc > now)
            {
                Refused(policy);

                return new AttemptLimitResult(false, 0, state.BlockedUntilUtc.Value - now);
            }

            if (state is null || now >= state.WindowEndsUtc)
            {
                state = new State { WindowEndsUtc = now + policy.Window };
            }

            if (!isFailure && state.Count >= policy.PermitLimit)
            {
                Refused(policy);

                return new AttemptLimitResult(false, 0, state.WindowEndsUtc - now);
            }

            state.Count++;

            if (isFailure && state.Count >= policy.PermitLimit && policy.BlockDuration > TimeSpan.Zero)
            {
                state.BlockedUntilUtc = now + policy.BlockDuration;
            }

            var expires = state.BlockedUntilUtc is { } blockedUntil && blockedUntil > state.WindowEndsUtc ? blockedUntil : state.WindowEndsUtc;

            await _cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(state), new DistributedCacheEntryOptions { AbsoluteExpiration = expires }, cancellationToken);

            // An attempt that was counted went ahead, the last permit included. A failure answers whether the key may still
            // try: not once it is blocked or the window is used up.
            if (!isFailure)
            {
                return new AttemptLimitResult(true, Math.Max(0, policy.PermitLimit - state.Count), TimeSpan.Zero);
            }

            var result = Evaluate(policy, state, now);

            if (!result.Allowed)
            {
                Refused(policy);
            }

            return result;
        }
    }

    private static AttemptLimitResult Evaluate(AttemptLimitPolicy policy, State state, DateTime now)
    {
        if (state is null)
        {
            return new AttemptLimitResult(true, policy.PermitLimit, TimeSpan.Zero);
        }

        if (state.BlockedUntilUtc > now)
        {
            return new AttemptLimitResult(false, 0, state.BlockedUntilUtc.Value - now);
        }

        if (now >= state.WindowEndsUtc)
        {
            return new AttemptLimitResult(true, policy.PermitLimit, TimeSpan.Zero);
        }

        var remaining = Math.Max(0, policy.PermitLimit - state.Count);

        return remaining > 0
            ? new AttemptLimitResult(true, remaining, TimeSpan.Zero)
            : new AttemptLimitResult(false, 0, state.WindowEndsUtc - now);
    }

    private async Task<State> ReadAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var data = await _cache.GetAsync(cacheKey, cancellationToken);

        if (data is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<State>(data);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private AttemptLimitPolicy GetPolicy(string policyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(policyName);

        // A missing policy must not mean "unlimited".
        return _options.Policies.TryGetValue(policyName, out var policy)
            ? policy
            : throw new InvalidOperationException($"The attempt policy '{policyName}' is not registered.");
    }

    private string GetCacheKey(AttemptLimitPolicy policy, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"{_shellSettings.Name}:AttemptLimit:{policy.Name}:{hash}";
    }

    private void Refused(AttemptLimitPolicy policy)
    {
        ThrottlingTelemetry.RecordRefusal(policy.Name);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("The attempt policy '{PolicyName}' refused an attempt.", policy.Name);
        }
    }

    private sealed class State
    {
        public int Count { get; set; }

        public DateTime WindowEndsUtc { get; set; }

        public DateTime? BlockedUntilUtc { get; set; }
    }
}
