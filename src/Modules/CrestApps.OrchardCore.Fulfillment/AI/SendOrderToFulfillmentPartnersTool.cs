using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Fulfillment.AI;

/// <summary>
/// Puts a paid order in line for its fulfillment partners, as the order's Send button would send it, or one request,
/// as the Fulfillment requests page's Send again would. The background task sends what is queued within about a minute,
/// so a chat never waits on a partner. It refuses an order that is not paid, leaves requests sent or being sent alone,
/// works under the order's lock, and queues a request the partner may already have (the partner did not answer clearly,
/// or sending was interrupted) only when the user says they checked with the partner, as the page asks them to confirm.
/// </summary>
internal sealed class SendOrderToFulfillmentPartnersTool : FulfillmentAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "sendOrderToFulfillmentPartners";

    private const string CheckHint = "Check the outcome in a minute or two with listFulfillmentRequests.";

    private const string BusyMessage = "Nothing was queued: someone else, or the background task, is sending this order right now. Read it again with listFulfillmentRequests in a moment.";

    private static readonly JsonElement _jsonSchema = Schema("""
    {
      "type": "object",
      "properties": {
        "orderId": {
          "type": "string",
          "description": "The id of the order to send to its partners."
        },
        "orderNumber": {
          "type": "string",
          "description": "The number of the order, instead of its id."
        },
        "requestId": {
          "type": "string",
          "description": "The id of one fulfillment request to send again, from listFulfillmentRequests, instead of an order."
        },
        "checkedWithPartner": {
          "type": "boolean",
          "description": "True only when the user has checked the partner's dashboard and the partner does not have the order. Needed when listFulfillmentRequests says mayAlreadyBeMade; otherwise leave it out."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Queues a paid order for the fulfillment partners that don't have it yet, and again where a request is waiting or needs attention; or queues one request again by its id. It is sent within about a minute and the partner then makes and ships the items, which cannot be undone here.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var requestId = ReadString(arguments, "requestId");
        var hasOrder = HasOrder(arguments);

        if (requestId is not null && hasOrder)
        {
            return "Nothing was queued: give either an order or a request id, not both.";
        }

        if (requestId is null && !hasOrder)
        {
            return "Nothing was queued: give the order's id or number, or a request id. Use listFulfillmentRequests to find them.";
        }

        var checkedWithPartner = ReadBool(arguments, "checkedWithPartner") == true;

        return requestId is null
            ? await QueueOrderAsync(arguments, services, checkedWithPartner, cancellationToken)
            : await QueueRequestAsync(services, requestId, checkedWithPartner, cancellationToken);
    }

    private static async Task<string> QueueOrderAsync(AIFunctionArguments arguments, IServiceProvider services, bool checkedWithPartner, CancellationToken cancellationToken)
    {
        var (order, error) = await FindOrderAsync(services, arguments, cancellationToken);

        if (order is null)
        {
            return error;
        }

        if (NotPaid(order) is { } refusal)
        {
            return refusal;
        }

        var requests = await services.GetRequiredService<FulfillmentRequestStore>().ListByOrderAsync(order.ItemId, cancellationToken);
        var providers = services.GetServices<IFulfillmentProvider>();
        var notSentYet = new List<string>();

        foreach (var provider in providers)
        {
            if (!requests.Any(request => request.ProviderName == provider.Name) && await provider.IsConnectedAsync())
            {
                notSentYet.Add(provider.DisplayName.Value);
            }
        }

        var again = requests.Where(request => request.Status is not (FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending)).ToList();

        // The order card offers its Send button only when there is something to send.
        if (notSentYet.Count == 0 && again.Count == 0)
        {
            if (requests.Count == 0)
            {
                return "Nothing was queued: no fulfillment partner is connected. A person connects one under Settings › Commerce › Fulfillment partners.";
            }

            return Json(new
            {
                queued = false,
                orderId = order.ItemId,
                orderNumber = order.OrderNumber,
                summary = $"Nothing was queued: every partner already has order {order.OrderNumber} or is being sent it.",
                requests = await DescribeAsync(services, requests),
                note = UntrustedNote,
            });
        }

        // An early answer, as the page asks before it sends. The sender decides again under the order's lock, on the
        // stored state, so a request that became unclear meanwhile is still left alone.
        if (!checkedWithPartner && again.Where(FulfillmentReasons.MayAlreadyBeMade).ToList() is { Count: > 0 } unsure)
        {
            return $"Nothing was queued: {MayAlreadyBeMade(providers, order.OrderNumber, unsure)}";
        }

        var queued = await services.GetRequiredService<FulfillmentSender>().TryQueueAsync(order, checkedWithPartner, cancellationToken);

        if (queued is null)
        {
            return BusyMessage;
        }

        return await DescribeQueuedAsync(services, order, queued);
    }

    private static async Task<string> QueueRequestAsync(IServiceProvider services, string requestId, bool checkedWithPartner, CancellationToken cancellationToken)
    {
        if (await services.GetRequiredService<FulfillmentRequestStore>().FindByIdAsync(requestId, cancellationToken) is not { } request)
        {
            return $"Nothing was queued: no fulfillment request has the id '{requestId}'. Use listFulfillmentRequests to find it.";
        }

        var order = await services.GetRequiredService<IOrderStore>().FindByIdAsync(request.OrderId, cancellationToken);

        if (order is null)
        {
            return $"Nothing was queued: order {request.OrderNumber} no longer exists.";
        }

        // Stricter than the Fulfillment requests page, whose Send again does not check the order: only a paid order goes
        // to a partner, as on the order card.
        if (NotPaid(order) is { } refusal)
        {
            return refusal;
        }

        var providers = services.GetServices<IFulfillmentProvider>();

        if (request.Status is FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending)
        {
            return Json(new
            {
                queued = false,
                orderId = order.ItemId,
                orderNumber = order.OrderNumber,
                summary = $"Nothing was queued: {PartnerName(providers, request.ProviderName)} already has order {order.OrderNumber} or is being sent it.",
                requests = new[] { await DescribeAsync(services, request) },
                note = UntrustedNote,
            });
        }

        if (!checkedWithPartner && FulfillmentReasons.MayAlreadyBeMade(request))
        {
            return $"Nothing was queued: {MayAlreadyBeMade(providers, order.OrderNumber, [request])}";
        }

        var result = await services.GetRequiredService<FulfillmentSender>().TryQueueAsync(request, checkedWithPartner, cancellationToken);

        if (result.TakenByAnotherWorker)
        {
            return BusyMessage;
        }

        return await DescribeQueuedAsync(services, order, [result.Request]);
    }

    private static async Task<string> DescribeQueuedAsync(IServiceProvider services, Order order, IReadOnlyList<FulfillmentRequest> requests)
    {
        var providers = services.GetServices<IFulfillmentProvider>();
        var text = services.GetRequiredService<FulfillmentRequestText>();
        var queued = requests.Where(request => request.Status == FulfillmentRequestStatus.Pending).ToList();
        var skipped = requests.Where(request => request.Status != FulfillmentRequestStatus.Pending).ToList();

        // Left alone by the sender under the order's lock: the partner may have the order since it was read.
        var unsure = skipped.Where(FulfillmentReasons.MayAlreadyBeMade).ToList();
        var names = string.Join(", ", queued.Select(request => PartnerName(providers, request.ProviderName)));
        var summary = queued.Count > 0
            ? $"Order {order.OrderNumber} is queued for {names}. The background task sends it within about a minute; it has not been sent yet. {CheckHint}"
            : unsure.Count > 0
                ? $"Nothing was queued for order {order.OrderNumber}."
                : $"Nothing was queued: every partner already has order {order.OrderNumber} or is being sent it.";

        if (unsure.Count > 0)
        {
            summary += $" Not queued: {MayAlreadyBeMade(providers, order.OrderNumber, unsure)}";
        }

        return Json(new
        {
            queued = queued.Count > 0,
            orderId = order.ItemId,
            orderNumber = order.OrderNumber,
            summary,
            requests = await DescribeAsync(services, queued),
            skipped = skipped.Select(request => new
            {
                id = request.ItemId,
                partner = PartnerName(providers, request.ProviderName),
                status = ToCamel(request.Status.ToString()),
                reason = ToCamel(request.Reason),
                reasonText = text.Reason(request).Value,
                mayAlreadyBeMade = FulfillmentReasons.MayAlreadyBeMade(request),
            }),
            note = UntrustedNote,
        });
    }

    private static string NotPaid(Order order)
        => order.Status == OrderStatus.Paid
            ? null
            : $"Nothing was queued: only a paid order is sent to fulfillment partners, and order {order.OrderNumber} is {ToCamel(order.Status.ToString())}.";

    private static string MayAlreadyBeMade(IEnumerable<IFulfillmentProvider> providers, string orderNumber, IEnumerable<FulfillmentRequest> requests)
    {
        var partners = string.Join(", ", requests.Select(request => PartnerName(providers, request.ProviderName)).Distinct(StringComparer.Ordinal));

        return $"{partners} may already have order {orderNumber}, because the partner did not answer clearly or sending was interrupted. Ask the user to check the partner's dashboard first, or the order may be made twice. Call again with checkedWithPartner true only after the user confirms the partner does not have it.";
    }
}
