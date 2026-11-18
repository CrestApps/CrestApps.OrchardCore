using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// Deletes a webhook and its delivery log, as the webhooks admin page does.
/// </summary>
public sealed class DeleteWebhookTool : WebhooksAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteWebhook";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the webhook to delete, from listWebhooks."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a webhook and its delivery log. Nothing more is sent to it.";

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

        await services.GetRequiredService<WebhookSubscriptionCommands>().DeleteAsync(webhook, cancellationToken);

        return Json(new
        {
            deleted = true,
            id = webhook.ItemId,
            name = webhook.Name,
            note = $"Nothing more is sent to it. Up to {WebhookSubscriptionCommands.DeliveriesDeletedWithWebhook} logged deliveries were deleted with it; any others are cleared with the log's old entries.",
        });
    }
}
