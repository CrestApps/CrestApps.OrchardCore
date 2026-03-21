using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class GetBrowserFormsTool : BrowserAutomationToolBase<GetBrowserFormsTool>
{
    public const string TheName = "getBrowserForms";

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
            },
            "maxItems": {
              "type": "integer",
              "description": "Optional maximum number of forms to return."
            }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public GetBrowserFormsTool(BrowserAutomationService browserAutomationService, ILogger<GetBrowserFormsTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Lists forms and their visible fields on the current page.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var maxItems = GetMaxItems(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var raw = await trackedPage.Page.EvaluateAsync<string>(
                    @"(maxItems) => JSON.stringify(Array.from(document.forms).slice(0, maxItems).map((form, index) => ({
                        index,
                        id: form.id || '',
                        name: form.getAttribute('name') || '',
                        method: form.getAttribute('method') || 'get',
                        action: form.getAttribute('action') || window.location.href,
                        fields: Array.from(form.elements).slice(0, 20).map((field, fieldIndex) => ({
                            index: fieldIndex,
                            tagName: field.tagName,
                            type: field.getAttribute('type') || '',
                            name: field.getAttribute('name') || '',
                            id: field.id || '',
                            placeholder: field.getAttribute('placeholder') || '',
                            required: !!field.required,
                            disabled: !!field.disabled,
                            value: field.type === 'password' ? '' : (field.value || '')
                        }))
                    })))",
                    maxItems);

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    title = await trackedPage.Page.TitleAsync(),
                    url = trackedPage.Page.Url,
                    forms = ParseJson(raw),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

