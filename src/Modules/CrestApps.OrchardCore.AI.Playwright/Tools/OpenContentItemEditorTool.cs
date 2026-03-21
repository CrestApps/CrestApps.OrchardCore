using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class OpenContentItemEditorTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["title"],
          "properties": {
            "title": {
              "type": "string",
              "description": "Title of the content item to open for editing. The tool uses the current content list first and can use fuzzy matching when needed."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.OpenContentItemEditor;
    public override string Description => "Opens the editor for an existing Orchard content item by title using the current content list first and row-scoped edit behavior when available.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var title = arguments["title"]?.ToString();
        if (string.IsNullOrWhiteSpace(title))
        {
            return new ValueTask<object?>("Parameter 'title' is required.");
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>()
                .OpenContentItemEditorAsync(session, title, token);

            return Serialize(result);
        });
    }
}
