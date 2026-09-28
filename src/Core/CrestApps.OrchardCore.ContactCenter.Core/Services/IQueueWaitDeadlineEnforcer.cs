namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Holds a waiting caller's next wait deadline — their next overflow hop, their queue's maximum wait, or a held
/// direct-to-agent call's ring window — and acts on it when it falls due, instead of leaving it to the next
/// queue-treatment sweep.
/// </summary>
public interface IQueueWaitDeadlineEnforcer
{
    /// <summary>
    /// Arms the caller's next wait deadline, or drops it when the caller is no longer waiting or has none.
    /// </summary>
    /// <param name="queueItemId">The queue item.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ArmAsync(string queueItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies whatever wait deadline the caller has reached: hands them on to their next overflow queue, applies the
    /// queue's maximum-wait action, or times a held direct-to-agent call out to voicemail.
    /// </summary>
    /// <param name="queueItemId">The queue item.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>When to look again, or <see langword="null"/> when there is nothing more to wait for here.</returns>
    Task<DateTime?> EnforceAsync(string queueItemId, CancellationToken cancellationToken = default);
}
