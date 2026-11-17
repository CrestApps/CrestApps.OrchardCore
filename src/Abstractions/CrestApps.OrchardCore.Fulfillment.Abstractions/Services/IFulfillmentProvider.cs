using CrestApps.OrchardCore.Fulfillment.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Fulfillment.Services;

/// <summary>
/// An outside company that makes and ships a store's products, such as a print-on-demand service. A provider module
/// implements this contract and registers it as a scoped service; the Fulfillment Partners engine lists every
/// registered provider, assigns each order line to at most one of them, and sends each one only its lines.
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
    /// Says which of an order's lines this partner makes, from the store's own data only (such as the links the
    /// provider keeps on products). It never calls the partner. The engine gives a line claimed by two partners to
    /// neither and asks a person, and caps a claim to the quantity left to ship.
    /// </summary>
    /// <param name="order">The order, with every physical line still to ship.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The lines this partner makes; empty when it makes none.</returns>
    Task<IReadOnlyList<FulfillmentLineClaim>> ClaimLinesAsync(FulfillmentOrder order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the lines the engine assigned to this partner. The engine has recorded them in a pending shipment before
    /// this call. The call is cancelled after <see cref="FulfillmentConstants.SubmitTimeout"/>; the engine then treats
    /// the outcome as unclear.
    /// </summary>
    /// <param name="order">The order, holding only the lines assigned to this partner.</param>
    /// <param name="cancellationToken">The token cancelled at the timeout.</param>
    /// <returns>
    /// How sending ended. Return <see cref="FulfillmentSubmitStatus.Refused"/> or
    /// <see cref="FulfillmentSubmitStatus.RetryLater"/> only when the partner certainly did not create the order. A
    /// refusal or an outage is a result, not an exception.
    /// </returns>
    Task<FulfillmentSubmitResult> SubmitAsync(FulfillmentOrder order, CancellationToken cancellationToken = default);
}
