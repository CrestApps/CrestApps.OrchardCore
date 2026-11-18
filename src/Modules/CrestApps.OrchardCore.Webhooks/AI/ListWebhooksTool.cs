using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// Lists the outbound webhooks, as Configuration › Webhooks does, newest first, with the number of deliveries that failed
/// for good, or returns one by id. The signing secret is never returned.
/// </summary>
public sealed class ListWebhooksTool : WebhooksAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listWebhooks";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "Return only this webhook."
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
          "description": "The number of webhooks per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the outbound webhook subscriptions with name, URL, events and enabled state, with paging or one webhook by id, and how many deliveries to each failed for good. The signing secret is never returned.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var subscriptions = services.GetRequiredService<IWebhookSubscriptionStore>();
        var deliveries = services.GetRequiredService<IWebhookDeliveryStore>();
        var id = ReadString(arguments, "id");

        if (id is not null)
        {
            var webhook = await subscriptions.FindByIdAsync(id, cancellationToken);

            if (webhook is null)
            {
                return $"There is no webhook with the id '{id}'. Call listWebhooks to find it.";
            }

            return Json(new
            {
                webhook = await DescribeAsync(services, deliveries, webhook, cancellationToken),
                note = WebhookToolResults.SecretNote,
            });
        }

        var (page, pageSize) = ReadPaging(arguments);
        var (total, webhooks) = await subscriptions.PageAsync((page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var webhook in webhooks)
        {
            items.Add(await DescribeAsync(services, deliveries, webhook, cancellationToken));
        }

        return Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = page * pageSize < total ? true : (bool?)null,
        });
    }

    private static async Task<Dictionary<string, object>> DescribeAsync(IServiceProvider services, IWebhookDeliveryStore deliveries, WebhookSubscription webhook, CancellationToken cancellationToken)
    {
        var description = WebhookToolResults.Describe(
            webhook,
            await ToLocalAsync(services, webhook.CreatedUtc),
            await ToLocalAsync(services, webhook.ModifiedUtc),
            GetEditPath(services, webhook.ItemId));

        var (failed, _) = await deliveries.PageAsync(webhook.ItemId, WebhookDeliveryStatus.Failed, 0, 1, cancellationToken);

        description["failedDeliveries"] = failed;

        return description;
    }
}
