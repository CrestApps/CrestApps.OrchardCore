using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// Compares two stored versions of one content item field by field, as the audit trail's diff compares two snapshots of
/// an item: both versions are serialized the same way and every value that was added, removed or changed is listed
/// with its before and after. It pairs with <c>restoreContentItemVersion</c>.
/// </summary>
public sealed class CompareContentItemVersionsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "compareContentItemVersions";

    /// <summary>
    /// The most changes one result lists.
    /// </summary>
    public const int MaxChanges = 100;

    /// <summary>
    /// The most characters of a value a result shows.
    /// </summary>
    public const int MaxValueLength = 500;

    // The properties that tell versions apart rather than describe the content; the result reports them per version.
    private static readonly HashSet<string> _versionProperties = new(StringComparer.Ordinal)
    {
        nameof(ContentItem.Id),
        nameof(ContentItem.ContentItemVersionId),
        nameof(ContentItem.Latest),
        nameof(ContentItem.Published),
        nameof(ContentItem.ModifiedUtc),
        nameof(ContentItem.PublishedUtc),
        nameof(ContentItem.CreatedUtc),
        "AuditTrailPart",
    };

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item."
        },
        "fromVersionId": {
          "type": "string",
          "description": "The ID of the earlier version (ContentItemVersionId), from listContentItemVersions. Defaults to the published version when the item has a newer draft, otherwise to the version before toVersionId."
        },
        "toVersionId": {
          "type": "string",
          "description": "The ID of the later version, from listContentItemVersions. Defaults to the latest version."
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
    public override string Description => "Shows what changed between two versions of a content item, field by field, with each value before and after; by default the draft against the published version, or the latest version against the one before it. Use it for \"what changed on this page?\" and before restoreContentItemVersion. Needs the right to edit the item.";

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

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId) || string.IsNullOrWhiteSpace(contentItemId))
        {
            return "Give contentItemId, the ID of the content item. Use listContentItems to find it.";
        }

        contentItemId = contentItemId.Trim();

        var contentManager = services.GetRequiredService<IContentManager>();

        // Each version is its own document; a higher document ID is a newer version.
        var versions = (await contentManager.GetAllVersionsAsync(contentItemId))
            .OrderByDescending(version => version.Id)
            .ToList();

        if (versions.Count == 0)
        {
            return $"No content item with the ID '{contentItemId}' was found. Use listContentItems to find it.";
        }

        var latest = versions.FirstOrDefault(version => version.Latest)
            ?? versions.FirstOrDefault(version => version.Published)
            ?? versions[0];

        // Earlier versions and drafts are not public: seeing them is seeing what the editor of the item sees.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.EditContent, latest))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        ContentItem to;

        if (arguments.TryGetFirstString("toVersionId", out var toVersionId) && !string.IsNullOrWhiteSpace(toVersionId))
        {
            to = FindVersion(versions, toVersionId);

            if (to is null)
            {
                return $"Content item '{contentItemId}' has no version '{toVersionId.Trim()}'. Use listContentItemVersions to get its version IDs.";
            }
        }
        else
        {
            to = latest;
        }

        ContentItem from;

        if (arguments.TryGetFirstString("fromVersionId", out var fromVersionId) && !string.IsNullOrWhiteSpace(fromVersionId))
        {
            from = FindVersion(versions, fromVersionId);

            if (from is null)
            {
                return $"Content item '{contentItemId}' has no version '{fromVersionId.Trim()}'. Use listContentItemVersions to get its version IDs.";
            }
        }
        else
        {
            var published = versions.FirstOrDefault(version => version.Published);

            // A draft is compared with what is live; anything else with the version saved before it.
            from = to.Latest && !to.Published && published is not null && published.Id != to.Id
                ? published
                : versions.FirstOrDefault(version => version.Id < to.Id);

            if (from is null)
            {
                return $"Version '{to.ContentItemVersionId}' is the first version of content item '{contentItemId}', so there is nothing earlier to compare it with.";
            }
        }

        if (from.Id == to.Id)
        {
            return "Give two different versions: a version compared with itself has no changes.";
        }

        var before = Flatten(JObject.FromObject(from));
        var after = Flatten(JObject.FromObject(to));
        var changes = new List<object>();
        var count = 0;

        foreach (var path in before.Keys.Union(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var hasBefore = before.TryGetValue(path, out var oldValue);
            var hasAfter = after.TryGetValue(path, out var newValue);

            if (hasBefore && hasAfter && string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                continue;
            }

            count++;

            if (changes.Count >= MaxChanges)
            {
                continue;
            }

            changes.Add(new
            {
                path,
                change = !hasBefore ? "added" : !hasAfter ? "removed" : "changed",
                before = hasBefore ? Shorten(oldValue) : null,
                after = hasAfter ? Shorten(newValue) : null,
            });
        }

        var localClock = services.GetRequiredService<ILocalClock>();

        return AgentToolResults.Json(new
        {
            contentItemId,
            contentType = to.ContentType,
            from = await DescribeAsync(from, localClock),
            to = await DescribeAsync(to, localClock),
            changes,
            changeCount = count,
            truncated = count > changes.Count,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            note = count == 0
                ? "The two versions have the same content."
                : "before and after are the item's own values, given as data, cut at " + MaxValueLength + " characters. To bring the earlier version back as a draft, call restoreContentItemVersion with from.contentItemVersionId.",
        });
    }

    private static ContentItem FindVersion(List<ContentItem> versions, string versionId)
        => versions.FirstOrDefault(version => string.Equals(version.ContentItemVersionId, versionId.Trim(), StringComparison.Ordinal));

    private static async Task<object> DescribeAsync(ContentItem version, ILocalClock localClock)
        => new
        {
            contentItemVersionId = version.ContentItemVersionId,
            displayText = version.DisplayText,
            latest = version.Latest,
            published = version.Published,
            author = version.Author,
            modified = await AgentToolResults.ToLocalAsync(localClock, version.ModifiedUtc),
            publishedAt = await AgentToolResults.ToLocalAsync(localClock, version.PublishedUtc),
        };

    // Every value of the item by its path, such as "TitlePart.Title" or "BagPart.ContentItems[0].HtmlBodyPart.Html".
    private static Dictionary<string, string> Flatten(JsonObject item)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in item)
        {
            if (!_versionProperties.Contains(property.Key))
            {
                Add(property.Key, property.Value, values);
            }
        }

        return values;
    }

    private static void Add(string path, JsonNode node, Dictionary<string, string> values)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                if (jsonObject.Count == 0)
                {
                    values[path] = "{}";

                    break;
                }

                foreach (var property in jsonObject)
                {
                    Add($"{path}.{property.Key}", property.Value, values);
                }

                break;

            case JsonArray array:
                if (array.Count == 0)
                {
                    values[path] = "[]";

                    break;
                }

                for (var index = 0; index < array.Count; index++)
                {
                    Add($"{path}[{index}]", array[index], values);
                }

                break;

            case JsonValue value:
                values[path] = value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();
                break;

            default:
                values[path] = null;
                break;
        }
    }

    private static string Shorten(string value)
        => value is { Length: > MaxValueLength } ? string.Concat(value.AsSpan(0, MaxValueLength), "…") : value;
}
