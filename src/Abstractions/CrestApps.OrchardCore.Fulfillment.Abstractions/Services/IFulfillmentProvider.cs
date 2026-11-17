using CrestApps.OrchardCore.Fulfillment.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Fulfillment.Services;

/// <summary>
/// An outside company that makes and ships a store's products, such as a print-on-demand service. A provider module
/// implements this contract and registers it as a scoped service; the Fulfillment Partners module lists every
/// registered provider.
/// </summary>
public interface IFulfillmentProvider
{
    /// <summary>
    /// Gets the provider's technical name, as stored records refer to it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown to people.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Tells whether the store has saved credentials and chosen an account for this partner. It reads saved settings
    /// only and never calls the partner.
    /// </summary>
    /// <returns><see langword="true"/> when the connection is set up.</returns>
    Task<bool> IsConnectedAsync();

    /// <summary>
    /// Calls the partner with the saved credentials and checks that the chosen account can be used.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How the check went. A refusal or an outage is a failed result, not an exception.</returns>
    Task<FulfillmentConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the lines of a paid order that this partner makes. The partner works out which lines are its own from
    /// the links the store saved on its products.
    /// </summary>
    /// <param name="order">The order, as the engine describes it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How sending ended. A refusal or an outage is a result, not an exception.</returns>
    Task<FulfillmentSubmitResult> SubmitAsync(FulfillmentOrder order, CancellationToken cancellationToken = default);
}
