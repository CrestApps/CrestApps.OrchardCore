using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers.AI;

/// <summary>
/// Finds customers as Commerce › Customers does: the same filters (<see cref="CustomerEditorRules.ToQuery"/>) and search,
/// which matches the start of the name, the company or the email, newest activity first. It needs
/// <see cref="CustomerPermissions.ViewCustomers"/>.
/// </summary>
internal sealed class ListCustomersTool : CustomersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listCustomers";

    private static readonly CustomerListFilter[] _filters =
    [
        CustomerListFilter.Active,
        CustomerListFilter.WithAccount,
        CustomerListFilter.Guests,
        CustomerListFilter.Businesses,
        CustomerListFilter.Disabled,
        CustomerListFilter.All,
    ];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "The start of the customer's name, company or email, ignoring case, such as 'dana' or 'dana@example.com'."
        },
        "filter": {
          "type": "string",
          "enum": ["Active", "WithAccount", "Guests", "Businesses", "Disabled", "All"],
          "description": "Which customers: Active (the default), WithAccount (active customers who sign in), Guests (active customers without an account), Businesses (active business customers), Disabled, or All, including anonymized and merged ones."
        },
        "page": {
          "type": "integer",
          "description": "The page, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many customers per page, at most 50 (20 by default)."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Finds customers by the start of their name, company or email, with filters for status, guests or account holders and businesses, and paging. Returns id, name, kind, status, email and last activity.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => CustomerPermissions.ViewCustomers;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var filter = CustomerListFilter.Active;

        if (Has(arguments, "filter") && !TryReadEnum(arguments, "filter", _filters, out filter))
        {
            return "Nothing was listed: filter must be Active, WithAccount, Guests, Businesses, Disabled or All.";
        }

        var page = Math.Max(1, ReadInt(arguments, "page") ?? 1);
        var pageSize = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize);
        var (count, customers) = await services.GetRequiredService<ICustomerStore>().PageAsync(
            CustomerEditorRules.ToQuery(filter, ReadString(arguments, "search")),
            (page - 1) * pageSize,
            pageSize,
            cancellationToken);
        var items = new List<object>();

        foreach (var customer in customers)
        {
            items.Add(new
            {
                id = customer.ItemId,
                name = customer.GetName(),
                kind = customer.Kind.ToString(),
                status = customer.Status.ToString(),
                company = customer.CompanyName,
                email = customer.Email,
                hasAccount = !string.IsNullOrEmpty(customer.UserId),
                lastActivity = await LocalTimeAsync(services, customer.LastActivityUtc),
            });
        }

        return Json(new
        {
            items,
            total = count,
            page,
            pageSize,
            truncated = (page - 1) * pageSize + items.Count < count,
        });
    }
}
