using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// Adds a webhook, or changes one by id, through the checks of the webhook editor: a name, an https address the egress
/// guard allows, and all events or at least one event an enabled module declares. A new webhook gets a signing secret
/// that is shown on its admin page only. It sends site data to another system, so it asks for approval.
/// </summary>
public sealed class SaveWebhookTool : WebhooksAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveWebhook";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the webhook to change, from listWebhooks. Leave it out to add a webhook."
        },
        "name": {
          "type": "string",
          "description": "What the webhook is called, such as the system it feeds. Required for a new webhook."
        },
        "url": {
          "type": "string",
          "description": "The full https:// address events are posted to. Required for a new webhook."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether events are sent. A new webhook is enabled unless this is false."
        },
        "allEvents": {
          "type": "boolean",
          "description": "Send every event, those added later included. When true, events is ignored."
        },
        "events": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The names of the events to send, from listWebhookEvents, such as order.paid. Giving events without allEvents sends only these."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a webhook that posts signed JSON for chosen events, or all events, to an https URL, or updates one by id, including enabling or disabling it. A new webhook gets a signing secret that the administrator copies from its admin page; the secret is never returned.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var subscriptions = services.GetRequiredService<IWebhookSubscriptionStore>();
        var commands = services.GetRequiredService<WebhookSubscriptionCommands>();
        var id = ReadString(arguments, "id");
        var existing = id is null ? null : await subscriptions.FindByIdAsync(id, cancellationToken);

        if (id is not null && existing is null)
        {
            return $"There is no webhook with the id '{id}'. Call listWebhooks to find it.";
        }

        var events = ReadStringList(arguments, "events");
        var allEvents = ReadBool(arguments, "allEvents");
        var input = new WebhookSubscriptionInput
        {
            Name = ReadString(arguments, "name") ?? existing?.Name,
            Url = ReadString(arguments, "url") ?? existing?.Url,
            IsEnabled = ReadBool(arguments, "enabled") ?? existing?.IsEnabled ?? true,
            AllEvents = allEvents ?? (events is null && existing?.Events?.Contains(WebhookEvents.All) == true),
            Events = events ?? existing?.Events?.Where(entry => entry != WebhookEvents.All).ToList() ?? [],
        };

        var errors = commands.Validate(input);

        if (errors.Count > 0)
        {
            return Json(new
            {
                saved = false,
                errors = errors.Select(error => error.Message).ToList(),
                hint = "Call listWebhookEvents for the event names.",
            });
        }

        var known = services.GetRequiredService<IWebhookEventCatalog>().GetEvents().Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
        var ignoredEvents = input.AllEvents ? [] : (input.Events ?? []).Where(entry => !known.Contains(entry)).Distinct(StringComparer.Ordinal).ToList();
        var webhook = existing;

        if (webhook is null)
        {
            webhook = await commands.CreateAsync(input);
        }
        else
        {
            await commands.UpdateAsync(webhook, input);
        }

        return Json(new
        {
            saved = true,
            created = existing is null,
            webhook = WebhookToolResults.Describe(
                webhook,
                await ToLocalAsync(services, webhook.CreatedUtc),
                await ToLocalAsync(services, webhook.ModifiedUtc),
                GetEditPath(services, webhook.ItemId)),
            ignoredEvents = ignoredEvents.Count > 0 ? ignoredEvents : null,
            ignoredEventsNote = ignoredEvents.Count > 0 ? "No enabled module declares these events, so they were not saved." : null,
            note = existing is null
                ? "A signing secret was created. It is never shown here: copy it from the webhook's admin page (editUrl) into the receiver, or the receiver rejects the deliveries."
                : null,
        });
    }
}
