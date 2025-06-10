using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripeCustomerService : IStripeCustomerService
{
    private readonly CustomerService _customerService;
    private readonly ILogger _logger;

    public StripeCustomerService(
        StripeClient stripeClient,
        ILogger<StripeCustomerService> logger)
    {
        _customerService = new CustomerService(stripeClient);
        _logger = logger;
    }

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
            var customer = await _customerService.CreateAsync(customerOptions);

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
