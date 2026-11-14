using YesSql;

namespace CrestApps.OrchardCore.Disbursements.Core.Services;

/// <summary>
/// Commits the session's writes.
/// </summary>
internal sealed class YesSqlDisbursementCommitter : IDisbursementCommitter
{
    private readonly ISession _session;

    public YesSqlDisbursementCommitter(ISession session)
    {
        _session = session;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _session.SaveChangesAsync(cancellationToken);
}
