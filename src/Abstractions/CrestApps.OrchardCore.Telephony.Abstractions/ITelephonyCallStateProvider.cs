using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Exposes provider-truth call-state lookups for telephony providers that can query the current server
/// state of a live call.
/// </summary>
public interface ITelephonyCallStateProvider
{
    /// <summary>
    /// Queries the provider for the current state of the specified call.
    /// </summary>
    /// <param name="callId">The provider call identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The lookup result describing whether the call was found and, when available, its current state.</returns>
    Task<TelephonyCallLookupResult> GetCallStateAsync(string callId, CancellationToken cancellationToken = default);
}
