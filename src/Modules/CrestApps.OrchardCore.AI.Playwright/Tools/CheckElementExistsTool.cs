using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class CheckElementExistsTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["query"],
          "properties": {
            "query": {
              "type": "string",
              "description": "Visible text, widget name, label, or control name to check for on the current page."
            },
            "maxMatches": {
              "type": "integer",
              "description": "Maximum number of matching elements to include in the response. Defaults to 3."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.CheckElementExists;
    public override string Description => "Checks whether a requested control, widget, or text snippet is currently visible on the page.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var query = arguments["query"]?.ToString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ValueTask<object?>("Parameter 'query' is required.");
        }

        var maxMatches = 3;
        if (arguments.TryGetValue("maxMatches", out var maxMatchesValue)
            && maxMatchesValue is JsonElement jsonMaxMatches
            && jsonMaxMatches.ValueKind == JsonValueKind.Number)
        {
            maxMatches = jsonMaxMatches.GetInt32();
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IPlaywrightPageInspectionService>()
                .CheckElementExistsAsync(session, query, maxMatches, token);

            return Serialize(result);
        });
    }
}
