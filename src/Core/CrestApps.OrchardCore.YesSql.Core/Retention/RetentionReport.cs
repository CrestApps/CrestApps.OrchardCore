namespace CrestApps.OrchardCore.YesSql.Core.Retention;

/// <summary>
/// What one retention cycle did.
/// </summary>
public sealed class RetentionReport
{
    /// <summary>
    /// Gets the result of every policy the cycle reached, and of those it could not reach.
    /// </summary>
    public IList<RetentionPolicyResult> Policies { get; } = [];

    /// <summary>
    /// Gets how many records the cycle removed in all.
    /// </summary>
    public int TotalPurged
        => Policies.Sum(policy => policy.PurgedCount);

    /// <summary>
    /// Gets whether any policy still has expired records.
    /// </summary>
    public bool WorkRemains
        => Policies.Any(policy => policy.WorkRemains);
}
