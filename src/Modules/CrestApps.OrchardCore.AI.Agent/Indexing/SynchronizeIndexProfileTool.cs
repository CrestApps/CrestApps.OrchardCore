using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;

namespace CrestApps.OrchardCore.AI.Agent.Indexing;

/// <summary>
/// Starts the background synchronization of search indexes, as the Synchronize and Reset actions of the Indexes admin
/// page do.
/// </summary>
public sealed class SynchronizeIndexProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "synchronizeIndexProfile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "indexes": {
          "type": "array",
          "description": "The IDs or the names of the index profiles to synchronize, from listIndexProfiles. At most 20.",
          "items": {
            "type": "string"
          },
          "minItems": 1,
          "maxItems": 20
        },
        "reset": {
          "type": "boolean",
          "description": "True indexes every item again from the beginning and keeps the entries the index has. False, the default, indexes only what changed since the last synchronization."
        }
      },
      "required": [
        "indexes"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Triggers a background synchronization of one or more indexes, or with `reset` re-indexes every item from the beginning while keeping the existing entries. Use it when search results are stale; use `rebuildIndexProfile` only when the index structure changed. The result says what was started, not that indexing is finished.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, IndexingPermissions.ManageIndexes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var indexProfileManager = services.GetRequiredService<IIndexProfileManager>();
        var (indexProfiles, notFound, error) = await IndexProfileContent.ResolveAsync(arguments, indexProfileManager);

        if (error is not null)
        {
            return $"Nothing was synchronized: {error}";
        }

        if (notFound.Count > 0)
        {
            return $"Nothing was synchronized: there is no index profile with the ID or the name {string.Join(", ", notFound.Select(index => $"'{index}'"))}. Use listIndexProfiles to get them.";
        }

        var reset = arguments.GetFirstValueOrDefault("reset", false);

        foreach (var indexProfile in indexProfiles)
        {
            if (reset)
            {
                // The Reset action of the Indexes page: forget how far the index was synchronized, then synchronize.
                await indexProfileManager.ResetAsync(indexProfile);
                await indexProfileManager.UpdateAsync(indexProfile);
            }

            await indexProfileManager.SynchronizeAsync(indexProfile);
        }

        return AgentToolResults.Json(new
        {
            started = indexProfiles.Select(indexProfile => new
            {
                id = indexProfile.Id,
                name = indexProfile.Name,
                provider = indexProfile.ProviderName,
            }),
            reset,
            note = reset
                ? "Each index was set to index every item again from the beginning, and the synchronization was started in the background. It is not finished yet: results stay as they were until an item is indexed again."
                : "The synchronization was started in the background. It is not finished yet.",
        });
    }
}
