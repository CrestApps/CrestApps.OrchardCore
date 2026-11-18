using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// Sends a test <c>webhook.ping</c> event to a webhook now, as the Send a ping button does: the delivery is logged and
/// sent at once, signed, and the receiver's answer is returned.
/// </summary>
public sealed class SendWebhookPingTool : WebhooksAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendWebhookPing";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the webhook to ping, from listWebhooks."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sends a test ping event to a webhook now and returns the receiver's status code or the error. Use it to check that a receiver is reachable and accepts the signature.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = ReadString(arguments, "id");

        if (id is null)
        {
            return "Give the webhook's id in id. Call listWebhooks to find it.";
        }

        var webhook = await services.GetRequiredService<IWebhookSubscriptionStore>().FindByIdAsync(id, cancellationToken);

        if (webhook is null)
        {
            return $"There is no webhook with the id '{id}'. Call listWebhooks to find it.";
        }

        var delivery = await services.GetRequiredService<WebhookSubscriptionCommands>().PingAsync(webhook, cancellationToken);

        return Json(new
        {
            webhookId = webhook.ItemId,
            name = webhook.Name,
            url = webhook.Url,
            result = WebhookToolResults.DescribeAttempt(delivery),
        });
    }
}
