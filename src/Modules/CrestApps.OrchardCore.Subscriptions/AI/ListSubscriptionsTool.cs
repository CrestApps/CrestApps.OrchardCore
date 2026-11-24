using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Lists subscription agreements with the agreements page's query (<see cref="ISubscriptionManager.PageAsync"/>): an
/// optional status and a search on the title, plus the owner the store also filters by.
/// </summary>
internal sealed class ListSubscriptionsTool : SubscriptionAgreementToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSubscriptions";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "status": {
          "type": "string",
          "enum": ["active", "trialing", "pastDue", "canceled", "expired", "paused", "incomplete"],
          "description": "Only agreements in this status. Leave it out for every status."
        },
        "search": {
          "type": "string",
          "description": "Text the agreement's title contains, such as the plan's name. Owner names are not searched."
        },
        "ownerUserId": {
          "type": "string",
          "description": "Only the agreements of this user, by user id (from searchForUsers)."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many agreements per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists subscription agreements with optional status, title search, owner and paging. Use it to find an agreement's id before reading, canceling or pausing it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!TryReadEnum<SubscriptionStatus>(arguments, "status", out var status))
        {
            return "Nothing was listed: status must be active, trialing, pastDue, canceled, expired, paused or incomplete.";
        }

        var (page, pageSize) = ReadPaging(arguments);

        var result = await services.GetRequiredService<ISubscriptionManager>().PageAsync(page, pageSize, new SubscriptionQuery
        {
            Status = status,
            Search = ReadString(arguments, "search"),
            OwnerId = ReadString(arguments, "ownerUserId"),
        }, cancellationToken);

        var items = new List<object>();

        foreach (var subscription in result.Entries ?? [])
        {
            items.Add(await SummarizeAsync(services, subscription));
        }

        return ListResult(items, result.Count, page, pageSize);
    }
}
