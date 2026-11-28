using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Copies an activity load, as the Clone action of the Load Activities page does. Creating a load from scratch and
/// editing one stay on the page: its editor holds the source-specific rules a load must pass.
/// </summary>
public sealed class SaveActivityBatchTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveActivityBatch";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "cloneFromId": {
          "type": "string",
          "description": "The ID of the load to copy, from listActivityBatches."
        },
        "displayText": {
          "type": "string",
          "description": "The title of the copy. Defaults to the source's title followed by (copy)."
        }
      },
      "required": ["cloneFromId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Copies an existing activity load definition as a new, unloaded load (same source, campaign, channel, subject, filters and assignees), optionally with a new title. Saving does not load it. Creating a load from scratch or changing its settings is done on the Load Activities page; the result links to the copy's editor.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageActivityBatches))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var sourceId = AIModuleTools.ReadString(arguments, "cloneFromId");

        if (sourceId is null)
        {
            return $"Nothing was saved: give the ID of the load to copy in cloneFromId. A new load is created on the Load Activities page: {OmnichannelToolSupport.GetPath(services, "ActivityBatches", "Index")}";
        }

        var manager = services.GetRequiredService<ICatalogManager<OmnichannelActivityBatch>>();
        var source = await manager.FindByIdAsync(sourceId, cancellationToken);

        if (source is null)
        {
            return $"Nothing was saved: no activity load has the ID '{sourceId}'. Call listActivityBatches to find it.";
        }

        var displayText = AIModuleTools.ReadString(arguments, "displayText") ?? $"{source.DisplayText} (copy)";
        var copy = ActivityBatchCloner.CreateCopy(source, await manager.NewAsync(cancellationToken: cancellationToken), displayText);

        await manager.CreateAsync(copy, cancellationToken);

        return AIModuleTools.Json(new
        {
            saved = "created",
            copiedFrom = new
            {
                id = source.ItemId,
                displayText = source.DisplayText,
            },
            load = await ListActivityBatchesTool.DescribeAsync(services, copy, full: false),
            note = "The copy has not been loaded. Review it on its edit page, then load it with loadActivityBatch.",
        });
    }
}
