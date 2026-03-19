using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class CloseBrowserSessionTool : BrowserAutomationToolBase<CloseBrowserSessionTool>
{
    public const string TheName = "closeBrowserSession";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "sessionId": {
              "type": "string",
              "description": "The browser session identifier returned by startBrowserSession."
            }
          },
          "required": [
            "sessionId"
          ],
          "additionalProperties": false
        }
        """);

    public CloseBrowserSessionTool(BrowserAutomationService browserAutomationService, ILogger<CloseBrowserSessionTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Closes an existing browser session and disposes all tracked pages.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var snapshot = await BrowserAutomationService.CloseSessionAsync(GetSessionId(arguments), cancellationToken);
            return Success(TheName, snapshot);
        });
    }
}

