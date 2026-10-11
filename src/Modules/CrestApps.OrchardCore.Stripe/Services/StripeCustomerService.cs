using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Implements Stripe Customer API operations.
/// </summary>
public sealed class StripeCustomerService : IStripeCustomerService
{
    private readonly CustomerService _customerService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeCustomerService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to call the Stripe API.</param>
    /// <param name="logger">The logger used to record Stripe customer operation failures.</param>
    public StripeCustomerService(
        StripeClient stripeClient,
        ILogger<StripeCustomerService> logger)
    {
        _customerService = new CustomerService(stripeClient);
        _logger = logger;
    }

    /// <summary>
    /// Creates a Stripe customer with the supplied contact and payment method data.
    /// </summary>
    /// <param name="model">The customer creation request.</param>
    /// <returns>The created customer details, or <see langword="null"/> when Stripe creation fails.</returns>
    public async Task<CreateCustomerResponse> CreateAsync(CreateCustomerRequest model)
    {
        var customerOptions = new CustomerCreateOptions
        {
            PaymentMethod = model.PaymentMethodId,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                DefaultPaymentMethod = model.PaymentMethodId,
            },
            Name = model.Name,
            Email = model.Email,
            Phone = model.Phone,
            Metadata = model.Metadata,
        };

        try
        {
            var customer = await _customerService.CreateAsync(customerOptions, model.ToRequestOptions());

            return new CreateCustomerResponse()
            {
                CustomerId = customer.Id,
                Phone = customer.Phone,
                Email = customer.Email,
                Name = customer.Name,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to create a customer.");

            return null;
        }
    }

    /// <summary>
    /// Updates an existing Stripe customer.
    /// </summary>
    /// <param name="id">The Stripe customer identifier.</param>
    /// <param name="model">The customer values to update.</param>
    /// <returns>The update response, including whether the update succeeded.</returns>
    public async Task<UpdateCustomerResponse> UpdateAsync(string id, UpdateCustomerRequest model)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(model);

        var customerOptions = new CustomerUpdateOptions
        {
            Name = model.Name,
            Phone = model.Phone,
            Email = model.Email,
            Metadata = model.Metadata,
        };

        try
        {
            var customer = await _customerService.UpdateAsync(id, customerOptions);

            return new UpdateCustomerResponse()
            {
                Updated = true,
                CustomerId = customer.Id,
                Phone = customer.Phone,
                Email = customer.Email,
                Name = customer.Name,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to update the Stripe's customer info. CustomerId: {CustomerId}", id);

            return new UpdateCustomerResponse()
            {
                Updated = false,
            };
        }
    }

    /// <summary>
    /// Retrieves a Stripe customer by identifier.
    /// </summary>
    /// <param name="id">The Stripe customer identifier.</param>
    /// <returns>The matching customer, or <see langword="null"/> when Stripe reports that the customer is missing.</returns>
    public async Task<CustomerResponse> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        Customer customer;

        try
        {
            customer = await _customerService.GetAsync(id);
        }
        catch (StripeException ex)
        {
            // Check if the error indicates that the resource does not exist.
            if (ex.StripeError.Type == "invalid_request_error" && ex.StripeError.Code == "resource_missing")
            {
                return null;
            }

            throw;
        }

        return new CustomerResponse()
        {
            Id = customer.Id,
            Name = customer.Name,
            Email = customer.Email,
            Phone = customer.Phone,
        };
    }
}
