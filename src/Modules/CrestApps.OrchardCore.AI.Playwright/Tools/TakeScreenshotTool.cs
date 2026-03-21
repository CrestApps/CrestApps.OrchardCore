using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class TakeScreenshotTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "fullPage": {
              "type": "boolean",
              "description": "When true, captures the full page instead of only the current viewport. Defaults to false."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.TakeScreenshot;
    public override string Description => "Captures a screenshot of the current page and returns the saved file path.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var fullPage = false;
        if (arguments.TryGetValue("fullPage", out var fullPageValue))
        {
            fullPage = fullPageValue switch
            {
                bool isFullPage => isFullPage,
                JsonElement jsonFullPage when jsonFullPage.ValueKind is JsonValueKind.True or JsonValueKind.False => jsonFullPage.GetBoolean(),
                _ => false,
            };
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IPlaywrightPageInspectionService>()
                .TakeScreenshotAsync(session, fullPage, token);

            return Serialize(result);
        });
    }
}
