using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Runs a write to an enrollment or its progress under one distributed lock per enrollment. The work reads the
/// enrollment again inside the lock, the session is committed before the lock is let go, so the next writer reads the
/// change, and the observers it queued run after the commit, only for what this request changed.
/// </summary>
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
    public async Task<T> RunAsync<T>(string enrollmentId, Func<IList<Func<Task>>, Task<T>> work, Func<T> busy, CancellationToken cancellationToken = default)
    {
        var notifications = new List<Func<Task>>();
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(Key(enrollmentId), _timeout, _expiration);

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
