using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.AI.Agent.Indexing;

/// <summary>
/// Deletes and recreates the physical index of search index profiles and starts indexing again, as the Rebuild action
/// of the Indexes admin page does.
/// </summary>
public sealed class RebuildIndexProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "rebuildIndexProfile";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(15);

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "indexes": {
          "type": "array",
          "description": "The IDs or the names of the index profiles to rebuild, from listIndexProfiles. At most 20.",
          "items": {
            "type": "string"
          },
          "minItems": 1,
          "maxItems": 20
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
    public override string Description => "Deletes and recreates the physical index of one or more index profiles and re-indexes everything in the background. Search on those indexes returns incomplete results until indexing finishes. The result says which indexes were recreated and that indexing was started, not that it is finished.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, IndexingPermissions.ManageIndexes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var indexProfileManager = services.GetRequiredService<IIndexProfileManager>();
        var (indexProfiles, notFound, error) = await IndexProfileContent.ResolveAsync(arguments, indexProfileManager);

        if (error is not null)
        {
            return $"Nothing was rebuilt: {error}";
        }

        if (notFound.Count > 0)
        {
            return $"Nothing was rebuilt: there is no index profile with the ID or the name {string.Join(", ", notFound.Select(index => $"'{index}'"))}. Use listIndexProfiles to get them.";
        }

        var distributedLock = services.GetRequiredService<IDistributedLock>();
        var rebuilt = new List<object>();
        var failed = new List<object>();

        foreach (var indexProfile in indexProfiles)
        {
            var indexManager = services.GetKeyedService<IIndexManager>(indexProfile.ProviderName);

            if (indexManager is null)
            {
                failed.Add(new
                {
                    id = indexProfile.Id,
                    name = indexProfile.Name,
                    error = $"The provider '{indexProfile.ProviderName}' has no index manager, so its index cannot be rebuilt.",
                });

                continue;
            }

            // The lock the indexing of this index takes, as the rebuild step of a recipe does, so the index is not
            // recreated while it is being written.
            var (locker, locked) = await distributedLock.TryAcquireLockAsync($"IndexingService-{indexProfile.Id}", _lockTimeout, _lockExpiration);

            if (!locked)
            {
                failed.Add(new
                {
                    id = indexProfile.Id,
                    name = indexProfile.Name,
                    error = "The index is being indexed right now. Try again in a moment.",
                });

                continue;
            }

            try
            {
                await indexProfileManager.ResetAsync(indexProfile);
                await indexProfileManager.UpdateAsync(indexProfile);

                if (await indexManager.RebuildAsync(indexProfile))
                {
                    await indexProfileManager.SynchronizeAsync(indexProfile);

                    rebuilt.Add(new
                    {
                        id = indexProfile.Id,
                        name = indexProfile.Name,
                        provider = indexProfile.ProviderName,
                    });
                }
                else
                {
                    failed.Add(new
                    {
                        id = indexProfile.Id,
                        name = indexProfile.Name,
                        error = "The provider could not recreate the index. The site's log has the reason.",
                    });
                }
            }
            finally
            {
                await locker.DisposeAsync();
            }
        }

        return AgentToolResults.Json(new
        {
            rebuilt,
            failed = failed.Count > 0
                ? failed
                : null,
            note = rebuilt.Count > 0
                ? "The physical index of each rebuilt index profile was deleted and created again, empty. Indexing was started in the background and is not finished yet: search results are incomplete until it is."
                : null,
        });
    }
}
