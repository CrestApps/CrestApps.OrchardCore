namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Paces campaigns that reserve an agent before each call -- Power, Progressive, and Predictive on the reserved pacing
/// model -- as soon as there is work and a free agent, rather than only once a minute.
/// </summary>
/// <remarks>
/// A tenant singleton over <see cref="IContactCenterDeadlineScheduler"/>. A request is debounced per queue, and requests
/// that arrive while one is pending are merged into it. The run takes the campaign's pacing lock and runs the same cycle
/// the minute dialer pacing task asks for, on a scope of its own, so the profile's pacing applies unchanged. It is an
/// accelerator only: the minute task requests every campaign with waiting work again, and the lock keeps two nodes from
/// pacing the same campaign at once.
/// </remarks>
public interface IDialerPacingScheduler
{
    /// <summary>
    /// Asks for the campaign queue to be paced shortly. Requests that arrive while one is already pending are merged.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    void Request(string queueId);

    /// <summary>
    /// Asks for the campaign queue to be paced again after the lock retry delay, because a run found its pacing lock held
    /// by another. A request already pending is kept; the retry merges into it.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    void RequestRetry(string queueId);
}
