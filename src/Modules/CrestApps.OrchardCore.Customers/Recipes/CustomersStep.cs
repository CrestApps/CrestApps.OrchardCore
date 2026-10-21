using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Customers.Recipes;

/// <summary>
/// Adds or updates guest and business customers from a recipe, such as a sample store's. A customer is matched by its
/// id, then by the email of a guest. Customers with an account are made from their user, so a recipe can't link one.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "Customers", "Customers": [ { "ItemId": "...", "Kind": "Business", "CompanyName": "...", "Email": "...",
///   "Addresses": [ { "AddressId": "...", "Label": "Office", "Address": { "Country": "US", ... } } ],
///   "DefaultBillingAddressId": "..." } ] }
/// </code>
/// </remarks>
internal sealed class CustomersStep : NamedRecipeStepHandler
{
    private static readonly EmailAddressAttribute _emailAddress = new();

    private readonly ICustomerStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public CustomersStep(ICustomerStore store, IClock clock, IStringLocalizer<CustomersStep> stringLocalizer)
        : base(CustomersConstants.Recipes.Customers)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<CustomersStepModel>();
        var now = _clock.UtcNow;

        foreach (var entry in model?.Customers ?? [])
        {
            if (entry is null)
            {
                continue;
            }

            var email = Trim(entry.Email);

            if (email is not null && !_emailAddress.IsValid(email))
            {
                context.Errors.Add(S["The customer email '{0}' is not valid.", email]);

                continue;
            }

            if (Trim(entry.DisplayName) is null && Trim(entry.FirstName) is null && Trim(entry.LastName) is null && Trim(entry.CompanyName) is null && email is null)
            {
                context.Errors.Add(S["A customer needs a name, a company or an email."]);

                continue;
            }

            var customer = await FindAsync(entry.ItemId, email);
            var isNew = customer is null;

            if (isNew)
            {
                customer = new Customer
                {
                    ItemId = !string.IsNullOrEmpty(entry.ItemId) && UniqueId.IsValid(entry.ItemId) ? entry.ItemId : UniqueId.GenerateId(),
                    CreatedUtc = now,
                    LastActivityUtc = now,
                };
            }
            else if (customer.Status is CustomerStatus.Anonymized or CustomerStatus.Merged || !string.IsNullOrEmpty(customer.UserId))
            {
                // An erased or merged customer, or an account's, is not changed by a recipe.
                continue;
            }

            customer.Kind = entry.Kind;
            customer.DisplayName = Trim(entry.DisplayName);
            customer.FirstName = Trim(entry.FirstName);
            customer.LastName = Trim(entry.LastName);
            customer.CompanyName = Trim(entry.CompanyName);
            customer.TaxId = Trim(entry.TaxId);
            customer.IsTaxExempt = entry.IsTaxExempt;
            customer.TaxExemptionReason = Trim(entry.TaxExemptionReason);
            customer.Email = email;
            customer.Phone = Trim(entry.Phone);
            customer.AcceptsMarketing = entry.AcceptsMarketing;
            customer.Addresses = (entry.Addresses ?? [])
                .Where(address => address?.Address is not null)
                .Select(address =>
                {
                    address.AddressId = string.IsNullOrEmpty(address.AddressId) ? UniqueId.GenerateId() : address.AddressId;

                    return address;
                })
                .ToList();
            customer.DefaultBillingAddressId = customer.FindAddress(entry.DefaultBillingAddressId)?.AddressId;
            customer.DefaultShippingAddressId = customer.FindAddress(entry.DefaultShippingAddressId)?.AddressId;
            customer.ModifiedUtc = now;

            if (isNew)
            {
                await _store.CreateAsync(customer);
            }
            else
            {
                await _store.UpdateAsync(customer);
            }
        }
    }

    private async Task<Customer> FindAsync(string itemId, string email)
    {
        if (!string.IsNullOrEmpty(itemId) && await _store.FindByIdAsync(itemId) is { } customer)
        {
            return customer;
        }

        if (email is null)
        {
            return null;
        }

        var guests = await _store.ListGuestsByEmailAsync(email);

        return guests.Count > 0 ? guests[0] : null;
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class CustomersStepModel
    {
        public CustomerStepEntry[] Customers { get; set; }
    }

    private sealed class CustomerStepEntry
    {
        public string ItemId { get; set; }

        public CustomerKind Kind { get; set; }

        public string DisplayName { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string CompanyName { get; set; }

        public string TaxId { get; set; }

        public bool IsTaxExempt { get; set; }

        public string TaxExemptionReason { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public bool AcceptsMarketing { get; set; }

        public List<CustomerAddress> Addresses { get; set; }

        public string DefaultBillingAddressId { get; set; }

        public string DefaultShippingAddressId { get; set; }
    }
}
