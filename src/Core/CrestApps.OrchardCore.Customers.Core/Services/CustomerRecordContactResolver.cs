using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Addresses a customer with an account by the name their customer record gives, and the account's email. A guest
/// is addressed from the contact kept at purchase time, as before.
/// </summary>
public sealed class CustomerRecordContactResolver : ICustomerContactResolver
{
    private readonly ICustomerStore _store;
    private readonly DefaultCustomerContactResolver _accounts;

    public CustomerRecordContactResolver(ICustomerStore store, DefaultCustomerContactResolver accounts)
    {
        _store = store;
        _accounts = accounts;
    }

    public async Task<ICustomerContact> ResolveAsync(CustomerOwner owner, ICustomerContact guestContact, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.ResolveAsync(owner, guestContact, cancellationToken);

        if (owner?.Kind != CustomerOwnerKind.Authenticated || await _store.FindByUserIdAsync(owner.Id, cancellationToken) is not { } customer)
        {
            return account;
        }

        return new CustomerContact
        {
            DisplayName = customer.GetName() ?? account?.DisplayName,
            Email = account?.Email ?? customer.Email,
        };
    }
}
