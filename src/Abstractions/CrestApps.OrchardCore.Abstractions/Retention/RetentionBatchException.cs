namespace CrestApps.OrchardCore.Retention;

/// <summary>
/// Thrown by <see cref="IRetentionPolicy.PurgeBatchAsync"/> when a record failed after the batch had staged work in the
/// session, so the caller must discard the whole batch instead of committing part of it.
/// </summary>
public sealed class RetentionBatchException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetentionBatchException"/> class.
    /// </summary>
    /// <param name="policyName">The name of the policy whose batch failed.</param>
    /// <param name="stagedCount">How many records the batch had staged before it failed, for diagnostics.</param>
    /// <param name="innerException">The failure.</param>
    public RetentionBatchException(string policyName, int stagedCount, Exception innerException)
        : base($"Retention failed while purging '{policyName}' after {stagedCount} records were staged; the batch was discarded.", innerException)
    {
        PolicyName = policyName;
        StagedCount = stagedCount;
    }

    /// <summary>
    /// Gets the name of the policy whose batch failed.
    /// </summary>
    public string PolicyName { get; }

    /// <summary>
    /// Gets how many records the batch had staged before it failed.
    /// </summary>
    public int StagedCount { get; }
}
