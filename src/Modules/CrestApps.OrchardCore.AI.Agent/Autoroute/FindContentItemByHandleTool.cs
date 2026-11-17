using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Contents;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.Autoroute;

/// <summary>
/// Finds the content item behind a URL path, an alias or a content handle, through the content handle manager.
/// </summary>
public sealed class FindContentItemByHandleTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "findContentItemByHandle";

    private const string SlugPrefix = "slug:";

    private const string AliasPrefix = "alias:";

    private const string ContentItemIdPrefix = "contentitemid:";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "handle": {
          "type": "string",
          "description": "A URL or path (https://example.com/about-us, /about-us), an alias (main-menu), or a handle with its prefix: slug:about-us, alias:main-menu, contentitemid:4abc. A path of / returns the home page."
        }
      },
      "required": [
        "handle"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Finds the content item behind a URL path, an alias or a handle (slug:about-us, alias:main-menu, contentitemid:...), and returns its ID, type, title and state. A path of / returns the current home page. Use it when the user names a page by its address.";

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

        if (!arguments.TryGetFirstString("handle", out var handle))
        {
            return "The handle argument is required.";
        }

        handle = handle.Trim();

        var path = GetPath(services, handle);
        string contentItemId = null;
        string matchedBy = null;
        string jsonPath = null;

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

        var contentManager = services.GetRequiredService<IContentManager>();
        var published = string.IsNullOrEmpty(contentItemId)
            ? null
            : await contentManager.GetAsync(contentItemId, VersionOptions.Published);
        var latest = string.IsNullOrEmpty(contentItemId)
            ? null
            : await contentManager.GetAsync(contentItemId, VersionOptions.Latest);
        var contentItem = published ?? latest;

        if (contentItem is null)
        {
            return AgentToolResults.Json(new
            {
                found = false,
                handle,
                note = "No content item was found for this handle. A path is matched by the Autoroute feature and an alias by the Alias feature; a content item that was never published has no path yet.",
            });
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.ViewContent, contentItem))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        return AgentToolResults.Json(new
        {
            found = true,
            handle,
            matchedBy,
            contentItemId = contentItem.ContentItemId,
            contentType = contentItem.ContentType,
            displayText = contentItem.DisplayText,
            published = published is not null,
            hasDraft = latest is not null && !latest.Published,
            containedItemPath = jsonPath,
            note = jsonPath is null
                ? null
                : "The home page is an item contained in this content item, at containedItemPath.",
        });
    }

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
