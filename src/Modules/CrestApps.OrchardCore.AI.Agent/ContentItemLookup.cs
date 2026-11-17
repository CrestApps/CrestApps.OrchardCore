using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Finds the content item a read tool is given by its content item ID, its alias or its title, for content items that
/// are few and that people call by name: menus and taxonomies.
/// </summary>
internal static class ContentItemLookup
{
    private const int MaxListed = 100;

    /// <summary>
    /// Lists the latest versions of the content items of one content type, by title. It runs the filter text a person
    /// types on the admin Content Items page through that page's parser and query service, so the caller gets what the
    /// page lists for them.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="contentType">The technical name of the content type.</param>
    /// <returns>The content items, at most 100.</returns>
    public static async Task<List<ContentItem>> ListLatestAsync(IServiceProvider services, string contentType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        // The list of the admin Content Items page is part of the Contents feature.
        var filterParser = services.GetService<IContentsAdminListFilterParser>();
        var queryService = services.GetService<IContentsAdminListQueryService>();

        // The 'type' filter lists every content type when it is given one that does not exist.
        if (filterParser is null ||
            queryService is null ||
            await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(contentType) is null)
        {
            return [];
        }

        var options = new ContentOptionsViewModel
        {
            FilterResult = filterParser.Parse($"type:{contentType} status:{ContentsStatus.Latest} sort:{ContentsOrder.Title}"),
        };

        options.FilterResult.MapTo(options);

        var query = await queryService.QueryAsync(options, new NullModelUpdater());
        var contentItems = await query
            .Take(MaxListed)
            .ListAsync(services.GetRequiredService<IContentManager>());

        return contentItems
            .Where(contentItem => string.Equals(contentItem.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Finds the latest version of the one content item that a value names: a content item ID, an alias, or the title of
    /// a content item of the given content type.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="contentType">The technical name of the content type a title is looked up in.</param>
    /// <param name="value">The content item ID, the alias or the title.</param>
    /// <returns>The content item, or the sentence that says why none or more than one was found.</returns>
    public static async Task<(ContentItem ContentItem, string Error)> FindAsync(IServiceProvider services, string contentType, string value)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(contentType);
        ArgumentException.ThrowIfNullOrEmpty(value);

        value = value.Trim();

        var contentManager = services.GetRequiredService<IContentManager>();
        var contentItem = await contentManager.GetAsync(value, VersionOptions.Latest);

        if (contentItem is not null)
        {
            return (contentItem, null);
        }

        // The alias handles are there when the Alias feature is on.
        var handleManager = services.GetService<IContentHandleManager>();
        var contentItemId = handleManager is null || value.Contains(':')
            ? null
            : await handleManager.GetContentItemIdAsync($"alias:{value}");

        if (!string.IsNullOrEmpty(contentItemId))
        {
            contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

            if (contentItem is not null)
            {
                return (contentItem, null);
            }
        }

        var candidates = await ListLatestAsync(services, contentType);
        var matches = candidates
            .Where(candidate => string.Equals(candidate.DisplayText?.Trim(), value, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1)
        {
            return (matches[0], null);
        }

        if (matches.Count > 1)
        {
            return (null, $"More than one {contentType} content item has the title '{value}': {Describe(matches)}. Give the content item ID of one of them.");
        }

        return candidates.Count == 0
            ? (null, $"No content item was found for '{value}', and there are no {contentType} content items you can see.")
            : (null, $"No content item was found for '{value}'. The {contentType} content items are: {Describe(candidates)}.");
    }

    private static string Describe(IEnumerable<ContentItem> contentItems)
        => string.Join("; ", contentItems.Select(contentItem => $"'{contentItem.DisplayText}' ({contentItem.ContentItemId})"));
}
