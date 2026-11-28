using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Agent.Indexing;

/// <summary>
/// Deletes search index profiles together with their physical indexes, as the Delete action of the Indexes admin page
/// does.
/// </summary>
public sealed class DeleteIndexProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteIndexProfile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "indexes": {
          "type": "array",
          "description": "The IDs or the names of the index profiles to delete, from listIndexProfiles. At most 20.",
          "items": {
            "type": "string"
          },
          "minItems": 1,
          "maxItems": 20
        },
        "force": {
          "type": "boolean",
          "description": "True deletes the index profile even when the provider cannot remove the physical index, as the force option of the Indexes page does. Use it only after a delete without it failed."
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
    public override string Description => "Deletes one or more index profiles together with their physical indexes. Check first that no saved query, the site search settings or a content picker uses the index.";

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
            return $"Nothing was deleted: {error}";
        }

        if (notFound.Count > 0)
        {
            return $"Nothing was deleted: there is no index profile with the ID or the name {string.Join(", ", notFound.Select(index => $"'{index}'"))}. Use listIndexProfiles to get them.";
        }

        var force = arguments.GetFirstValueOrDefault("force", false);
        var deleted = new List<object>();
        var failed = new List<object>();

        foreach (var indexProfile in indexProfiles)
        {
            var problem = await DeleteAsync(services, indexProfileManager, indexProfile, force);

            if (problem is null)
            {
                deleted.Add(new
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
                    error = problem,
                });
            }
        }

        return AgentToolResults.Json(new
        {
            deleted,
            failed = failed.Count > 0
                ? failed
                : null,
            note = deleted.Count > 0
                ? "The index profiles and their physical indexes cannot be restored. A saved query, the site search settings or a content picker that used one of them no longer returns results."
                : null,
        });
    }

    // The Delete action of the Indexes page, with and without its force option.
    private static async Task<string> DeleteAsync(IServiceProvider services, IIndexProfileManager indexProfileManager, IndexProfile indexProfile, bool force)
    {
        var indexManager = services.GetKeyedService<IIndexManager>(indexProfile.ProviderName);

        if (force)
        {
            if (indexManager is not null)
            {
                await indexManager.DeleteAsync(indexProfile);
            }

            await indexProfileManager.DeleteAsync(indexProfile);

            return null;
        }

        if (indexManager is null)
        {
            return $"The provider '{indexProfile.ProviderName}' has no index manager, so its physical index cannot be removed. Set force to delete the index profile anyway.";
        }

        if (await indexManager.ExistsAsync(indexProfile.IndexFullName) && !await indexManager.DeleteAsync(indexProfile))
        {
            return "The provider could not remove the physical index, so the index profile was kept. Set force to delete the index profile anyway.";
        }

        return await indexProfileManager.DeleteAsync(indexProfile)
            ? null
            : "The physical index was removed, but the index profile could not be deleted. Set force to delete it.";
    }
}
