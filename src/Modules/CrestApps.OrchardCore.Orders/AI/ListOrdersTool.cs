using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Lists the store's orders as Commerce › Orders does (<see cref="OrderListQuery"/> and
/// <see cref="IOrderStore.PageAsync"/>): by order number or customer email, status and channel, newest first, plus a
/// customer and the days they were placed, as the Orders API filters them. Each order says whether money is still owed
/// (<see cref="IOrderOperationsService.IsOwedAsync"/>). The result links to the list's CSV export, which takes the search,
/// status and channel only.
/// </summary>
internal sealed class ListOrdersTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listOrders";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "An order number or a customer's email."
        },
        "status": {
          "type": "string",
          "enum": ["placed", "paid", "awaitingPayment", "canceled", "drafts", "any"],
          "description": "placed (the default: paid or awaiting payment), paid, awaitingPayment, canceled, drafts (made by the store and not checked out yet) or any."
        },
        "channel": {
          "type": "string",
          "enum": ["online", "pos", "draft", "agent"],
          "description": "Where the orders were made: online, pos (point of sale), draft (sent by staff) or agent (AI agents). Every channel when left out."
        },
        "customerId": {
          "type": "string",
          "description": "Only the orders of this customer, from the customer tools."
        },
        "placedFrom": {
          "type": "string",
          "description": "Only orders placed on or after this day, yyyy-MM-dd, in the site's time zone."
        },
        "placedTo": {
          "type": "string",
          "description": "Only orders placed on or before this day, yyyy-MM-dd, in the site's time zone."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Orders per page: 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    private static readonly Dictionary<string, OrderStatusFilter> _statuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["placed"] = OrderStatusFilter.Placed,
        ["paid"] = OrderStatusFilter.Paid,
        ["awaitingPayment"] = OrderStatusFilter.AwaitingPayment,
        ["canceled"] = OrderStatusFilter.Canceled,
        ["drafts"] = OrderStatusFilter.Drafts,
        ["any"] = OrderStatusFilter.All,
    };

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists orders with number, date, customer, status, channel, fulfilment status, total, refunded amount and whether money is still owed. Filters by order number or customer email, status (placed, paid, awaiting payment, canceled, drafts, any), channel, customer, and placed-between dates, with paging; it also returns the link that exports the same list as CSV.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!_statuses.TryGetValue(ReadString(arguments, "status") ?? "placed", out var status))
        {
            return "Nothing was listed: status must be placed, paid, awaitingPayment, canceled, drafts or any.";
        }

        var channel = ReadString(arguments, "channel")?.ToLowerInvariant();

        if (channel is not (null or OrderChannels.Online or OrderChannels.PointOfSale or OrderChannels.Draft or OrderChannels.Agent))
        {
            return "Nothing was listed: channel must be online, pos, draft or agent.";
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        var (from, fromError) = await ReadDayAsync(arguments, "placedFrom", localClock, nextDay: false);
        var (to, toError) = await ReadDayAsync(arguments, "placedTo", localClock, nextDay: true);

        if ((fromError ?? toError) is { } dateError)
        {
            return dateError;
        }

        var search = ReadString(arguments, "search");
        var query = OrderListQuery.Build(status, channel, search, userId: null);

        query.CustomerId = ReadString(arguments, "customerId");
        query.CreatedOnOrAfter = from;
        query.CreatedBefore = to;

        var (page, pageSize) = ReadPaging(arguments);
        var (total, orders) = await services.GetRequiredService<IOrderStore>().PageAsync(query, (page - 1) * pageSize, pageSize, cancellationToken);
        var operations = services.GetRequiredService<IOrderOperationsService>();
        var items = new List<object>();

        foreach (var order in orders)
        {
            items.Add(new
            {
                orderId = order.ItemId,
                number = order.OrderNumber,
                placed = await LocalTimeAsync(services, order.CreatedUtc),
                customer = order.CustomerName,
                customerEmail = order.CustomerEmail,
                status = ToCamel(order.Status.ToString()),
                channel = order.Channel,
                fulfillment = order.FulfillmentStatus is { } fulfillment ? ToCamel(fulfillment.ToString()) : null,
                total = order.Total ?? order.Subtotal,
                currency = order.Currency,
                refunded = order.RefundedAmount > 0m ? order.RefundedAmount : (decimal?)null,
                owed = await operations.IsOwedAsync(order, cancellationToken),
            });
        }

        var exportValues = new Dictionary<string, object>();

        if (search is not null)
        {
            exportValues["Options.Search"] = search;
        }

        if (status != OrderStatusFilter.Placed)
        {
            exportValues["Options.Status"] = status.ToString();
        }

        if (channel is not null)
        {
            exportValues["Options.Channel"] = channel;
        }

        return Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = total > page * pageSize,
            exportLink = GetAdminPath(services, "OrdersExport", exportValues),
            exportNote = query.CustomerId is not null || from is not null || to is not null
                ? "The export takes the search, status and channel only, not the customer or the dates."
                : null,
        });
    }

    // A day in the site's time zone, as the UTC time it starts (or, for the last day, the time the next day starts).
    private static async Task<(DateTime? Utc, string Error)> ReadDayAsync(AIFunctionArguments arguments, string name, ILocalClock localClock, bool nextDay)
    {
        if (ReadString(arguments, name) is not { } text)
        {
            return (null, null);
        }

        if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return (null, $"Nothing was listed: write {name} as yyyy-MM-dd.");
        }

        return (await localClock.ToUtcLenientAsync(nextDay ? day.AddDays(1) : day), null);
    }
}
