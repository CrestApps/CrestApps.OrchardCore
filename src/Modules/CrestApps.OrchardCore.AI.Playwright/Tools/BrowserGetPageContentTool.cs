using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

/// <summary>
/// Returns the visible text content of the current page.
/// </summary>
public sealed class BrowserGetPageContentTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "max_length": {
              "type": "integer",
              "description": "Maximum number of characters to return. Defaults to 4000."
            }
          },
          "additionalProperties": false
        }
        """);

    private static readonly Regex _whitespace = new(@"\s{2,}", RegexOptions.Compiled);

    public override string Name => PlaywrightConstants.ToolNames.GetPageContent;
    public override string Description => "Returns the visible text content of the current browser page.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var maxLength = 4000;
            if (arguments.TryGetValue("max_length", out var maxLengthValue)
                && maxLengthValue is JsonElement jsonMaxLength
                && jsonMaxLength.ValueKind == JsonValueKind.Number)
            {
                maxLength = Math.Clamp(jsonMaxLength.GetInt32(), 500, 16_000);
            }

            var text = await session.Page.EvaluateAsync<string>("""
                (() => {
                    const clone = document.body.cloneNode(true);
                    clone.querySelectorAll('script, style, noscript, [hidden], [aria-hidden="true"]').forEach(el => el.remove());
                    return clone.innerText || '';
                })()
                """).WaitAsync(token);

            text = _whitespace.Replace(text ?? string.Empty, " ").Trim();
            if (text.Length > maxLength)
            {
                text = text[..maxLength] + $"\n\n[...truncated - {text.Length - maxLength} more characters]";
            }

            return Serialize(new
            {
                url = session.Page.Url,
                title = await session.Page.TitleAsync().WaitAsync(token),
                content = text,
            });
        });
    }
}
