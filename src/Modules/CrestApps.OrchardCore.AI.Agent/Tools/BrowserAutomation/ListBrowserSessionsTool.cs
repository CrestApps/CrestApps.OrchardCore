using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class ListBrowserSessionsTool : BrowserAutomationToolBase<ListBrowserSessionsTool>
{
    public const string TheName = "listBrowserSessions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public ListBrowserSessionsTool(BrowserAutomationService browserAutomationService, ILogger<ListBrowserSessionsTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Lists the currently tracked Playwright browser sessions.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessions = await BrowserAutomationService.ListSessionsAsync(cancellationToken);
            return Success(TheName, new
            {
                count = sessions.Count,
                sessions,
            });
        });
    }
}

