using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Customers.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Customers.Core.Indexes;

public sealed class CustomerIndex : CatalogItemIndex
{
    public string UserId { get; set; }

    /// <summary>
    /// The email in upper case, for an exact lookup that ignores case.
    /// </summary>
    public string NormalizedEmail { get; set; }

    /// <summary>
    /// The name, company and email in upper case, for searching the list by their start.
    /// </summary>
    public string NormalizedName { get; set; }

    public string NormalizedCompany { get; set; }

    public CustomerKind Kind { get; set; }

    public CustomerStatus Status { get; set; }

    public bool HasAccount { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime LastActivityUtc { get; set; }
}

public sealed class CustomerIndexProvider : IndexProvider<Customer>
{
    public CustomerIndexProvider()
    {
        CollectionName = CustomersConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Customer> context)
    {
        context.For<CustomerIndex>()
            .Map(customer => new CustomerIndex
            {
                ItemId = customer.ItemId,
                UserId = customer.UserId,
                NormalizedEmail = Normalize(customer.Email),
                NormalizedName = Normalize(customer.GetName()),
                NormalizedCompany = Normalize(customer.CompanyName),
                Kind = customer.Kind,
                Status = customer.Status,
                HasAccount = !string.IsNullOrEmpty(customer.UserId),
                CreatedUtc = customer.CreatedUtc,
                LastActivityUtc = customer.LastActivityUtc,
            });
    }

    internal static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant();

        return normalized.Length > 255 ? normalized[..255] : normalized;
    }
}
