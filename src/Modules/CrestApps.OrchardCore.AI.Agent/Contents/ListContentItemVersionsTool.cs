using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// Lists the stored versions of one content item, newest first.
/// </summary>
public sealed class ListContentItemVersionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listContentItemVersions";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item (ContentItemId)."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of versions per page. The default is 20 and the maximum is 50."
        }
      },
      "required": [
        "contentItemId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the stored versions of one content item, newest first (version ID, created/modified/published dates, author, latest and published flags). Use it before restoring a version with restoreContentItemVersion. A deleted item still lists its versions until they are pruned.";

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

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "The contentItemId argument is required.";
        }

        var contentManager = services.GetRequiredService<IContentManager>();

        // Each version is its own document; a higher document ID is a newer version.
        var versions = (await contentManager.GetAllVersionsAsync(contentItemId))
            .OrderByDescending(version => version.Id)
            .ToList();

        if (versions.Count == 0)
        {
            return $"No content item with the ID '{contentItemId}' was found.";
        }

        var current = versions.FirstOrDefault(version => version.Latest)
            ?? versions.FirstOrDefault(version => version.Published)
            ?? versions[0];

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, current))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var localClock = services.GetRequiredService<ILocalClock>();
        var items = new List<object>();

        foreach (var version in versions.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(new
            {
                contentItemVersionId = version.ContentItemVersionId,
                displayText = version.DisplayText,
                latest = version.Latest,
                published = version.Published,
                author = version.Author,
                created = await AgentToolResults.ToLocalAsync(localClock, version.CreatedUtc),
                modified = await AgentToolResults.ToLocalAsync(localClock, version.ModifiedUtc),
                publishedAt = await AgentToolResults.ToLocalAsync(localClock, version.PublishedUtc),
            });
        }

        return AgentToolResults.Json(new
        {
            contentItemId = current.ContentItemId,
            contentType = current.ContentType,
            items,
            total = versions.Count,
            page,
            pageSize,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        });
    }
}
