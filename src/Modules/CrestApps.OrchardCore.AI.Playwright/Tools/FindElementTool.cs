using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class FindElementTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["query"],
          "properties": {
            "query": {
              "type": "string",
              "description": "Visible text, widget name, label, or control name to search for on the current page."
            },
            "maxMatches": {
              "type": "integer",
              "description": "Maximum number of matches to return. Defaults to 5."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.FindElement;
    public override string Description => "Finds visible page elements that match a requested widget name, label, or text snippet.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var query = arguments["query"]?.ToString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ValueTask<object?>("Parameter 'query' is required.");
        }

        var maxMatches = 5;
        if (arguments.TryGetValue("maxMatches", out var maxMatchesValue)
            && maxMatchesValue is JsonElement jsonMaxMatches
            && jsonMaxMatches.ValueKind == JsonValueKind.Number)
        {
            maxMatches = jsonMaxMatches.GetInt32();
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IPlaywrightPageInspectionService>()
                .FindElementsAsync(session, query, maxMatches, token);

            return Serialize(result);
        });
    }
}
