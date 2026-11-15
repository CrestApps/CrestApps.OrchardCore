using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Runs a write to an enrollment or its progress under one distributed lock per enrollment. The work reads the
/// enrollment again inside the lock, the session is committed before the lock is let go, so the next writer reads the
/// change, and the observers it queued run after the commit, only for what this request changed.
/// </summary>
/// <remarks>
/// The commit is the request's: <see cref="ISession.SaveChangesAsync(CancellationToken)"/> writes everything pending in
/// the session, not only the enrollment. That is what the player and the admin pages want. A caller in the middle of a
/// larger unit of work (a workflow task, a pipeline step, a recipe step, an order handler) must finish its own writes
/// first, or accept that they are committed with the enrollment; an order handler enrolls after the order is saved.
/// </remarks>
public sealed class EnrollmentWriteGate
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _expiration = TimeSpan.FromSeconds(30);

    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentWriteGate"/> class.
    /// </summary>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="session">The session.</param>
    public EnrollmentWriteGate(IDistributedLock distributedLock, ISession session)
    {
        _distributedLock = distributedLock;
        _session = session;
    }

    /// <summary>
    /// Commits the session inside a lock the caller holds, such as the enrollment lock of a new enrollment, so the next
    /// holder of that lock reads the write.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _session.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Gets the lock key of an enrollment.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <returns>The key.</returns>
    public static string Key(string enrollmentId)
        => "Learning:EnrollmentWrite:" + enrollmentId;

    /// <summary>
    /// Runs a write under the lock of an enrollment.
    /// </summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="work">The write; it adds the notifications to send after the commit.</param>
    /// <param name="busy">The result when the lock is not acquired in time.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    public Task<T> RunAsync<T>(string enrollmentId, Func<IList<Func<Task>>, Task<T>> work, Func<T> busy, CancellationToken cancellationToken = default)
        => RunLockedAsync(Key(enrollmentId), work, busy, cancellationToken);

    /// <summary>
    /// Runs a write to another Learning record, such as an assessment attempt, under the named lock with the same
    /// discipline: the work reads its record again inside the lock, the session is committed before the lock is let go
    /// and the queued notifications run after the commit. A caller that takes a record lock and then writes the
    /// enrollment takes the locks in that order, record first; enrollment writes never take a record lock.
    /// </summary>
    /// <typeparam name="T">The result.</typeparam>
    /// <param name="lockKey">The name of the lock, one per record.</param>
    /// <param name="work">The write; it adds the notifications to send after the commit.</param>
    /// <param name="busy">The result when the lock is not acquired in time.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result of the work, or the busy result.</returns>
    public async Task<T> RunLockedAsync<T>(string lockKey, Func<IList<Func<Task>>, Task<T>> work, Func<T> busy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(lockKey);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(busy);

        var notifications = new List<Func<Task>>();
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(lockKey, _timeout, _expiration);

        if (!locked)
        {
            return busy();
        }

        T result;

        await using (locker)
        {
            result = await work(notifications);
            await _session.SaveChangesAsync(cancellationToken);
        }

        foreach (var notify in notifications)
        {
            await notify();
        }

        return result;
    }
}
