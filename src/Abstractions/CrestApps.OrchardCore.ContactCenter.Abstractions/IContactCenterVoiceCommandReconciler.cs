using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Defines optional provider support for reconciling an outbound voice command by its stable command identifier.
/// </summary>
public interface IContactCenterVoiceCommandReconciler
{
    /// <summary>
    /// Queries the provider for the outcome of a previously sent command without issuing the command again.
    /// </summary>
    /// <param name="commandId">The stable command identifier supplied with the original request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider's current knowledge of the command outcome.</returns>
    Task<ContactCenterVoiceCommandReconciliationResult> ReconcileCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default);
}
