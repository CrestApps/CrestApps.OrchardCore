using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Sends the buyer the invoice of an order the store put together, or the quote, with the link to review and pay it, as
/// the order page's Send invoice button does (<see cref="DraftOrderService.SendAsync"/>): as an invoicing module's document
/// when one sends it, otherwise as the order's own e-mail. Sending again is allowed while it is unpaid. It sends an email,
/// so it asks for approval.
/// </summary>
internal sealed class SendOrderInvoiceTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendOrderInvoice";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "orderId": {
          "type": "string",
          "description": "The draft order's id, from saveDraftOrder or listOrders."
        },
        "orderNumber": {
          "type": "string",
          "description": "The order's number, when the id is not known."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Emails the buyer the invoice of a draft order, or the quote, with the link to review and pay it; sending again is allowed while it is unpaid. It needs the site's email and base URL to be set up.";

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

        if (order.Invoice is null)
        {
            return $"Nothing was sent: order {order.OrderNumber} was not put together by the store, so it has no invoice to send. Use saveDraftOrder to make one.";
        }

        if (order.Status is OrderStatus.Paid or OrderStatus.Canceled)
        {
            return $"Nothing was sent: order {order.OrderNumber} is {ToCamel(order.Status.ToString())}, and only an unpaid order's invoice can be sent.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        // Sent as the order page's Send button sends it: as another module's document when one sends it (an invoicing
        // module's invoice or estimate), otherwise as the order's own e-mail with its pay link.
        var sent = await services.GetRequiredService<DraftOrderService>().SendAsync(order, GetActor(caller), cancellationToken);

        if (!sent.Sent)
        {
            return "Nothing was sent: " + (sent.Errors.Count > 0 ? string.Join(" ", sent.Errors) : "the invoice couldn't be emailed. Check the email settings and the site's base URL.");
        }

        var kind = order.Quote is null ? "invoice" : "quote";

        if (await CommitAsync(services, cancellationToken) is not null)
        {
            return $"The {kind} was emailed to {order.CustomerEmail}, but the order's record of it was not saved because the order changed at the same moment. Do not send it again; check the order's page.";
        }

        return Json(new
        {
            orderId = order.ItemId,
            number = order.OrderNumber,
            sent = kind,
            sentTo = order.CustomerEmail,
            payBy = order.Invoice.DueUtc?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            document = sent.DocumentNumber,
            message = sent.Message,
        });
    }
}
