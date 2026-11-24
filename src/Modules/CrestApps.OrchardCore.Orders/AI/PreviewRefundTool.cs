using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Shows what a refund of an order would do, without moving money (the owner's review, F23): what is still refundable,
/// the amount, and where it would go, planned by the same service and in the same order as the refund itself
/// (<see cref="IOrderRefundPlanner"/>). It applies <c>refundOrder</c>'s rules, so a preview is issued only for a refund
/// <c>refundOrder</c> would accept, and returns a token <c>refundOrder</c> requires: tied to the caller and to the
/// refund's fingerprint (order, amount, currency, <c>refundAll</c>, <c>asStoreCredit</c>, <c>restock</c> and
/// <c>returnId</c>, with what is still refundable, what was refunded so far and the payments the refund would go to, so a
/// refund made or a payment changed after the preview makes the token fail), used once, and valid for
/// <see cref="AIActionPreviewTokens.Lifetime"/>.
/// </summary>
internal sealed class PreviewRefundTool : OrderRefundAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "previewRefund";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "orderId": {
          "type": "string",
          "description": "The order's id, from listOrders."
        },
        "orderNumber": {
          "type": "string",
          "description": "The order's number, when the id is not known."
        },
        "amount": {
          "type": "number",
          "description": "How much to give back, more than zero, in the order's currency. With refundAll it must be everything still refundable, from getOrder."
        },
        "currency": {
          "type": "string",
          "description": "The order's currency, such as USD."
        },
        "refundAll": {
          "type": "boolean",
          "description": "Set to true to give back everything still refundable; amount must then be exactly that."
        },
        "restock": {
          "type": "boolean",
          "description": "Put what the order sold back into stock once everything is refunded."
        },
        "asStoreCredit": {
          "type": "boolean",
          "description": "Give the amount as store credit for the buyer instead of back to how they paid (only when getOrder says canRefundAsStoreCredit)."
        },
        "returnId": {
          "type": "string",
          "description": "The id of an approved or received return of this order, from listOrderReturns, when the money is given back for that return. It cannot be combined with restock or asStoreCredit."
        }
      },
      "required": ["amount", "currency"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Shows what refundOrder would do, without moving money: what is still refundable for the order, the amount, and where it would go back to (newest online payment first, then Pay Later and offline payments, then gift cards and store credit, or store credit when asked), with any payment that would be skipped. Call it before every refund and show the result to the user. It returns a previewToken that refundOrder requires: it works once, only for you and for the same order, amount, currency and options, and expires after 10 minutes.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (order, error) = await FindOrderAsync(services, arguments, cancellationToken);

        if (order is null)
        {
            return error;
        }

        var (request, refusal) = await ReadRefundAsync(services, arguments, order, "No refund was previewed", cancellationToken);

        if (request is null)
        {
            return refusal;
        }

        var cache = services.GetService<IDistributedCache>();

        if (cache is null)
        {
            return "No refund was previewed: the site can't keep preview tokens right now, so a refund from a chat can't be checked first. Refund it from the order's page.";
        }

        // The plan the token's fingerprint holds, so the refund goes where this preview shows.
        var plan = request.Plan;

        var utcNow = services.GetRequiredService<IClock>().UtcNow;
        var (token, expiresUtc) = await AIActionPreviewTokens.IssueAsync(cache, PreviewPurpose, GetPreviewScope(services, caller), request.Fingerprint, utcNow, cancellationToken);

        return Json(new
        {
            orderId = order.ItemId,
            number = order.OrderNumber,
            currency = order.Currency,
            refundable = request.Totals.Refundable,
            amount = request.Amount,
            refundAll = request.RefundAll,
            asStoreCredit = request.AsStoreCredit,
            restock = request.Restock,
            returnId = request.Return?.ItemId,
            returnNumber = request.Return?.Number,
            goesTo = plan is null ? null : await DescribeAsync(services, plan.Portions),
            skipped = plan is { Skipped.Count: > 0 } ? await DescribeAsync(services, plan.Skipped) : null,
            notCovered = plan is { NotCovered: > 0m } ? plan.NotCovered : (decimal?)null,
            previewToken = token,
            expiresAt = await LocalTimeAsync(services, expiresUtc),
            notes = GetNotes(plan, request),
            message = $"Nothing was refunded. Show this to the user. To refund {Format(request.Amount, order.Currency)} on order {order.OrderNumber}, call refundOrder with the same order, amount, currency and options, this previewToken and a new idempotencyKey, before the token expires.",
        });
    }

    private static async Task<List<object>> DescribeAsync(IServiceProvider services, List<OrderRefundPortion> portions)
    {
        var described = new List<object>();

        foreach (var portion in portions)
        {
            described.Add(new
            {
                kind = ToCamel(portion.Kind.ToString()),
                description = portion.Description,
                amount = portion.Amount,
                paidAt = await LocalTimeAsync(services, portion.PaidUtc),
                providerCanRefund = portion.ProviderSupportsRefunds,
            });
        }

        return described;
    }

    private static List<string> GetNotes(OrderRefundPlan plan, OrderRefundRequest request)
    {
        var notes = new List<string>();

        if (plan is null)
        {
            notes.Add("Where the money would go can't be shown here; the order's page shows its payments.");
        }
        else
        {
            if (plan.Skipped.Count > 0)
            {
                notes.Add("A refund of a skipped payment is waiting to be settled by hand on the Refunds page, so that payment is not refunded again until it is settled.");
            }

            if (plan.NotCovered > 0m)
            {
                notes.Add($"No payment would take {Format(plan.NotCovered, request.Order.Currency)} of the amount, so the refund would come to less than asked.");
            }

            if (plan.Portions.Any(portion => portion.ProviderSupportsRefunds == false))
            {
                notes.Add("A payment method that can't refund automatically is recorded for settling by hand; no money moves until someone settles it.");
            }
        }

        if (request.Return is not null)
        {
            notes.Add("The refund closes the return and emails the buyer.");
        }

        return notes.Count > 0 ? notes : null;
    }
}
