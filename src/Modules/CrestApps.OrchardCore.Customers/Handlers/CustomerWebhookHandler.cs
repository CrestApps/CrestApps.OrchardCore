using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Customers.Api;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Tells webhooks when a customer is made: by an administrator, a recipe, a guest's first order, or an account's
/// first visit to its customer record. The customer is sent in the shape the commerce API returns it.
/// </summary>
internal sealed class CustomerWebhookHandler : ICustomerHandler
{
    private readonly ICommerceWebhookPublisher _publisher;

    public CustomerWebhookHandler(ICommerceWebhookPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task CreatedAsync(Customer customer, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync(
            CommerceWebhookEvents.CustomerCreated,
            new { customer = CustomerAccountApiEndpoints.ToApi(customer) },
            $"evt_customer-created_{customer.ItemId}",
            cancellationToken);
}
