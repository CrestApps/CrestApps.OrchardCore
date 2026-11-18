using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// Lists the events the enabled modules publish to webhooks, grouped as the webhook editor groups them.
/// </summary>
public sealed class ListWebhookEventsTool : WebhooksAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listWebhookEvents";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the events the site can send to webhooks, grouped, with their names and display names. Use it to choose the events of a webhook.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var events = services.GetRequiredService<IWebhookEventCatalog>().GetEvents();

        return Task.FromResult<object>(Json(new
        {
            groups = events
                .GroupBy(entry => entry.Group)
                .Select(group => new
                {
                    group = group.Key,
                    events = group.Select(entry => new
                    {
                        name = entry.Name,
                        displayName = entry.DisplayName,
                    }).ToList(),
                })
                .ToList(),
            total = events.Count,
            note = "A webhook can also take every event, those added later included, with allEvents in saveWebhook.",
        }));
    }
}
