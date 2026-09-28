namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Raised when a purge batch fails partway through staging its deletes. The deletes staged before the failure are
/// still in the session's unit of work, so the count is carried out with the exception: without it the caller
/// cannot attribute or commit that work, and an unrelated entity's later flush would commit it anonymously.
/// </summary>
public sealed class ContactCenterRetentionBatchException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRetentionBatchException"/> class.
    /// </summary>
    /// <param name="entityName">The entity whose batch failed.</param>
    /// <param name="stagedBeforeFailure">The number of deletes already staged when the failure occurred.</param>
    /// <param name="innerException">The failure that interrupted the batch.</param>
    public ContactCenterRetentionBatchException(
        string entityName,
        int stagedBeforeFailure,
        Exception innerException)
        : base($"Purging entity '{entityName}' failed after staging {stagedBeforeFailure} deletes.", innerException)
    {
        EntityName = entityName;
        StagedBeforeFailure = stagedBeforeFailure;
    }

    /// <summary>
    /// Gets the entity whose batch failed.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Gets the number of deletes already staged in the session when the failure occurred.
    /// </summary>
    public int StagedBeforeFailure { get; }
}
