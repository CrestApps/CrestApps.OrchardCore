using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers.AI;

/// <summary>
/// Adds an address to a customer's address book or changes one, with the address editor's rules
/// (<see cref="CustomerAddressBook"/>): the address is checked against its country's rules and names who receives it, a
/// book holds up to 20 addresses, and an address can become the default billing or shipping address. A new address
/// starts with the customer's name, company and phone, as the editor fills them in, and the customer's first address
/// becomes their default, as the editor ticks it. An anonymized or merged customer's book cannot change. It needs
/// <see cref="CustomerPermissions.ManageCustomers"/>.
/// </summary>
internal sealed class SaveCustomerAddressTool : CustomersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveCustomerAddress";

    private static readonly JsonElement _jsonSchema = JsonSerializer.SerializeToElement(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["customerId"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The customer's id, from listCustomers.",
            },
            ["addressId"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The id of the address to change, from getCustomer. Leave it out to add an address.",
            },
            ["label"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "A short label, such as 'Home' or 'Office'. An empty text removes it.",
            },
            ["address"] = AddressArguments.CreateSchema("The address, with its two-letter country code. Required for a new address, which starts with the customer's name and company when they are left out; when changing an address, leave it out to keep it. The address's phone is kept."),
            ["isDefaultBilling"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = "true to bill this address first, false to stop billing it first. Leave it out to keep it as it is (a customer's first address becomes their default).",
            },
            ["isDefaultShipping"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = "true to ship to this address first, false to stop shipping to it first. Leave it out to keep it as it is (a customer's first address becomes their default).",
            },
        },
        ["required"] = new JsonArray("customerId"),
        ["additionalProperties"] = false,
    });

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds an address to a customer's address book or updates one by id, after checking it against the country's rules; can mark it as the default billing or shipping address.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => CustomerPermissions.ManageCustomers;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (customer, error) = await FindCustomerAsync(services, arguments, cancellationToken);

        if (customer is null)
        {
            return error;
        }

        if (!CustomerEditorRules.IsEditable(customer))
        {
            return "Nothing was changed: the customer was anonymized or merged into another customer, so their address book cannot change.";
        }

        var addressId = ReadString(arguments, "addressId");
        var existing = addressId is null ? null : customer.FindAddress(addressId);

        if (addressId is not null && existing is null)
        {
            return $"Nothing was changed: the customer has no address with the id '{addressId}'. Use getCustomer for their addresses, or leave addressId out to add one.";
        }

        var model = new CustomerAddressViewModel
        {
            Label = Has(arguments, "label") ? ReadString(arguments, "label") : existing?.Label,
            IsDefaultBilling = Has(arguments, "isDefaultBilling")
                ? ReadBool(arguments, "isDefaultBilling") == true
                : existing is null ? customer.GetDefaultBillingAddress() is null : string.Equals(customer.DefaultBillingAddressId, existing.AddressId, StringComparison.Ordinal),
            IsDefaultShipping = Has(arguments, "isDefaultShipping")
                ? ReadBool(arguments, "isDefaultShipping") == true
                : existing is null ? customer.GetDefaultShippingAddress() is null : string.Equals(customer.DefaultShippingAddressId, existing.AddressId, StringComparison.Ordinal),
        };

        Address address;

        if (ReadJson(arguments, "address") is { } addressJson)
        {
            if (!AddressArguments.TryRead(addressJson, includeRecipient: true, out var entered, out var addressError))
            {
                return $"Nothing was changed: {addressError}";
            }

            // What the editor would show before the person typed: a new address starts with the customer's own details,
            // a changed one keeps its recipient and phone.
            var start = existing?.Address ?? CustomerAddressBook.NewAddressFor(customer);
            var editor = AddressEditorViewModel.From(entered);

            editor.Name ??= start?.Name;
            editor.Company ??= start?.Company;
            editor.Phone ??= start?.Phone;
            model.Address = editor;

            var modelState = new ModelStateDictionary();

            address = await services.GetRequiredService<CustomerAddressBook>().ValidateAsync(customer, model, existing is null, modelState);

            if (address is null)
            {
                var messages = modelState.Values.SelectMany(entry => entry.Errors).Select(entry => entry.ErrorMessage).Where(message => !string.IsNullOrEmpty(message));

                return $"Nothing was changed: {string.Join(" ", messages)} The address editor on the admin page shows which fields the country needs.";
            }
        }
        else if (existing is null)
        {
            return "Nothing was changed: a new address needs the address.";
        }
        else
        {
            address = existing.Address;
        }

        var entry = CustomerAddressBook.Apply(customer, existing, model, address);

        customer.ModifiedUtc = services.GetRequiredService<IClock>().UtcNow;

        if (await SaveCustomerAsync(services, customer, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        return Json(new
        {
            customerId = customer.ItemId,
            customer = customer.GetName(),
            addressId = entry.AddressId,
            label = entry.Label,
            address = AddressArguments.Describe(entry.Address),
            isDefaultBilling = string.Equals(customer.DefaultBillingAddressId, entry.AddressId, StringComparison.Ordinal),
            isDefaultShipping = string.Equals(customer.DefaultShippingAddressId, entry.AddressId, StringComparison.Ordinal),
            created = existing is null,
            message = existing is null ? "The address was added." : "The address was saved.",
        });
    }
}
