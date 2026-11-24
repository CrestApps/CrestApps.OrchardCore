using YesSql;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

/// <summary>
/// Commits the session's writes.
/// </summary>
internal sealed class YesSqlCheckCommitter : ICheckCommitter
{
    private readonly ISession _session;

    public YesSqlCheckCommitter(ISession session)
    {
        _session = session;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _session.SaveChangesAsync(cancellationToken);
}
