using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

/// <summary>
/// Navigates the Playwright browser to a given URL.
/// </summary>
public class BrowserNavigateTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["url"],
          "properties": {
            "url": {
              "type": "string",
              "description": "The URL to navigate to. Use a relative path or absolute URL."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.Navigate;
    public override string Description => "Navigates the browser to the specified URL and returns the final URL plus the observed page state.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var url = arguments["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(url))
            {
                return "Parameter 'url' is required.";
            }

            var absoluteUrl = ResolveAbsoluteUrl(session, url);
            var observationService = arguments.Services.GetRequiredService<IPlaywrightObservationService>();
            var response = await session.Page.GotoAsync(absoluteUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000,
            }).WaitAsync(token);
            var observation = await observationService.CaptureAsync(session, token);

            return Serialize(new
            {
                navigatedTo = absoluteUrl,
                status = response?.Status,
                observation,
            });
        });
    }
}
