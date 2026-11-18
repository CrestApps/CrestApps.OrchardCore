using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// Lists the delivery log of one webhook, newest first, as its Deliveries page does, optionally by status. Payloads are
/// not returned.
/// </summary>
public sealed class ListWebhookDeliveriesTool : WebhooksAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listWebhookDeliveries";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "webhookId": {
          "type": "string",
          "description": "The id of the webhook, from listWebhooks."
        },
        "status": {
          "type": "string",
          "enum": ["Pending", "Delivered", "Failed"],
          "description": "Only deliveries in this status: Pending (waiting to be sent or sent again), Delivered, or Failed (every attempt failed). Leave it out for all."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of deliveries per page, 20 by default and at most 50."
        }
      },
      "required": ["webhookId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the deliveries of one webhook with event, status, attempts, last status code, last error and times, filtered by status, with paging. Use it to find out why a receiver is not getting events.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var webhookId = ReadString(arguments, "webhookId");

        if (webhookId is null)
        {
            return "Give the webhook's id in webhookId. Call listWebhooks to find it.";
        }

        WebhookDeliveryStatus? status = null;
        var statusText = ReadString(arguments, "status");

        if (statusText is not null)
        {
            if (!Enum.TryParse<WebhookDeliveryStatus>(statusText, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(statusText, out _))
            {
                return "Give status as Pending, Delivered or Failed, or leave it out.";
            }

            status = parsed;
        }

        var webhook = await services.GetRequiredService<IWebhookSubscriptionStore>().FindByIdAsync(webhookId, cancellationToken);

        if (webhook is null)
        {
            return $"There is no webhook with the id '{webhookId}'. Call listWebhooks to find it.";
        }

        var (page, pageSize) = ReadPaging(arguments);
        var (total, deliveries) = await services.GetRequiredService<IWebhookDeliveryStore>().PageAsync(webhook.ItemId, status, (page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var delivery in deliveries)
        {
            items.Add(new
            {
                id = delivery.ItemId,
                eventId = delivery.EventId,
                @event = delivery.EventName,
                status = delivery.Status.ToString(),
                attempts = delivery.Attempts,
                lastStatusCode = delivery.LastStatusCode,
                lastError = delivery.LastError,
                created = await ToLocalAsync(services, delivery.CreatedUtc),
                lastAttempt = await ToLocalAsync(services, delivery.LastAttemptUtc),
                nextAttempt = await ToLocalAsync(services, delivery.NextAttemptUtc),
                delivered = await ToLocalAsync(services, delivery.DeliveredUtc),
            });
        }

        return Json(new
        {
            webhookId = webhook.ItemId,
            webhookName = webhook.Name,
            items,
            total,
            page,
            pageSize,
            truncated = page * pageSize < total ? true : (bool?)null,
        });
    }
}
