using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Sitemaps;
using OrchardCore.Sitemaps.Cache;

namespace CrestApps.OrchardCore.AI.Agent.Sitemaps;

/// <summary>
/// Clears the cached sitemap files, one file or all of them, as the Sitemaps Cache admin page does.
/// </summary>
public sealed class PurgeSitemapCacheTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "purgeSitemapCache";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "cacheFileName": {
          "type": "string",
          "description": "The name of one cached sitemap file to clear, from the cachedFiles of listSitemaps. Leave it out to clear every cached sitemap file."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Clears the cached sitemap files, one file or all of them, so sitemaps are rebuilt on the next request. Use it after changing sitemaps or publishing many items.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, SitemapsPermissions.ManageSitemaps))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var cacheProvider = services.GetRequiredService<ISitemapCacheProvider>();
        var cachedFiles = (await cacheProvider.ListAsync()).ToList();

        if (!arguments.TryGetFirstString("cacheFileName", out var cacheFileName))
        {
            // The provider answers whether clearing a file failed.
            if (await cacheProvider.PurgeAllAsync())
            {
                return "The sitemap cache was cleared, but some files could not be removed. Call listSitemaps to see the cached files that are left.";
            }

            return AgentToolResults.Json(new
            {
                purged = true,
                all = true,
                purgedFiles = cachedFiles.Count,
                note = "Each sitemap is built again the next time it is requested.",
            });
        }

        // Only a file the cache lists is cleared, so a path outside the cache is never passed on.
        var fileName = cachedFiles.FirstOrDefault(name => string.Equals(name, cacheFileName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (fileName is null)
        {
            return $"Nothing was cleared: the sitemap cache has no file named '{cacheFileName}'. Use listSitemaps to see the cached files.";
        }

        if (await cacheProvider.PurgeAsync(fileName))
        {
            return $"The cached sitemap file '{fileName}' could not be removed.";
        }

        return AgentToolResults.Json(new
        {
            purged = true,
            all = false,
            cacheFileName = fileName,
            note = "The sitemap is built again the next time it is requested.",
        });
    }
}
