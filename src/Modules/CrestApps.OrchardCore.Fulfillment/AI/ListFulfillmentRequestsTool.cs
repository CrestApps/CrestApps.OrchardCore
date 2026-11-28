using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Fulfillment.AI;

/// <summary>
/// Lists the orders sent, or waiting to be sent, to fulfillment partners. Given an order, it shows what the order's
/// Fulfillment partners card shows: each partner's request, the connected partners the order was not sent to yet, and
/// whether the card's Send button is offered. Otherwise it is the Commerce › Fulfillment requests page, filtered by
/// status, most recently changed first. Shipments and tracking are <c>getOrderFulfillment</c>'s, of the Shipping module.
/// </summary>
internal sealed class ListFulfillmentRequestsTool : FulfillmentAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "listFulfillmentRequests";

    private const int DefaultPageSize = 20;

    private const int MaxPageSize = 50;

    private static readonly JsonElement _jsonSchema = Schema("""
    {
      "type": "object",
      "properties": {
        "orderId": {
          "type": "string",
          "description": "The id of one order: lists its requests and says whether it can be sent to the partners now."
        },
        "orderNumber": {
          "type": "string",
          "description": "The number of one order, instead of its id."
        },
        "status": {
          "type": "string",
          "enum": ["pending", "sending", "sent", "nothingToSend", "needsAttention"],
          "description": "Lists only the requests with this status across all orders, for example needsAttention for the ones a person must handle. Not used with an order."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1. Not used with an order."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many requests a page holds: 20 by default, at most 50. Not used with an order."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists orders sent, or waiting to be sent, to fulfillment partners: each partner's status and why, its message and order id, the lines and times; for one order, also whether it can be sent now. Tracking is in getOrderFulfillment.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<FulfillmentRequestStore>();

        if (HasOrder(arguments))
        {
            var (order, error) = await FindOrderAsync(services, arguments, cancellationToken);

            if (order is null)
            {
                return error;
            }

            return await DescribeOrderAsync(services, store, order, cancellationToken);
        }

        FulfillmentRequestStatus? status = null;

        if (ReadString(arguments, "status") is { } statusText)
        {
            if (!TryParseStatus(statusText, out var parsed))
            {
                return $"Nothing was listed: '{statusText}' is not a status. Use pending, sending, sent, nothingToSend or needsAttention.";
            }

            status = parsed;
        }

        if (!TryReadInt(arguments, "page", out var pageValue) || pageValue < 1)
        {
            return "Nothing was listed: page must be a whole number from 1.";
        }

        if (!TryReadInt(arguments, "pageSize", out var pageSizeValue) || pageSizeValue < 1)
        {
            return "Nothing was listed: pageSize must be a whole number from 1 to 50.";
        }

        var page = pageValue ?? 1;
        var pageSize = Math.Min(pageSizeValue ?? DefaultPageSize, MaxPageSize);
        var skip = (page - 1) * pageSize;
        var (items, total) = await store.ListPageAsync(status, search: null, skip, pageSize, cancellationToken);

        return Json(new
        {
            items = await DescribeAsync(services, items),
            total,
            page,
            pageSize,
            truncated = skip + items.Count < total,
            note = UntrustedNote,
        });
    }

    private static async Task<string> DescribeOrderAsync(IServiceProvider services, FulfillmentRequestStore store, Order order, CancellationToken cancellationToken)
    {
        var requests = await store.ListByOrderAsync(order.ItemId, cancellationToken);
        var paid = order.Status == OrderStatus.Paid;
        var notSentYet = new List<string>();

        // As the order card: the partners still to send to are listed only while the order may be sent.
        if (paid)
        {
            foreach (var provider in services.GetServices<IFulfillmentProvider>())
            {
                if (!requests.Any(request => request.ProviderName == provider.Name) && await provider.IsConnectedAsync())
                {
                    notSentYet.Add(provider.DisplayName.Value);
                }
            }
        }

        var canRetry = requests.Any(request => request.Status is FulfillmentRequestStatus.Pending or FulfillmentRequestStatus.NeedsAttention or FulfillmentRequestStatus.NothingToSend);

        return Json(new
        {
            order = new
            {
                id = order.ItemId,
                number = order.OrderNumber,
                status = ToCamel(order.Status.ToString()),
                paid,
            },
            requests = await DescribeAsync(services, requests),
            notSentYet,
            canSend = paid && (notSentYet.Count > 0 || canRetry),
            sendNeedsPartnerCheck = requests.Any(FulfillmentReasons.MayAlreadyBeMade),
            note = UntrustedNote,
        });
    }
}
