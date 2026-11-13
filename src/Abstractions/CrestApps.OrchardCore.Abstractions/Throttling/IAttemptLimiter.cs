namespace CrestApps.OrchardCore.Throttling;

/// <summary>
/// Counts attempts and failures against named policies: begin-payment calls per network, declined cards, wrong codes,
/// order lookups. It knows outcomes, which the request rate limiter can't, and holds across nodes when the tenant uses a
/// shared cache and lock. Never write another attempt counter: register a policy with
/// <see cref="AttemptLimitOptions.AddPolicy"/> and call this.
/// </summary>
public interface IAttemptLimiter
{
    /// <summary>
    /// Counts one attempt against the policy and key. Refuses when the window is full or the key is blocked. Fails
    /// closed when the counter can't be reached.
    /// </summary>
    /// <param name="policyName">The registered policy.</param>
    /// <param name="key">What is counted, such as a network or a checkout. It is hashed before it is stored.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AttemptLimitResult> AcquireAsync(string policyName, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the state without counting, for example to decide whether to show a challenge or to refuse before work.
    /// </summary>
    /// <param name="policyName">The registered policy.</param>
    /// <param name="key">What is counted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AttemptLimitResult> PeekAsync(string policyName, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts one failure (a decline, a wrong code). When the policy's limit is reached the key is blocked for the
    /// policy's <see cref="AttemptLimitPolicy.BlockDuration"/>.
    /// </summary>
    /// <param name="policyName">The registered policy.</param>
    /// <param name="key">What is counted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AttemptLimitResult> RecordFailureAsync(string policyName, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the counter, after a success for a policy that sets <see cref="AttemptLimitPolicy.ResetOnSuccess"/>.
    /// </summary>
    /// <param name="policyName">The registered policy.</param>
    /// <param name="key">What is counted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResetAsync(string policyName, string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// A named limit on attempts.
/// </summary>
public sealed class AttemptLimitPolicy
{
    /// <summary>
    /// Gets or sets the name the policy is registered and overridden by.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets how many attempts or failures a key may have in a window. Zero or less disables the policy.
    /// </summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// Gets or sets the window the limit applies to.
    /// </summary>
    public TimeSpan Window { get; set; }

    /// <summary>
    /// Gets or sets how long a key that reached the limit stays blocked. Zero refuses until the window ends.
    /// </summary>
    public TimeSpan BlockDuration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a success clears the counter.
    /// </summary>
    public bool ResetOnSuccess { get; set; }
}

/// <summary>
/// The answer of the attempt limiter.
/// </summary>
/// <param name="Allowed">Whether the attempt may go ahead.</param>
/// <param name="Remaining">How many attempts are left in the window.</param>
/// <param name="RetryAfter">How long to wait before trying again, when refused.</param>
public readonly record struct AttemptLimitResult(bool Allowed, int Remaining, TimeSpan RetryAfter)
{
    /// <summary>
    /// An attempt that may go ahead with no limit applied.
    /// </summary>
    public static AttemptLimitResult Unlimited { get; } = new(true, int.MaxValue, TimeSpan.Zero);

    /// <summary>
    /// Gets a value indicating whether this failure is the one that used up the window and blocked the key. Exactly one
    /// caller sees it for each block, so it is the moment to tell the staff; a refusal because the counter couldn't be
    /// reached never sets it.
    /// </summary>
    public bool ReachedLimit { get; init; }
}

/// <summary>
/// The registered attempt policies. Code registers each with its defaults; the shell configuration section
/// <c>CrestApps_Throttling:AttemptPolicies</c> overrides any of them by name, without a deployment.
/// </summary>
public sealed class AttemptLimitOptions
{
    /// <summary>
    /// The configuration section that overrides policies by name.
    /// </summary>
    public const string SectionName = "CrestApps_Throttling:AttemptPolicies";

    /// <summary>
    /// Gets the policies by name.
    /// </summary>
    public IDictionary<string, AttemptLimitPolicy> Policies { get; } = new Dictionary<string, AttemptLimitPolicy>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a policy, or replaces the one with the same name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="permitLimit">How many attempts a key may have in a window; zero or less disables it.</param>
    /// <param name="window">The window.</param>
    /// <param name="blockDuration">How long a key that reached the limit stays blocked.</param>
    /// <param name="resetOnSuccess">Whether a success clears the counter.</param>
    public AttemptLimitOptions AddPolicy(string name, int permitLimit, TimeSpan window, TimeSpan blockDuration = default, bool resetOnSuccess = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (permitLimit > 0 && window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "An attempt policy that limits needs a window longer than zero.");
        }

        Policies[name] = new AttemptLimitPolicy
        {
            Name = name,
            PermitLimit = permitLimit,
            Window = window,
            BlockDuration = blockDuration,
            ResetOnSuccess = resetOnSuccess,
        };

        return this;
    }
}
