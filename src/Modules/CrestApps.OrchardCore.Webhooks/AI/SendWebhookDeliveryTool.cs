using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// Sends something to a webhook now: either a test <c>webhook.ping</c> event, as the Send a ping button does, or one
/// logged delivery again from the start, as the Resend button of the Deliveries page does. A resent delivery must belong
/// to the webhook named, so a delivery that is not for a webhook is never resent from here. Both are logged, signed and
/// sent at once, and the receiver's answer is returned.
/// </summary>
internal sealed class SendWebhookDeliveryTool : WebhooksAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendWebhookDelivery";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "webhookId": {
          "type": "string",
          "description": "The id of the webhook to send to, from listWebhooks."
        },
        "deliveryId": {
          "type": "string",
          "description": "The id of a logged delivery of this webhook to send again, from listWebhookDeliveries. Leave it out with ping true."
        },
        "ping": {
          "type": "boolean",
          "description": "True to send a test ping event instead of resending a delivery. Leave it out with deliveryId."
        }
      },
      "required": ["webhookId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends to a webhook now and returns the receiver's status code or the error: with ping true, a test ping event, to check that a receiver is reachable and accepts the signature; with a deliveryId from listWebhookDeliveries, that logged delivery again.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var webhookId = ReadString(arguments, "webhookId");
        var deliveryId = ReadString(arguments, "deliveryId");
        var ping = ReadBool(arguments, "ping") == true;

        if (webhookId is null)
        {
            return "Nothing was sent: give the webhook's id in webhookId. Call listWebhooks to find it.";
        }

        if (ping == (deliveryId is not null))
        {
            return "Nothing was sent: give either ping true to send a test ping, or the deliveryId of a logged delivery to send again, not both.";
        }

        if (ping)
        {
            var webhook = await services.GetRequiredService<IWebhookSubscriptionStore>().FindByIdAsync(webhookId, cancellationToken);

            if (webhook is null)
            {
                return $"Nothing was sent: there is no webhook with the id '{webhookId}'. Call listWebhooks to find it.";
            }

            var pinged = await services.GetRequiredService<WebhookSubscriptionCommands>().PingAsync(webhook, cancellationToken);

            return Json(new
            {
                sent = "ping",
                webhookId = webhook.ItemId,
                name = webhook.Name,
                url = webhook.Url,
                result = WebhookToolResults.DescribeAttempt(pinged),
            });
        }

        var delivery = await services.GetRequiredService<IWebhookDeliveryStore>().FindByIdAsync(deliveryId, cancellationToken);

        if (delivery is null || !string.Equals(delivery.SubscriptionId, webhookId, StringComparison.Ordinal))
        {
            return $"Nothing was sent: the webhook '{webhookId}' has no delivery with the id '{deliveryId}'. Call listWebhookDeliveries to find it.";
        }

        await services.GetRequiredService<WebhookDispatcher>().ResendAsync(delivery, cancellationToken);

        return Json(new
        {
            sent = "delivery",
            webhookId,
            deliveryId,
            result = WebhookToolResults.DescribeAttempt(delivery),
        });
    }
}
