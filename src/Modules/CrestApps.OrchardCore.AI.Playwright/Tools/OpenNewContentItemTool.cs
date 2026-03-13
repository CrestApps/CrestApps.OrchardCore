using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class OpenNewContentItemTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["contentType"],
          "properties": {
            "contentType": {
              "type": "string",
              "description": "The Orchard content type to create, for example SitePage."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.OpenNewContentItem;
    public override string Description => "Starts the Orchard create-content flow for a specific content type and waits for the editor to load.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var contentType = arguments["contentType"]?.ToString();
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return new ValueTask<object?>("Parameter 'contentType' is required.");
        }

        return ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            $"open_new_content_item:{contentType}",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().OpenNewContentItemAsync(session, contentType, token));
    }
}
