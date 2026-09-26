namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The result of resolving what an agent typed into the transfer field into a destination the platform is
/// willing to hand a live call to.
/// </summary>
public sealed class TransferTargetDecision
{
    private TransferTargetDecision(bool isAllowed, string resolvedTarget, string reason)
    {
        IsAllowed = isAllowed;
        ResolvedTarget = resolvedTarget;
        Reason = reason;
    }

    /// <summary>
    /// Gets a value indicating whether the transfer may proceed.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Gets the destination to hand to the provider, which may differ from what the agent typed when the raw
    /// value was a logical identifier such as a catalog entry.
    /// </summary>
    public string ResolvedTarget { get; }

    /// <summary>
    /// Gets the reason the transfer was refused, or <see langword="null"/> when it was allowed.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Creates an allowing decision.
    /// </summary>
    /// <param name="resolvedTarget">The destination to hand to the provider.</param>
    /// <returns>The decision.</returns>
    public static TransferTargetDecision Allow(string resolvedTarget)
        => new(true, resolvedTarget, null);

    /// <summary>
    /// Creates a refusing decision.
    /// </summary>
    /// <param name="reason">The reason to show the agent.</param>
    /// <returns>The decision.</returns>
    public static TransferTargetDecision Refuse(string reason)
        => new(false, null, reason);
}
