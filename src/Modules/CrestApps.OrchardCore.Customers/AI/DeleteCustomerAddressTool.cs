using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers.AI;

/// <summary>
/// Deletes an address from a customer's address book as the address list's Delete button does, with the defaults that
/// named it. Orders keep their own copy of the address. An anonymized or merged customer's book cannot change. It needs
/// <see cref="CustomerPermissions.ManageCustomers"/> and asks for approval.
/// </summary>
internal sealed class DeleteCustomerAddressTool : CustomersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteCustomerAddress";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "customerId": {
          "type": "string",
          "description": "The customer's id, from listCustomers."
        },
        "addressId": {
          "type": "string",
          "description": "The id of the address to delete, from getCustomer."
        }
      },
      "required": ["customerId", "addressId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes one address from a customer's address book. Orders that used it keep their own copy.";

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
            return "Nothing was deleted: the customer was anonymized or merged into another customer, so their address book cannot change.";
        }

        var addressId = ReadString(arguments, "addressId");
        var label = customer.FindAddress(addressId)?.Label;

        if (!customer.RemoveAddress(addressId))
        {
            return $"Nothing was deleted: the customer has no address with the id '{addressId}'. Use getCustomer for their addresses.";
        }

        customer.ModifiedUtc = services.GetRequiredService<IClock>().UtcNow;

        if (await SaveCustomerAsync(services, customer, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        return Json(new
        {
            deleted = true,
            customerId = customer.ItemId,
            customer = customer.GetName(),
            addressId,
            label,
            remaining = customer.Addresses?.Count ?? 0,
            message = "The address was deleted.",
        });
    }
}
