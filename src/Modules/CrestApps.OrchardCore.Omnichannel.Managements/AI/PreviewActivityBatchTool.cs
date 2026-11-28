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
/// Counts what a saved activity load would match and skip, as the Count matches button of the load editor does,
/// without creating anything.
/// </summary>
public sealed class PreviewActivityBatchTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "previewActivityBatch";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the saved load to count, from listActivityBatches."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Counts how many records a saved load would match and how many would be skipped and why, without creating anything. Use it before loadActivityBatch.";

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

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was counted: give the ID of a saved load in id.";
        }

        var batch = await services.GetRequiredService<ICatalogManager<OmnichannelActivityBatch>>().FindByIdAsync(id, cancellationToken);

        if (batch is null)
        {
            return $"Nothing was counted: no activity load has the ID '{id}'. Call listActivityBatches to find it.";
        }

        // Counted on a copy, as the editor does, so nothing reaches the saved load.
        var preview = await services.GetRequiredService<IActivityBatchLoadCoordinator>().PreviewAsync(batch.Clone(), cancellationToken);

        return AIModuleTools.Json(new
        {
            id = batch.ItemId,
            displayText = batch.DisplayText,
            status = batch.Status.ToString(),
            error = preview.Error,
            matched = preview.Matched,
            wouldLoad = preview.WouldLoad,
            skipped = new
            {
                duplicate = preview.SkippedAsDuplicate,
                optedOut = preview.SkippedAsOptedOut,
                noDestination = preview.SkippedForNoDestination,
                notInService = preview.SkippedAsNotInService,
                lead = preview.SkippedAsLead,
                byLimit = preview.SkippedByLimit,
            },
            note = batch.Status == OmnichannelActivityBatchStatus.New
                ? null
                : "This load has already been started or loaded, and a load runs only once. Copy it with saveActivityBatch to load these records again.",
        });
    }
}
