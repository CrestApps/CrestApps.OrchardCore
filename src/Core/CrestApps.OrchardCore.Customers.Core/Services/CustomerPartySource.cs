using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Finds and names commerce customers (party kind <see cref="PartyKinds.Customer"/>): the id is the customer record's
/// <see cref="CrestApps.Core.Models.CatalogItem.ItemId"/>, not a user id.
/// </summary>
public sealed class CustomerPartySource : IPartySource
{
    private readonly ICustomerStore _customerStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerPartySource"/> class.
    /// </summary>
    /// <param name="customerStore">The customer store.</param>
    public CustomerPartySource(ICustomerStore customerStore)
    {
        _customerStore = customerStore;
    }

    /// <inheritdoc/>
    public string Kind => PartyKinds.Customer;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var (_, customers) = await _customerStore.PageAsync(new CustomerQuery { Search = query?.Trim(), NotAnonymized = true }, 0, Math.Max(count, 1), cancellationToken);

        return [.. customers.Select(customer => new PartyOption(customer.ItemId, NameOf(customer)))];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var id in ids ?? [])
        {
            if (string.IsNullOrEmpty(id) || names.ContainsKey(id))
            {
                continue;
            }

            if (await _customerStore.FindByIdAsync(id, cancellationToken) is { } customer)
            {
                names[id] = NameOf(customer);
            }
        }

        return names;
    }

    private static string NameOf(Customer customer)
    {
        var name = !string.IsNullOrWhiteSpace(customer.DisplayName)
            ? customer.DisplayName
            : !string.IsNullOrWhiteSpace(customer.CompanyName)
                ? customer.CompanyName
                : $"{customer.FirstName} {customer.LastName}".Trim();

        if (string.IsNullOrEmpty(name))
        {
            return customer.Email ?? customer.ItemId;
        }

        return string.IsNullOrEmpty(customer.Email) ? name : $"{name} ({customer.Email})";
    }
}
