using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class CloseBrowserTabTool : BrowserAutomationToolBase<CloseBrowserTabTool>
{
    public const string TheName = "closeBrowserTab";

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
              "description": "Optional page identifier. Defaults to the active tab."
            }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public CloseBrowserTabTool(BrowserAutomationService browserAutomationService, ILogger<CloseBrowserTabTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Closes a browser tab. When pageId is omitted, closes the active tab.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var snapshot = await BrowserAutomationService.ClosePageAsync(GetSessionId(arguments), GetPageId(arguments), cancellationToken);
            return Success(TheName, snapshot);
        });
    }
}

