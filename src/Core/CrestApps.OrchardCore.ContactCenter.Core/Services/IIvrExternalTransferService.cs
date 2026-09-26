using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Sends a caller from an entry point's phone menu to one of the tenant's approved external destinations.
/// </summary>
public interface IIvrExternalTransferService
{
    /// <summary>
    /// Transfers the caller to the approved external destination the menu named. Only an enabled destination from
    /// the tenant's approved catalog that the dial policy allows, and that is not one of the contact center's own
    /// numbers, is reachable; the menu never supplies a number of its own.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="destinationId">The identifier of the approved destination.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the provider transferred the call; otherwise <see langword="false"/>, and
    /// the caller is still on the line for the menu to put somewhere else.</returns>
    Task<bool> TransferAsync(Interaction interaction, string destinationId, CancellationToken cancellationToken = default);
}
