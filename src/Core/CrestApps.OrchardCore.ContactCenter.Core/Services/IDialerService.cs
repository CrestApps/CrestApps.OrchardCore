using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Orchestrates outbound dialing: reserves agents through routing, creates communication-history
/// interactions, and asks the Voice Contact Center Call Router to place each call.
/// </summary>
public interface IDialerService
{
    /// <summary>
    /// Runs one pacing cycle for a campaign queue, placing calls for as many reserved activities as pacing allows.
    /// </summary>
    /// <param name="profile">The dialer profile whose settings govern the cycle.</param>
    /// <param name="queueId">The campaign queue whose waiting inventory is dialed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of outbound attempts started.</returns>
    Task<int> RunCycleAsync(DialerProfile profile, string queueId, CancellationToken cancellationToken = default);
}
