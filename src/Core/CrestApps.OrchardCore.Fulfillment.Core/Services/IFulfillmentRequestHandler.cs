using CrestApps.OrchardCore.Fulfillment.Core.Models;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// Reacts to changes of fulfillment requests, for example to tell people when one needs them. Register implementations
/// as scoped services.
/// </summary>
public interface IFulfillmentRequestHandler
{
    /// <summary>
    /// Called once each time a request becomes <see cref="FulfillmentRequestStatus.NeedsAttention"/>, after it is saved.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task.</returns>
    Task NeedsAttentionAsync(FulfillmentRequest request, CancellationToken cancellationToken = default);
}
