using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class GetPageContentTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "maxLength": {
              "type": "integer",
              "description": "Maximum number of characters to return from the visible page content. Defaults to 4000."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.InspectPageContent;
    public override string Description => "Returns the visible content of the current page for grounded follow-up questions.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var maxLength = 4000;
        if (arguments.TryGetValue("maxLength", out var maxLengthValue)
            && maxLengthValue is JsonElement jsonMaxLength
            && jsonMaxLength.ValueKind == JsonValueKind.Number)
        {
            maxLength = jsonMaxLength.GetInt32();
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IPlaywrightPageInspectionService>()
                .GetPageContentAsync(session, maxLength, token);

            return Serialize(result);
        });
    }
}
