namespace CrestApps.OrchardCore.YesSql.Core.Retention;

/// <summary>
/// What one retention cycle did for one policy.
/// </summary>
public sealed class RetentionPolicyResult
{
    /// <summary>
    /// Gets or sets the name of the policy.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the policy removes records at all.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the cutoff the cycle used.
    /// </summary>
    public DateTime? CutoffUtc { get; set; }

    /// <summary>
    /// Gets or sets how many records the cycle removed and committed.
    /// </summary>
    public int PurgedCount { get; set; }

    /// <summary>
    /// Gets or sets whether expired records are left, because the batch budget ran out or a batch failed.
    /// </summary>
    public bool WorkRemains { get; set; }
}
