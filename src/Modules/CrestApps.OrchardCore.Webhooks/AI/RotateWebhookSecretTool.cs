using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// Gives a webhook a new signing secret, as the Rotate secret button does. The new secret is not returned: the person
/// copies it from the webhook's admin page into the receiver.
/// </summary>
public sealed class RotateWebhookSecretTool : WebhooksAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "rotateWebhookSecret";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the webhook, from listWebhooks."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gives a webhook a new signing secret. Deliveries are signed with it from then on, so the receiver rejects them until the new secret, copied from the webhook's admin page, is entered there. The secret is never returned.";

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

        await services.GetRequiredService<WebhookSubscriptionCommands>().RotateSecretAsync(webhook);

        return Json(new
        {
            rotated = true,
            id = webhook.ItemId,
            name = webhook.Name,
            editUrl = GetEditPath(services, webhook.ItemId),
            note = "The webhook has a new signing secret and deliveries are signed with it from now on. It is never shown here: copy it from the webhook's admin page (editUrl) into the receiver.",
        });
    }
}
