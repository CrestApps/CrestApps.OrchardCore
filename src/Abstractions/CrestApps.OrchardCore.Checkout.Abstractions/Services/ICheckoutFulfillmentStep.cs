using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Quotes and saves how a checkout's order reaches the buyer from code, such as an API or an AI agent, with the same
/// rules as the step's page. The module that owns shipping implements it.
/// </summary>
public interface ICheckoutFulfillmentStep
{
    /// <summary>
    /// Gets the key of the step this saves.
    /// </summary>
    string StepKey { get; }

    /// <summary>
    /// Returns the ways the checkout's order can be shipped to an address, cheapest first. Nothing is saved.
    /// </summary>
    /// <param name="flow">The checkout.</param>
    /// <param name="address">Where to ship.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<CheckoutFulfillmentQuote> QuoteAsync(CheckoutFlow flow, Address address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the places the buyer can collect the order from.
    /// </summary>
    /// <param name="flow">The checkout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<CheckoutPickupOption>> GetPickupOptionsAsync(CheckoutFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the buyer's choice, which puts its cost on the checkout and marks the step done.
    /// </summary>
    /// <param name="flow">The checkout.</param>
    /// <param name="choice">Where and how to ship, or where to collect.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<CheckoutStepSaveResult> SaveAsync(CheckoutFlow flow, CheckoutFulfillmentChoice choice, CancellationToken cancellationToken = default);
}
