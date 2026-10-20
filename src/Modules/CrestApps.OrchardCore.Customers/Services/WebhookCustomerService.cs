using CrestApps.OrchardCore.Commerce.Webhooks;
using CrestApps.OrchardCore.Customers.Api;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// The customer service, telling webhooks when it makes a customer: a guest's first order, or an account's first visit
/// to its customer record. The customer is sent in the shape the commerce API returns it.
/// </summary>
internal sealed class WebhookCustomerService : ICustomerService
{
    private readonly DefaultCustomerService _inner;
    private readonly IServiceProvider _serviceProvider;

    public WebhookCustomerService(DefaultCustomerService inner, IServiceProvider serviceProvider)
    {
        _inner = inner;
        _serviceProvider = serviceProvider;
    }

    public async Task<Customer> GetOrCreateForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var existing = await _inner.FindForUserAsync(userId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var customer = await _inner.GetOrCreateForUserAsync(userId, cancellationToken);

        await PublishCreatedAsync(customer, cancellationToken);

        return customer;
    }

    public Task<Customer> FindForUserAsync(string userId, CancellationToken cancellationToken = default)
        => _inner.FindForUserAsync(userId, cancellationToken);

    public async Task<Customer> CreateGuestAsync(string name, string email, string phone, CancellationToken cancellationToken = default)
    {
        var customer = await _inner.CreateGuestAsync(name, email, phone, cancellationToken);

        await PublishCreatedAsync(customer, cancellationToken);

        return customer;
    }

    public Task TouchAsync(Customer customer, CancellationToken cancellationToken = default)
        => _inner.TouchAsync(customer, cancellationToken);

    internal static Task PublishCreatedAsync(IServiceProvider serviceProvider, Customer customer, CancellationToken cancellationToken)
        => customer is not null && serviceProvider.GetService<ICommerceWebhookPublisher>() is { } publisher
            ? publisher.PublishAsync(CommerceWebhookEvents.CustomerCreated, new { customer = CustomerAccountApiEndpoints.ToApi(customer) }, cancellationToken)
            : Task.CompletedTask;

    private Task PublishCreatedAsync(Customer customer, CancellationToken cancellationToken)
        => PublishCreatedAsync(_serviceProvider, customer, cancellationToken);
}
