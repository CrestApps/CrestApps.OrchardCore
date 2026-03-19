using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class GetBrowserSessionTool : BrowserAutomationToolBase<GetBrowserSessionTool>
{
    public const string TheName = "getBrowserSession";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "sessionId": {
              "type": "string",
              "description": "The browser session identifier to inspect."
            }
          },
          "required": [
            "sessionId"
          ],
          "additionalProperties": false
        }
        """);

    public GetBrowserSessionTool(BrowserAutomationService browserAutomationService, ILogger<GetBrowserSessionTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Returns details about a specific browser session, including tracked tabs.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var snapshot = await BrowserAutomationService.GetSessionSnapshotAsync(GetSessionId(arguments), cancellationToken);
            return Success(TheName, snapshot);
        });
    }
}

