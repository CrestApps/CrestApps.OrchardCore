using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Saves a checkout's Contact step from code, such as an API or an AI agent, with the same rules as the step's page.
/// The module that owns the step implements it.
/// </summary>
public interface ICheckoutContactStep
{
    /// <summary>
    /// Gets the key of the step this saves.
    /// </summary>
    string StepKey { get; }

    /// <summary>
    /// Saves who the buyer is and, when the store asks for one, their billing address, and marks the step done.
    /// </summary>
    /// <param name="flow">The checkout.</param>
    /// <param name="details">The buyer's details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<CheckoutStepSaveResult> SaveAsync(CheckoutFlow flow, CheckoutContactDetails details, CancellationToken cancellationToken = default);
}
