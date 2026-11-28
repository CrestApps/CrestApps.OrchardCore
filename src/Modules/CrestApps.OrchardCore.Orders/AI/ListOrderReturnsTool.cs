using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Lists the buyers' return requests as Commerce › Returns does (<see cref="IOrderReturnStore.PageAsync"/>, newest
/// first): open ones by default, or one status, or all, with the page's search on the return or order number.
/// </summary>
internal sealed class ListOrderReturnsTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listOrderReturns";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "The start of a return number (such as RMA-2611) or an order number."
        },
        "status": {
          "type": "string",
          "enum": ["open", "requested", "approved", "received", "completed", "declined", "withdrawn", "all"],
          "description": "Which returns to list: open (requested, approved or received; the default), one status, or all."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Returns per page, 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists buyers' return requests with number, order, customer, status, whether a refund or an exchange is wanted, and quantity. Filters by text and status (open by default), with paging.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var query = new OrderReturnQuery { Search = ReadString(arguments, "search") };
        var status = ReadString(arguments, "status") ?? "open";

        if (string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            query.All = true;
        }
        else if (ReturnToolSupport.Statuses.TryGetValue(status, out var chosen))
        {
            query.Status = chosen;
        }
        else if (!string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was listed: status must be open, requested, approved, received, completed, declined, withdrawn or all.";
        }

        var (page, pageSize) = ReadPaging(arguments);
        var (count, returns) = await services.GetRequiredService<IOrderReturnStore>().PageAsync(query, (page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var entry in returns)
        {
            items.Add(new
            {
                id = entry.ItemId,
                number = entry.Number,
                orderId = entry.OrderId,
                orderNumber = entry.OrderNumber,
                customerName = entry.CustomerName,
                status = ReturnToolSupport.NameOf(entry.Status),
                wants = ToCamel(entry.Resolution.ToString()),
                quantity = entry.Quantity,
                refundedAmount = entry.RefundedAmount,
                currency = entry.Currency,
                requestedAt = await LocalTimeAsync(services, entry.CreatedUtc),
            });
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
