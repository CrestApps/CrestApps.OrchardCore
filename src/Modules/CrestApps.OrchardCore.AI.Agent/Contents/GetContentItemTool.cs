using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Autoroute;
using CrestApps.OrchardCore.AI.Agent.Flows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Contents;
using OrchardCore.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// Gets one content item by its ID or by a URL, a path, an alias or a content handle, and on request its translations
/// and the outline of the items nested in its Flow, Bag and widgets list parts.
/// </summary>
internal sealed class GetContentItemTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getContentItem";

    /// <summary>
    /// The include option that adds the translations of the content item.
    /// </summary>
    public const string LocalizationsInclude = "localizations";

    /// <summary>
    /// The include option that adds the outline of the nested content items.
    /// </summary>
    public const string NestedInclude = "nested";

    private const int MaxNestedItems = 200;

    private const string LatestVersion = "latest";

    private const string PublishedVersion = "published";

    private const string SlugPrefix = "slug:";

    private const string AliasPrefix = "alias:";

    private const string ContentItemIdPrefix = "contentitemid:";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item (ContentItemId). Give this or handle."
        },
        "handle": {
          "type": "string",
          "description": "Instead of contentItemId: a URL or path (https://example.com/about-us, /about-us), an alias (main-menu), or a handle with its prefix: slug:about-us, alias:main-menu, contentitemid:4abc. A path of / returns the home page."
        },
        "version": {
          "type": "string",
          "enum": [ "latest", "published" ],
          "description": "Which version to read: latest (the default; a draft when there is one) or published."
        },
        "include": {
          "type": "array",
          "items": {
            "type": "string",
            "enum": [ "localizations", "nested" ]
          },
          "description": "What to add to the result: localizations (the item's translation in each site culture, or that it is missing) and nested (the outline of the widgets and items in its Flow, Bag and widget-zone parts, with their IDs and positions)."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gets one content item by its ID or by a URL, path, alias or handle (slug:about-us, alias:main-menu), and returns its ID, type, title and state; a path of / returns the home page. Add include localizations for its translation in each site culture, and nested for the outline of the widgets and items in its Flow, Bag and widget-zone parts.";

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

        var hasId = arguments.TryGetFirstString("contentItemId", out var contentItemId);
        var hasHandle = arguments.TryGetFirstString("handle", out var handle);

        if (hasId == hasHandle)
        {
            return "Give contentItemId or handle (a URL, a path, an alias or a handle such as slug:about-us), but not both.";
        }

        var version = arguments.TryGetFirstString("version", out var versionText)
            ? versionText.Trim().ToLowerInvariant()
            : LatestVersion;

        if (version is not LatestVersion and not PublishedVersion)
        {
            return $"'{versionText}' is not a version: use latest or published.";
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "include", out var includeValues))
        {
            return "include must be a list: localizations, nested or both.";
        }

        var include = (includeValues ?? []).Select(value => value.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var unknown = include.Where(value => value is not LocalizationsInclude and not NestedInclude).ToList();

        if (unknown.Count > 0)
        {
            return $"These are not include options: {string.Join(", ", unknown)}. Use localizations, nested or both.";
        }

        string matchedBy = null;
        string jsonPath = null;

        if (hasHandle)
        {
            handle = handle.Trim();

            var path = GetPath(services, handle);

            if (path == "/")
            {
                var site = await services.GetRequiredService<ISiteService>().GetSiteSettingsAsync();
                var options = services.GetRequiredService<IOptions<AutorouteOptions>>().Value;

                contentItemId = HomeRouteContent.GetContentItemId(site, options);
                jsonPath = HomeRouteContent.GetJsonPath(site, options);
                matchedBy = "homepage";

                if (contentItemId is null)
                {
                    return AgentToolResults.Json(new
                    {
                        found = false,
                        handle,
                        homeRoute = HomeRouteContent.Describe(site),
                        note = "The home page of the site is not a content item. It is the route in homeRoute, or the site has no home route.",
                    });
                }
            }
            else
            {
                var handleManager = services.GetRequiredService<IContentHandleManager>();

                contentItemId = null;

                foreach (var candidate in GetHandles(handle, path))
                {
                    contentItemId = await handleManager.GetContentItemIdAsync(candidate);

                    if (!string.IsNullOrEmpty(contentItemId))
                    {
                        matchedBy = candidate;

                        break;
                    }
                }
            }
        }
        else
        {
            contentItemId = contentItemId.Trim();
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var published = string.IsNullOrEmpty(contentItemId)
            ? null
            : await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        var latest = string.IsNullOrEmpty(contentItemId)
            ? null
            : await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (latest is null && published is null)
        {
            return AgentToolResults.Json(new
            {
                found = false,
                contentItemId = hasId ? contentItemId : null,
                handle = hasHandle ? handle : null,
                note = hasHandle
                    ? "No content item was found for this handle. A path is matched by the Autoroute feature and an alias by the Alias feature; a content item that was never published has no path yet."
                    : "No content item has this ID. Use listContentItems to find it.",
            });
        }

        var contentItem = version == PublishedVersion
            ? published
            : latest ?? published;

        if (contentItem is null)
        {
            // Only checked against the latest version, so a refused caller learns nothing about the item.
            if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, latest))
            {
                return AIToolCallerAuthorization.NotAllowedMessage;
            }

            return $"Content item '{latest.ContentItemId}' has never been published, or its published version was removed. Ask for the latest version instead.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, contentItem))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        return AgentToolResults.Json(new
        {
            found = true,
            handle = hasHandle ? handle : null,
            matchedBy,
            contentItemId = contentItem.ContentItemId,
            contentItemVersionId = contentItem.ContentItemVersionId,
            version,
            contentType = contentItem.ContentType,
            displayText = contentItem.DisplayText,
            published = published is not null,
            hasDraft = latest is not null && !latest.Published,
            containedItemPath = jsonPath,
            note = jsonPath is null
                ? null
                : "The home page is an item contained in this content item, at containedItemPath.",
            localizations = include.Contains(LocalizationsInclude)
                ? await GetLocalizationsAsync(services, contentItem)
                : null,
            nested = include.Contains(NestedInclude)
                ? await GetNestedAsync(services, contentItem)
                : null,
        });
    }

    /// <summary>
    /// Lists the translations of a content item, one entry for each culture of the site. The content localization
    /// manager is there only when the Content Localization feature is on.
    /// </summary>
    private static async Task<object> GetLocalizationsAsync(IServiceProvider services, ContentItem contentItem)
    {
        var contentLocalizationManager = services.GetService<IContentLocalizationManager>();
        var localizationService = services.GetService<ILocalizationService>();

        if (contentLocalizationManager is null || localizationService is null)
        {
            return new
            {
                available = false,
                note = "Translations are not available on this site: the Content Localization feature is off.",
            };
        }

        if (!contentItem.TryGet<LocalizationPart>(out var localizationPart))
        {
            return new
            {
                available = false,
                note = $"The content type '{contentItem.ContentType}' has no localization part, so the content item has no translations.",
            };
        }

        var supportedCultures = await localizationService.GetSupportedCulturesAsync();

        // The set holds the published and the latest version of each translation.
        var versions = string.IsNullOrEmpty(localizationPart.LocalizationSet)
            ? [contentItem]
            : (await contentLocalizationManager.GetItemsForSetAsync(localizationPart.LocalizationSet)).ToList();

        var localizations = versions
            .GroupBy(item => item.ContentItemId)
            .Select(group => new
            {
                Item = group.FirstOrDefault(item => item.Latest) ?? group.First(),
                Published = group.Any(item => item.Published),
            })
            .ToList();

        var items = new List<object>();

        foreach (var culture in supportedCultures)
        {
            var match = localizations.FirstOrDefault(entry => string.Equals(GetCulture(entry.Item), culture, StringComparison.OrdinalIgnoreCase));
            var localization = match?.Item;

            if (localization is null)
            {
                items.Add(new
                {
                    culture,
                    exists = false,
                });

                continue;
            }

            // A translation the caller may not view is reported as existing, without its details.
            var isRequestedItem = localization.ContentItemId == contentItem.ContentItemId;
            var canView = isRequestedItem
                || await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, localization);

            items.Add(new
            {
                culture,
                exists = true,
                contentItemId = canView ? localization.ContentItemId : null,
                displayText = canView ? localization.DisplayText : null,
                published = canView ? match.Published : (bool?)null,
                hasDraft = canView ? !localization.Published : (bool?)null,
                isRequestedItem = isRequestedItem ? true : (bool?)null,
            });
        }

        return new
        {
            available = true,
            culture = localizationPart.Culture,
            localizationSet = localizationPart.LocalizationSet,
            defaultCulture = await localizationService.GetDefaultCultureAsync(),
            items,
            total = items.Count,
        };
    }

    /// <summary>
    /// Gets the outline of the items nested in the Flow, Bag and widgets list parts of a content item. The nested items
    /// are stored in the item's JSON, so this needs no service of the Flows or Widgets features.
    /// </summary>
    private static async Task<object> GetNestedAsync(IServiceProvider services, ContentItem contentItem)
    {
        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var root = NestedContentItemTree.ToJson(services, contentItem);
        var entries = await NestedContentItemTree.GetEntriesAsync(contentDefinitionManager, root);
        var containers = await NestedContentItemTree.GetContainersAsync(contentDefinitionManager, root, null, null, 1, true);

        return new
        {
            parts = containers.Select(container => new
            {
                part = container.PartName,
                kind = container.Kind,
                zone = container.Zone,
                count = container.Items.Count,
            }),
            items = entries.Take(MaxNestedItems).Select(entry => new
            {
                nestedContentItemId = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.ContentItemId)),
                contentType = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.ContentType)),
                displayText = NestedContentItemTree.GetString(entry.Item, nameof(ContentItem.DisplayText)),
                part = entry.Container.PartName,
                kind = entry.Container.Kind,
                zone = entry.Container.Zone,
                position = entry.Position,
                depth = entry.Container.Depth,
                parentNestedContentItemId = entry.Container.OwnerId,
            }),
            total = entries.Count,
            truncated = entries.Count > MaxNestedItems ? true : (bool?)null,
            note = containers.Count == 0
                ? $"The content type '{contentItem.ContentType}' has no Flow, Bag or widget-zone part."
                : null,
        };
    }

    private static string GetCulture(ContentItem contentItem)
        => contentItem.TryGet<LocalizationPart>(out var part) ? part.Culture : null;

    /// <summary>
    /// Gets the path of a URL or a path, without the address of the site, the base path of the tenant, the query and the
    /// fragment. A handle that is neither gives <see langword="null"/>.
    /// </summary>
    private static string GetPath(IServiceProvider services, string handle)
    {
        string path;

        if (handle.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || handle.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(handle, UriKind.Absolute, out var uri))
            {
                return null;
            }

            path = Uri.UnescapeDataString(uri.AbsolutePath);
        }
        else if (handle.StartsWith("~/", StringComparison.Ordinal))
        {
            path = handle.Substring(1);
        }
        else if (handle.StartsWith('/'))
        {
            path = handle;
        }
        else
        {
            return null;
        }

        var end = path.IndexOfAny(['?', '#']);

        if (end >= 0)
        {
            path = path.Substring(0, end);
        }

        var pathBase = services.GetService<IHttpContextAccessor>()?.HttpContext?.Request.PathBase ?? PathString.Empty;

        if (pathBase.HasValue && new PathString(path).StartsWithSegments(pathBase, StringComparison.OrdinalIgnoreCase, out var remaining))
        {
            path = remaining.Value;
        }

        path = path.TrimEnd('/');

        return path.Length == 0
            ? "/"
            : path;
    }

    /// <summary>
    /// Gets the handles to try, in order. A path is a slug. A handle with a prefix is tried as it is. A bare word is
    /// tried as a slug, then as an alias, then as a content item ID.
    /// </summary>
    private static IEnumerable<string> GetHandles(string handle, string path)
    {
        if (path is not null)
        {
            return [$"{SlugPrefix}{path}"];
        }

        if (handle.Contains(':'))
        {
            return [handle];
        }

        return [$"{SlugPrefix}{handle}", $"{AliasPrefix}{handle}", $"{ContentItemIdPrefix}{handle}"];
    }
}
