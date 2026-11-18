using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Customers.AI;

/// <summary>
/// Answers an erasure request as the customer page's Erase action does (<see cref="ICustomerPrivacyService.EraseAsync"/>):
/// every module that keeps personal data about the customer erases it through its personal-data contributor, their
/// orders keep their totals without the personal details, the customer record is anonymized and unlinked from their
/// account, and the erasure is recorded in the privacy log under the caller's name. It cannot be undone. It needs
/// <see cref="CustomerPermissions.ManageCustomers"/> and asks for approval.
/// </summary>
internal sealed class EraseCustomerDataTool : CustomersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "eraseCustomerData";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "customerId": {
          "type": "string",
          "description": "The id of the customer whose personal data to erase, from listCustomers."
        }
      },
      "required": ["customerId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Erases a customer's personal data and the personal details on their orders for an erasure request. Orders stay without them; this cannot be undone.";

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

        if (customer.Status == CustomerStatus.Anonymized)
        {
            return Json(new
            {
                customerId = customer.ItemId,
                erased = false,
                message = "Nothing was done: the customer's personal data was already erased.",
            });
        }

        var name = customer.GetName();

        try
        {
            await services.GetRequiredService<ICustomerPrivacyService>().EraseAsync(customer, GetActor(caller), cancellationToken);

            if (services.GetService<ISession>() is { } session)
            {
                await session.SaveChangesAsync(cancellationToken);
            }
        }
        catch (ConcurrencyException)
        {
            return "The erasure could not be saved: someone else changed this customer at the same time. Check the customer with getCustomer and try again.";
        }

        return Json(new
        {
            customerId = customer.ItemId,
            erased = true,
            status = customer.Status.ToString(),
            message = $"The personal data of {name} was erased. Their orders stay, without it. The erasure is recorded in the customer's privacy log.",
        });
    }
}
