using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class SwitchBrowserTabTool : BrowserAutomationToolBase<SwitchBrowserTabTool>
{
    public const string TheName = "switchBrowserTab";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "sessionId": {
              "type": "string",
              "description": "The browser session identifier."
            },
            "pageId": {
              "type": "string",
              "description": "The page identifier to activate."
            }
          },
          "required": [
            "pageId"
          ],
          "additionalProperties": false
        }
        """);

    public SwitchBrowserTabTool(BrowserAutomationService browserAutomationService, ILogger<SwitchBrowserTabTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Marks a specific browser tab as the active tab for future actions.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var snapshot = await BrowserAutomationService.SwitchActivePageAsync(GetSessionId(arguments), GetRequiredString(arguments, "pageId"), cancellationToken);
            return Success(TheName, snapshot);
        });
    }
}

