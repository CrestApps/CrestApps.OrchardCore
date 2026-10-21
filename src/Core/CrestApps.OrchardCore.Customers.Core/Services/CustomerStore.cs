using CrestApps.OrchardCore.Customers.Core.Indexes;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Customers.Core.Services;

public interface ICustomerStore
{
    ValueTask<Customer> FindByIdAsync(string customerId, CancellationToken cancellationToken = default);

    Task<Customer> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<(int Count, IReadOnlyList<Customer> Customers)> PageAsync(CustomerQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// The active guest customers who bought with <paramref name="email"/>, ignoring case.
    /// </summary>
    Task<IReadOnlyList<Customer>> ListGuestsByEmailAsync(string email, CancellationToken cancellationToken = default);

    ValueTask CreateAsync(Customer customer, CancellationToken cancellationToken = default);

    ValueTask UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
}

public sealed class CustomerQuery
{
    /// <summary>
    /// Matches the start of the name, the company or the email, ignoring case.
    /// </summary>
    public string Search { get; set; }

    public CustomerKind? Kind { get; set; }

    public CustomerStatus? Status { get; set; }

    public bool? HasAccount { get; set; }

    /// <summary>
    /// Gets or sets the date before which the customers were last active.
    /// </summary>
    public DateTime? LastActivityBefore { get; set; }

    /// <summary>
    /// Gets or sets whether to leave out customers whose personal data was already erased.
    /// </summary>
    public bool NotAnonymized { get; set; }
}

public sealed class CustomerStore : DocumentCatalog<Customer, CustomerIndex>, ICustomerStore
{
    // Two editors, or an editor and a checkout, must not overwrite each other's changes.
    protected override bool CheckConcurrency => true;

    private readonly IEnumerable<ICustomerHandler> _handlers;

    public CustomerStore(
        ISession session,
        IEnumerable<ICustomerHandler> handlers)
        : base(session)
    {
        _handlers = handlers;
        CollectionName = CustomersConstants.CollectionName;
    }

    public Task<Customer> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult<Customer>(null);
        }

        return Session.Query<Customer, CustomerIndex>(index => index.UserId == userId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> ListGuestsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (CustomerIndexProvider.Normalize(email) is not { } normalized)
        {
            return [];
        }

        var guests = await Session.Query<Customer, CustomerIndex>(index =>
                index.NormalizedEmail == normalized &&
                !index.HasAccount &&
                index.Status == CustomerStatus.Active,
                collection: CollectionName)
            .ListAsync(cancellationToken);

        return guests.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<Customer> Customers)> PageAsync(CustomerQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        query ??= new CustomerQuery();

        var customers = Session.Query<Customer, CustomerIndex>(collection: CollectionName);

        if (query.Kind is { } kind)
        {
            customers = customers.Where(index => index.Kind == kind);
        }

        if (query.Status is { } status)
        {
            customers = customers.Where(index => index.Status == status);
        }

        if (query.HasAccount is { } hasAccount)
        {
            customers = customers.Where(index => index.HasAccount == hasAccount);
        }

        if (query.LastActivityBefore is { } lastActivityBefore)
        {
            customers = customers.Where(index => index.LastActivityUtc < lastActivityBefore);
        }

        if (query.NotAnonymized)
        {
            customers = customers.Where(index => index.Status != CustomerStatus.Anonymized);
        }

        if (CustomerIndexProvider.Normalize(query.Search) is { } search)
        {
            customers = customers.Where(index =>
                index.NormalizedName.StartsWith(search) ||
                index.NormalizedCompany.StartsWith(search) ||
                index.NormalizedEmail.StartsWith(search));
        }

        var count = await customers.CountAsync(cancellationToken);
        var page = await customers.OrderByDescending(index => index.LastActivityUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }

    protected override async ValueTask CreatedAsync(Customer record, CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers)
        {
            await handler.CreatedAsync(record, cancellationToken);
        }
    }
}
