using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// Lists content items with the filters of the admin Content Items page. It builds the same filter text a person types
/// in that page's search box and runs it through the same parser and query service.
/// </summary>
public sealed class ListContentItemsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listContentItems";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "text": {
          "type": "string",
          "description": "Text to find in the title of the items, as typed in the search box of the admin page: words, AND, OR and NOT, and a phrase in double quotes that is matched whole. Leave it out to list every item."
        },
        "contentTypes": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "The technical names of the content types to list, for example BlogPost. Leave it out to list every listable content type."
        },
        "stereotype": {
          "type": "string",
          "description": "List the content types of this stereotype. As on the admin page, only listable content types are returned. Ignored when contentTypes is given."
        },
        "status": {
          "type": "string",
          "enum": [
            "latest",
            "published",
            "draft",
            "ownedByMe",
            "allVersions"
          ],
          "description": "Which versions to list: latest (default), published, draft (latest versions that are not published), ownedByMe (latest versions the caller owns) or allVersions (lists the same items as latest, as the admin page does)."
        },
        "sort": {
          "type": "string",
          "enum": [
            "modified",
            "published",
            "created",
            "title"
          ],
          "description": "The order of the list: most recently modified (default), most recently published, most recently created, or by title."
        },
        "culture": {
          "type": "string",
          "description": "List only the items of this culture, for example fr or en-US. Needs the Content Localization feature."
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
          "description": "The number of items per page. The default is 20 and the maximum is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists content items with the same filters as the admin Content Items page: text, content types, stereotype, status (latest, published, draft, owned by me, all versions), sort, culture, and paging. Use it to find items and their IDs; it returns summaries, not full bodies. The caller sees only the content types they may view.";

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
        var user = AgentToolAuthorization.GetCaller(services);

        if (user is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var typeDefinitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

        // The check of the admin Content Items page. The 'type' filter then keeps the content types the caller may view.
        if (!await authorizationService.AuthorizeContentTypeDefinitionsAsync(user, CommonPermissions.ListContent, typeDefinitions, contentManager))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // Each entry is one term of the filter text, with the name the parser gives it.
        var terms = new List<(string Name, string Text)>();

        if (arguments.TryGetFirst<string[]>("contentTypes", out var contentTypes) && contentTypes is { Length: > 0 })
        {
            var names = new List<string>();

            foreach (var contentType in contentTypes.Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                var definition = typeDefinitions.FirstOrDefault(type => string.Equals(type.Name, contentType.Trim(), StringComparison.OrdinalIgnoreCase));

                if (definition is null)
                {
                    return $"Nothing was listed: there is no content type named '{contentType}'. Use the technical name of a content type.";
                }

                names.Add(definition.Name);
            }

            if (names.Count > 0)
            {
                terms.Add(("type", $"type:{string.Join(',', names)}"));
            }
        }
        else if (arguments.TryGetFirstString("stereotype", out var stereotype) && !string.IsNullOrWhiteSpace(stereotype))
        {
            if (!IsFilterValue(stereotype))
            {
                return "Nothing was listed: the stereotype must be one word, for example Widget.";
            }

            terms.Add(("stereotype", $"stereotype:{stereotype.Trim()}"));
        }

        if (arguments.TryGetFirstString("status", out var status))
        {
            var contentsStatus = status.Trim().ToUpperInvariant() switch
            {
                "LATEST" => ContentsStatus.Latest,
                "PUBLISHED" => ContentsStatus.Published,
                "DRAFT" => ContentsStatus.Draft,
                "OWNEDBYME" => ContentsStatus.Owner,
                "ALLVERSIONS" => ContentsStatus.AllVersions,
                _ => (ContentsStatus?)null,
            };

            if (contentsStatus is null)
            {
                return "Nothing was listed: status must be latest, published, draft, ownedByMe or allVersions.";
            }

            terms.Add(("status", $"status:{contentsStatus}"));
        }

        if (arguments.TryGetFirstString("sort", out var sort))
        {
            var contentsOrder = sort.Trim().ToUpperInvariant() switch
            {
                "MODIFIED" => ContentsOrder.Modified,
                "PUBLISHED" => ContentsOrder.Published,
                "CREATED" => ContentsOrder.Created,
                "TITLE" => ContentsOrder.Title,
                _ => (ContentsOrder?)null,
            };

            if (contentsOrder is null)
            {
                return "Nothing was listed: sort must be modified, published, created or title.";
            }

            terms.Add(("sort", $"sort:{contentsOrder}"));
        }

        if (arguments.TryGetFirstString("culture", out var culture) && !string.IsNullOrWhiteSpace(culture))
        {
            // The 'culture' filter is added by the Content Localization feature; without it the term would be read as text.
            if (services.GetService<IContentLocalizationManager>() is null)
            {
                return "Nothing was listed: the culture filter needs the Content Localization feature, which is not enabled.";
            }

            if (!IsFilterValue(culture))
            {
                return "Nothing was listed: the culture must be a culture code, for example fr or en-US.";
            }

            terms.Add(("culture", $"culture:{culture.Trim()}"));
        }

        // The text goes last: the parser stops at what it cannot read, and the filters before it must still apply.
        if (arguments.TryGetFirstString("text", out var text) && ToSearchText(text) is { } searchText)
        {
            terms.Add((ContentsAdminListFilterOptions.DefaultTermName, searchText));
        }

        var filterText = string.Join(' ', terms.Select(term => term.Text));
        var options = new ContentOptionsViewModel
        {
            FilterResult = services.GetRequiredService<IContentsAdminListFilterParser>().Parse(filterText),
        };

        // A term the parser did not keep would be a filter that silently does not apply.
        if (terms.Any(term => !options.FilterResult.Any(node => string.Equals(node.TermName, term.Name, StringComparison.OrdinalIgnoreCase))))
        {
            return "Nothing was listed: the filter could not be read. Use plain words in text, or put a phrase in double quotes.";
        }

        options.FilterResult.MapTo(options);

        var query = await services.GetRequiredService<IContentsAdminListQueryService>().QueryAsync(options, new NullModelUpdater());
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var startIndex = (page - 1) * pageSize;

        // As on the admin page, the items are not counted when the site sets a maximum paged count.
        var maxPagedCount = services.GetRequiredService<IOptions<PagerOptions>>().Value.MaxPagedCount;
        var total = maxPagedCount > 0
            ? maxPagedCount
            : await query.CountAsync(cancellationToken);

        var contentItems = await query
            .Skip(startIndex)
            .Take(pageSize)
            .ListAsync(contentManager);

        var localClock = services.GetRequiredService<ILocalClock>();
        var items = new List<ContentItemSummary>();

        foreach (var contentItem in contentItems)
        {
            items.Add(await ContentItemSummary.CreateAsync(contentItem, localClock));
        }

        var totalIsCapped = maxPagedCount > 0;

        if (totalIsCapped && items.Count < pageSize)
        {
            total = Math.Min(startIndex + items.Count, maxPagedCount);
            totalIsCapped = false;
        }

        return AgentToolResults.Json(new
        {
            items,
            total,
            totalIsCapped = totalIsCapped ? true : (bool?)null,
            page,
            pageSize,
            filter = filterText,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        });
    }

    private static bool IsFilterValue(string value)
        => !value.Trim().Any(character => char.IsWhiteSpace(character) || character is '"' or '\'' or ':' or ',');

    /// <summary>
    /// Makes the search text safe for the filter parser. A colon reads as a named filter and an open quote never closes;
    /// either makes the parser drop what follows, so such a text is searched as one phrase.
    /// </summary>
    private static string ToSearchText(string text)
    {
        text = text?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (!text.Contains(':') && text.Count(character => character == '"') % 2 == 0)
        {
            return text;
        }

        var phrase = text.Replace("\"", string.Empty, StringComparison.Ordinal).Trim();

        return phrase.Length == 0
            ? null
            : $"\"{phrase}\"";
    }
}
