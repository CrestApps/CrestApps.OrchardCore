using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Sitemaps;
using OrchardCore.Sitemaps.Services;

namespace CrestApps.OrchardCore.AI.Agent.Sitemaps;

/// <summary>
/// Deletes a sitemap or a sitemap index, as the Delete action of the Sitemaps admin pages does.
/// </summary>
public sealed class DeleteSitemapTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteSitemap";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "sitemapId": {
          "type": "string",
          "description": "The ID of the sitemap or sitemap index to delete, from listSitemaps."
        }
      },
      "required": [
        "sitemapId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a sitemap or a sitemap index. Indexes that contain the sitemap are not updated.";

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

        if (!arguments.TryGetFirstString("sitemapId", out var sitemapId))
        {
            return "The sitemapId argument is required.";
        }

        var sitemapManager = services.GetRequiredService<ISitemapManager>();
        var sitemap = await sitemapManager.LoadSitemapAsync(sitemapId.Trim());

        if (sitemap is null)
        {
            return $"Nothing was deleted: there is no sitemap with the ID '{sitemapId}'. Use listSitemaps to get the IDs.";
        }

        await sitemapManager.DeleteSitemapAsync(sitemap.SitemapId);

        var kind = SitemapContent.GetKind(sitemap);

        return AgentToolResults.Json(new
        {
            deleted = true,
            sitemapId = sitemap.SitemapId,
            kind,
            name = sitemap.Name,
            path = sitemap.Path,
            note = kind == SitemapContent.SitemapKind
                ? "A sitemap index that contained this sitemap still lists its ID. Update the index on the Sitemap Indexes page."
                : null,
        });
    }
}
