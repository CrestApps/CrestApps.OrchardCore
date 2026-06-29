using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Orchestrates outbound dialing: reserves agents through routing, creates communication-history
/// interactions, and asks a dialer-agnostic provider to place each call.
/// </summary>
public interface IDialerService
{
    /// <summary>
    /// Runs one pacing cycle for the dialer profile, placing calls for as many reserved activities as pacing allows.
    /// </summary>
    /// <param name="profile">The dialer profile to run.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of outbound attempts started.</returns>
    Task<int> RunCycleAsync(DialerProfile profile, CancellationToken cancellationToken = default);
}
