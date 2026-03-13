using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class WaitForUrlTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["urlPattern"],
          "properties": {
            "urlPattern": {
              "type": "string",
              "description": "A URL substring that should appear in the current page URL."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Maximum number of milliseconds to wait."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.WaitForUrl;
    public override string Description => "Waits until the page URL matches the expected Orchard navigation target.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var urlPattern = arguments["urlPattern"]?.ToString();
        if (string.IsNullOrWhiteSpace(urlPattern))
        {
            return new ValueTask<object?>("Parameter 'urlPattern' is required.");
        }

        var timeoutMs = 15_000;
        if (arguments.TryGetValue("timeoutMs", out var timeoutValue)
            && timeoutValue is JsonElement jsonTimeout
            && jsonTimeout.ValueKind == JsonValueKind.Number)
        {
            timeoutMs = jsonTimeout.GetInt32();
        }

        return ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            $"wait_for_url:{urlPattern}",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().WaitForUrlAsync(session, urlPattern, timeoutMs, token));
    }
}
