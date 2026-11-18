namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// Commits what the approval service wrote while it holds the lock of a subject or a request, so the next holder reads
/// it.
/// </summary>
public interface IApprovalCommitter
{
    /// <summary>
    /// Commits the pending writes.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Commits the session's writes.
/// </summary>
internal sealed class YesSqlApprovalCommitter : IApprovalCommitter
{
    private readonly global::YesSql.ISession _session;

    public YesSqlApprovalCommitter(global::YesSql.ISession session)
    {
        _session = session;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _session.SaveChangesAsync(cancellationToken);
}
