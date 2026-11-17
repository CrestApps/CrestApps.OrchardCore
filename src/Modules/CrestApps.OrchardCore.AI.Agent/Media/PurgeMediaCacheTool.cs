using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// Purges the local cache of a remote media store, as the Purge button of the Media Cache settings page does.
/// </summary>
public sealed class PurgeMediaCacheTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "purgeMediaCache";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Purges the local cache of files fetched from remote media storage (Amazon S3 or Azure Blob); files are fetched again on the next request. Only works when a remote media store is enabled and configured.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, MediaPermissions.ManageAssetCache))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // The cache is registered only when a remote media store is enabled and its configuration is valid.
        var mediaFileStoreCache = services.GetService<IMediaFileStoreCache>();

        if (mediaFileStoreCache is null)
        {
            return "Nothing was purged: the media cache feature is enabled, but no remote media store (Amazon S3 or Azure Blob) is enabled and configured, so there is no cache.";
        }

        // The cache answers whether a file or folder could not be removed.
        if (await mediaFileStoreCache.PurgeAsync())
        {
            return "The media cache was purged, but some files could not be removed. The site's log has the details.";
        }

        return AgentToolResults.Json(new
        {
            purged = true,
            note = "Each file is fetched again from the remote media store the next time it is requested.",
        });
    }
}
