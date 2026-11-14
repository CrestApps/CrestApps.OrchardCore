using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Writes the guest and business customers into a <c>Customers</c> recipe step, in the shape the step reads, a page at a
/// time. Customers with a user account are made from their user on each site, so they are left out, as are erased and
/// merged customers.
/// </summary>
internal sealed class CustomersDeploymentSource : DeploymentSourceBase<CustomersDeploymentStep>
{
    private readonly ICustomerStore _store;

    public CustomersDeploymentSource(ICustomerStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Gets how many customers are read in one query.
    /// </summary>
    internal int PageSize { get; init; } = 500;

    protected override async Task ProcessAsync(CustomersDeploymentStep step, DeploymentPlanResult result)
    {
        var query = new CustomerQuery
        {
            HasAccount = false,
            NotAnonymized = true,
        };
        var customers = new JsonArray();

        for (var skip = 0; ; skip += PageSize)
        {
            var (_, page) = await _store.PageAsync(query, skip, PageSize);

            foreach (var customer in page.Where(customer => customer.Status is not (CustomerStatus.Anonymized or CustomerStatus.Merged) && string.IsNullOrEmpty(customer.UserId)))
            {
                customers.Add(Export(customer));
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = CustomersConstants.Recipes.Customers,
            ["Customers"] = customers,
        });
    }

    internal static JsonObject Export(Customer customer)
        => new()
        {
            [nameof(Customer.ItemId)] = customer.ItemId,
            [nameof(Customer.Kind)] = customer.Kind.ToString(),
            [nameof(Customer.DisplayName)] = customer.DisplayName,
            [nameof(Customer.FirstName)] = customer.FirstName,
            [nameof(Customer.LastName)] = customer.LastName,
            [nameof(Customer.CompanyName)] = customer.CompanyName,
            [nameof(Customer.TaxId)] = customer.TaxId,
            [nameof(Customer.TaxIdVerified)] = customer.TaxIdVerified,
            [nameof(Customer.IsTaxExempt)] = customer.IsTaxExempt,
            [nameof(Customer.TaxExemptionReason)] = customer.TaxExemptionReason,
            [nameof(Customer.Email)] = customer.Email,
            [nameof(Customer.Phone)] = customer.Phone,
            [nameof(Customer.AcceptsMarketing)] = customer.AcceptsMarketing,
            [nameof(Customer.Addresses)] = JsonSerializer.SerializeToNode(customer.Addresses ?? [], JOptions.Default),
            [nameof(Customer.DefaultBillingAddressId)] = customer.DefaultBillingAddressId,
            [nameof(Customer.DefaultShippingAddressId)] = customer.DefaultShippingAddressId,
        };
}
