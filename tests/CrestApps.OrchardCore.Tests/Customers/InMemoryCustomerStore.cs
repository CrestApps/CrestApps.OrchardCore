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
        => Task.FromResult<(int, IReadOnlyList<Customer>)>((Customers.Count, Customers));

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
