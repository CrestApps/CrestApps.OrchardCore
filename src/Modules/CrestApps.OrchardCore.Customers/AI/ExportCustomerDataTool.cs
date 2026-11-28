using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers.AI;

/// <summary>
/// Answers a data access request with the address of the customer page's Export action, which builds the file of
/// everything the store holds on the customer when it is opened, checks <see cref="CustomerPermissions.ManageCustomers"/>
/// again for whoever opens it, and records the export in the customer's privacy log. The personal data never passes
/// through the conversation: the tool does not run the export. It needs <see cref="CustomerPermissions.ManageCustomers"/>
/// and asks for approval.
/// </summary>
internal sealed class ExportCustomerDataTool : CustomersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "exportCustomerData";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "customerId": {
          "type": "string",
          "description": "The customer's id, from listCustomers."
        }
      },
      "required": ["customerId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the link that downloads everything the store holds on a customer, for a data access request, as a JSON file. Opening the link builds the file, checks the permission again and records the export in the customer's privacy log. The data never passes through the conversation: give the link to the person handling the request.";

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

        var path = GetAdminPath(services, "CustomersExport", new { customerId = customer.ItemId });

        if (path is null)
        {
            return "The export link could not be built, because this chat has no web request to build it from. Open the customer in Commerce › Customers and use Export there.";
        }

        return Json(new
        {
            customerId = customer.ItemId,
            customer = customer.GetName(),
            downloadPath = path,
            fileName = $"customer-{customer.ItemId}.json",
            exported = false,
            message = "Open the link while signed in to download the customer's data. The export is built and recorded in the customer's privacy log, under the name of whoever opens the link, only when it is opened. The file holds personal data: hand it to the requester, do not paste it elsewhere.",
        });
    }
}
