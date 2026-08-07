using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Encapsulates the pacing and reservation behavior for a single outbound dialing mode. Each safe,
/// automated dialing mode is implemented as a dedicated strategy so that unsupported modes can be
/// withheld entirely rather than falling through to an unsafe default.
/// </summary>
public interface IDialerStrategy
{
    /// <summary>
    /// Gets the dialing mode this strategy implements.
    /// </summary>
    DialerMode Mode { get; }

    /// <summary>
    /// Runs one pacing cycle for the dialer profile, reserving agents and placing calls within the
    /// mode's pacing limits.
    /// </summary>
    /// <param name="profile">The dialer profile to run.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of outbound attempts started in the cycle.</returns>
    Task<int> RunCycleAsync(DialerProfile profile, CancellationToken cancellationToken = default);
}
