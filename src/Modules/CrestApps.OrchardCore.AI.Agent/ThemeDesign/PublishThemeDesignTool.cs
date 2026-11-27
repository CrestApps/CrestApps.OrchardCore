using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Publishes the user's theme draft.
/// </summary>
public sealed class PublishThemeDesignTool : ThemeDesignTool
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "publishThemeDesign";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {

      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => TheName;

    /// <inheritdoc />
    public override string Description => "Publishes every change in the user's theme draft: templates and site resources go live for everyone at once, and the draft is deleted. Nothing is published when any change can't be. Only call it when the user asks to publish.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var draftService = GetDraftService(services);
        var count = (await draftService.GetDraftAsync())?.ChangeCount ?? 0;
        var result = await draftService.PublishAsync();

        return Report(result, $"The draft is published: {count} change(s) are live for everyone.");
    }
}
