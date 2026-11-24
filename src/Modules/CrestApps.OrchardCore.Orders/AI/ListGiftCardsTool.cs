using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Lists gift cards as Commerce › Gift cards does (<see cref="IGiftCardStore.PageAsync"/>, newest first), with the
/// page's search (the last characters of a code, or the recipient's email) and status filter. Codes are never returned,
/// only their last characters.
/// </summary>
internal sealed class ListGiftCardsTool : GiftCardAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listGiftCards";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "The last four characters of a card's code, such as 7Q2K, or (part of) the recipient's email."
        },
        "status": {
          "type": "string",
          "enum": ["any", "active", "usedUp", "expired", "disabled"],
          "description": "Which cards to list; any by default."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Cards per page, 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists gift cards with the last characters of the code, balance and initial value, currency, status (active, used up, expired, disabled), expiry and recipient. Filters by text and status, with paging.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var status = ReadString(arguments, "status") ?? "any";
        GiftCardStatusFilter filter;

        if (string.Equals(status, "any", StringComparison.OrdinalIgnoreCase))
        {
            filter = GiftCardStatusFilter.All;
        }
        else if (!Enum.TryParse(status, ignoreCase: true, out filter) || filter == GiftCardStatusFilter.All || !Enum.IsDefined(filter))
        {
            return "Nothing was listed: status must be any, active, usedUp, expired or disabled.";
        }

        var (page, pageSize) = ReadPaging(arguments);
        var (count, cards) = await services.GetRequiredService<IGiftCardStore>().PageAsync(new GiftCardQuery
        {
            Search = ReadString(arguments, "search"),
            Status = filter,
        }, (page - 1) * pageSize, pageSize, cancellationToken);

        var items = new List<object>();

        foreach (var card in cards)
        {
            items.Add(await DescribeAsync(services, card));
        }

        return Json(new
        {
            items,
            total = count,
            page,
            pageSize,
            truncated = page * pageSize < count,
        });
    }
}
