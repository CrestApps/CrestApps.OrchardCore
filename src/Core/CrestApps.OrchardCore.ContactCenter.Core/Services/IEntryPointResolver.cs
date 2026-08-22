using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Resolves the inbound entry point that serves a dialed number and produces its routing plan.
/// </summary>
public interface IEntryPointResolver
{
    /// <summary>
    /// Finds the enabled entry point that serves the specified dialed number.
    /// </summary>
    /// <param name="dialedNumber">The dialed number (DID) the caller reached.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching entry point, or <see langword="null"/> when none matches.</returns>
    Task<ContactCenterEntryPoint> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the routing plan for the specified dialed number, evaluating business hours.
    /// </summary>
    /// <param name="dialedNumber">The dialed number (DID) the caller reached.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The routing plan, or <see langword="null"/> when no entry point matches.</returns>
    Task<EntryPointRoutingPlan> ResolveAsync(string dialedNumber, CancellationToken cancellationToken = default);
}
