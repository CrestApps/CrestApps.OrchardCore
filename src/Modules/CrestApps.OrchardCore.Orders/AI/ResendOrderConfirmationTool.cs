using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Emails a paid order's confirmation to the customer again and notes it on the order, as the order page's Resend button
/// does (<see cref="IOrderOperationsService.ResendConfirmationAsync"/>). It is offered when the page offers the button:
/// the order is paid, has a customer email, and the site can send email (<see cref="OrderConfirmationSender.CanSend"/>).
/// It sends an email, so it asks for approval.
/// </summary>
internal sealed class ResendOrderConfirmationTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "resendOrderConfirmation";

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
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Emails a paid order's confirmation to the customer again and notes it on the order. It needs the order to have a customer email and the site's email to be set up.";

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

        if (order.Status != OrderStatus.Paid)
        {
            return $"Nothing was sent: order {order.OrderNumber} is not paid, so it has no confirmation to send.";
        }

        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            return $"Nothing was sent: order {order.OrderNumber} has no customer email.";
        }

        if (!services.GetRequiredService<OrderConfirmationSender>().CanSend)
        {
            return "Nothing was sent: the site's email is not set up. Set it up under the email settings first.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        if (!await services.GetRequiredService<IOrderOperationsService>().ResendConfirmationAsync(order, GetActor(caller), cancellationToken))
        {
            return "The confirmation couldn't be sent. Check the site's email settings.";
        }

        // The email went; the note that says so is written now, so a refused save is not mistaken for an unsent email.
        if (await CommitAsync(services, cancellationToken) is not null)
        {
            return $"The confirmation was sent to {order.CustomerEmail}, but the note recording it on the order was not saved because the order changed at the same moment. Do not send it again.";
        }

        return Json(new
        {
            orderId = order.ItemId,
            number = order.OrderNumber,
            sentTo = order.CustomerEmail,
            message = $"The confirmation was sent again to {order.CustomerEmail}.",
        });
    }
}
