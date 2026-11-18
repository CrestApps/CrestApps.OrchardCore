using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// Sends one logged delivery of a webhook again from the start, as the Resend button of its Deliveries page does. The
/// delivery must belong to the webhook named, so a delivery that is not for a webhook is never resent from here.
/// </summary>
public sealed class ResendWebhookDeliveryTool : WebhooksAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "resendWebhookDelivery";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "webhookId": {
          "type": "string",
          "description": "The id of the webhook the delivery was sent to, from listWebhooks."
        },
        "deliveryId": {
          "type": "string",
          "description": "The id of the delivery to send again, from listWebhookDeliveries."
        }
      },
      "required": ["webhookId", "deliveryId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends one logged delivery to its webhook again and returns the receiver's status code or the error.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var webhookId = ReadString(arguments, "webhookId");
        var deliveryId = ReadString(arguments, "deliveryId");

        if (webhookId is null || deliveryId is null)
        {
            return "Give the webhook's id in webhookId and the delivery's id in deliveryId. Call listWebhookDeliveries to find them.";
        }

        var delivery = await services.GetRequiredService<IWebhookDeliveryStore>().FindByIdAsync(deliveryId, cancellationToken);

        if (delivery is null || !string.Equals(delivery.SubscriptionId, webhookId, StringComparison.Ordinal))
        {
            return $"The webhook '{webhookId}' has no delivery with the id '{deliveryId}'. Call listWebhookDeliveries to find it.";
        }

        await services.GetRequiredService<WebhookDispatcher>().ResendAsync(delivery, cancellationToken);

        return Json(new
        {
            webhookId,
            result = WebhookToolResults.DescribeAttempt(delivery),
        });
    }
}
