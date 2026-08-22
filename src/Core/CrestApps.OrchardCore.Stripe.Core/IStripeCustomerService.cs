using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Provides access to the Stripe Customer API for creating, retrieving, and updating customers.
/// </summary>
public interface IStripeCustomerService
{
    /// <summary>
    /// Creates a new Stripe customer.
    /// </summary>
    /// <param name="model">The details of the customer to create.</param>
    /// <returns>The result of the create operation.</returns>
    Task<CreateCustomerResponse> CreateAsync(CreateCustomerRequest model);

    /// <summary>
    /// Retrieves an existing Stripe customer by identifier.
    /// </summary>
    /// <param name="id">The Stripe customer identifier.</param>
    /// <returns>The matching customer.</returns>
    Task<CustomerResponse> GetAsync(string id);

    /// <summary>
    /// Updates an existing Stripe customer.
    /// </summary>
    /// <param name="id">The Stripe customer identifier.</param>
    /// <param name="model">The customer values to update.</param>
    /// <returns>The result of the update operation.</returns>
    Task<UpdateCustomerResponse> UpdateAsync(string id, UpdateCustomerRequest model);
}
