using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;

namespace CrestApps.OrchardCore.Tests.Customers;

internal sealed class InMemoryCustomerStore : ICustomerStore
{
    public List<Customer> Customers { get; } = [];

    public ValueTask<Customer> FindByIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Customers.FirstOrDefault(customer => customer.ItemId == customerId));

    public Task<Customer> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Customers.FirstOrDefault(customer => customer.UserId == userId));

    public Task<(int Count, IReadOnlyList<Customer> Customers)> PageAsync(CustomerQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var matches = Customers
            .Where(customer => query.Status is null || customer.Status == query.Status)
            .Where(customer => query.Kind is null || customer.Kind == query.Kind)
            .Where(customer => query.HasAccount is null || string.IsNullOrEmpty(customer.UserId) != query.HasAccount)
            .Where(customer => query.LastActivityBefore is null || customer.LastActivityUtc < query.LastActivityBefore)
            .Where(customer => !query.NotAnonymized || customer.Status != CustomerStatus.Anonymized)
            .ToArray();

        return Task.FromResult<(int, IReadOnlyList<Customer>)>((matches.Length, matches.Skip(skip).Take(take).ToArray()));
    }

    public Task<IReadOnlyList<Customer>> ListGuestsByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Customer>>(Customers
            .Where(customer => customer.UserId is null && customer.Status == CustomerStatus.Active && string.Equals(customer.Email?.Trim(), email?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray());

    public ValueTask CreateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        Customers.Add(customer);

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
