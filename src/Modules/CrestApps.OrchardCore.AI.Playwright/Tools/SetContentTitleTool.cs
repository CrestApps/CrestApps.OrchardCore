using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class SetContentTitleTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["title"],
          "properties": {
            "title": {
              "type": "string",
              "description": "The exact title value to set in the Orchard editor."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.SetContentTitle;
    public override string Description => "Sets the Orchard content Title field using label-first fallback-safe locators. This edits the field only and does not save or publish.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var title = arguments["title"]?.ToString();
        if (string.IsNullOrWhiteSpace(title))
        {
            return new ValueTask<object?>("Parameter 'title' is required.");
        }

        return ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            $"set_content_title:{title}",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().SetContentTitleAsync(session, title, token));
    }
}
