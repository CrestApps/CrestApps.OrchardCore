using YesSql;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Commits what Payroll changed while it holds a pay run's lock, so the next caller waiting for the lock reads it.
/// </summary>
public interface IPayrollCommitter
{
    /// <summary>
    /// Commits the session.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes once committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Commits the YesSql session.
/// </summary>
internal sealed class YesSqlPayrollCommitter : IPayrollCommitter
{
    private readonly ISession _session;

    public YesSqlPayrollCommitter(ISession session)
    {
        _session = session;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _session.SaveChangesAsync(cancellationToken);
}
