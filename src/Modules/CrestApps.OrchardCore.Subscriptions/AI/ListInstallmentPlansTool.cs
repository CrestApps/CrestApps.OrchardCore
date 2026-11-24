using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Lists installment plans with the Installment Plans page's query (<see cref="IInstallmentPlanStore.PageAsync"/>):
/// an optional status and search, plus the customer the store also filters by.
/// </summary>
internal sealed class ListInstallmentPlansTool : InstallmentPlanToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listInstallmentPlans";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "status": {
          "type": "string",
          "enum": ["draft", "active", "pastDue", "completed", "canceled"],
          "description": "Only plans in this status. draft means waiting for its down payment. Leave it out for every status."
        },
        "search": {
          "type": "string",
          "description": "Text to search for, as the page's search box."
        },
        "customerUserId": {
          "type": "string",
          "description": "Only the plans of this customer, by user id (from searchForUsers)."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many plans per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists installment plans with optional status, search and paging, showing customer, total, paid, outstanding and next payment. Use it to find past-due plans or a plan's id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!TryReadEnum<InstallmentPlanStatus>(arguments, "status", out var status))
        {
            return "Nothing was listed: status must be draft, active, pastDue, completed or canceled.";
        }

        var (page, pageSize) = ReadPaging(arguments);

        var result = await services.GetRequiredService<IInstallmentPlanStore>().PageAsync(page, pageSize, new InstallmentPlanQuery
        {
            Status = status,
            Search = ReadString(arguments, "search"),
            OwnerId = ReadString(arguments, "customerUserId"),
        }, cancellationToken);

        var items = new List<object>();

        foreach (var plan in result.Entries ?? [])
        {
            items.Add(await SummarizeAsync(services, plan));
        }

        return ListResult(items, result.Count, page, pageSize);
    }
}
