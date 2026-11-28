using CrestApps.OrchardCore.ContentFields.Taxonomies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Markdown.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.KnowledgeBase.AI;

/// <summary>
/// What the Knowledge Base AI tools return of an article: its link, its category's name, its body and the passage of the
/// body that matches a search.
/// </summary>
internal static class KnowledgeArticleProjection
{
    /// <summary>
    /// The most characters of an article's body a search returns for each article.
    /// </summary>
    public const int PassageLength = 400;

    /// <summary>
    /// What goes between the names of a category's path, as the help center shows it.
    /// </summary>
    public const string PathSeparator = " › ";

    // How far a passage's ends move to fall between words.
    private const int WordSnap = 40;

    /// <summary>
    /// Gets what an article's link starts with: the site's base URL (which holds the tenant's prefix) for a link that
    /// works anywhere, a customer's reply included; otherwise the tenant's prefix. It does not read the request, which a
    /// chat hub call may not have.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The start of the links, without a trailing slash; empty for the site's root.</returns>
    public static async Task<string> GetLinkBaseAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.GetService<ISiteService>() is { } siteService
            && await siteService.GetSiteSettingsAsync() is { } site
            && !string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return site.BaseUrl.Trim().TrimEnd('/');
        }

        var prefix = services.GetService<ShellSettings>()?.RequestUrlPrefix?.Trim('/');

        return string.IsNullOrEmpty(prefix) ? string.Empty : "/" + prefix;
    }

    /// <summary>
    /// Gets an article's link: its path (<c>help/{slug}</c>), as the help center's suggestions link it, or the content
    /// item's display page when it has none.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="linkBase">The start of the links, from <see cref="GetLinkBaseAsync(IServiceProvider)"/>.</param>
    /// <param name="article">The article.</param>
    /// <returns>The link, or <see langword="null"/> when it can't be built.</returns>
    public static string GetLink(IServiceProvider services, string linkBase, ContentItem article)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(article);

        var path = article.TryGet<AutoroutePart>(out var route) && !string.IsNullOrWhiteSpace(route.Path)
            ? "/" + route.Path.Trim().TrimStart('/')
            : services.GetService<LinkGenerator>()?.GetPathByRouteValues(
                routeName: null,
                values: new RouteValueDictionary
                {
                    ["area"] = "OrchardCore.Contents",
                    ["controller"] = "Item",
                    ["action"] = "Display",
                    ["contentItemId"] = article.ContentItemId,
                },
                pathBase: PathString.Empty);

        return string.IsNullOrEmpty(path) ? null : linkBase + path;
    }

    /// <summary>
    /// Gets an article's category: its term's id and its name with the categories above it.
    /// </summary>
    /// <param name="tree">The categories.</param>
    /// <param name="categoryId">The term's id, or <see langword="null"/>.</param>
    /// <returns>The category, or <see langword="null"/> when the article has none.</returns>
    public static object GetCategory(TaxonomyTermTree tree, string categoryId)
        => string.IsNullOrEmpty(categoryId)
            ? null
            : new
            {
                id = categoryId,
                name = tree?.GetPathText(categoryId, PathSeparator),
            };

    /// <summary>
    /// Gets an article's body, the Markdown of its Body part.
    /// </summary>
    /// <param name="article">The article.</param>
    /// <returns>The body, or <see langword="null"/>.</returns>
    public static string GetBody(ContentItem article)
        => article.TryGet<MarkdownBodyPart>(out var body) && !string.IsNullOrWhiteSpace(body.Markdown)
            ? body.Markdown.Trim()
            : null;

    /// <summary>
    /// Gets the passage of a body around the first of the words it holds (the words are tried in order), or its start
    /// when it holds none, cut between words.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="words">The words searched for, the most telling first.</param>
    /// <param name="length">The most characters of the body.</param>
    /// <returns>The passage, with "…" where it was cut, or <see langword="null"/> for an empty body.</returns>
    public static string GetPassage(string body, IEnumerable<string> words, int length = PassageLength)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var text = body.Trim();

        if (text.Length <= length)
        {
            return text;
        }

        var at = -1;

        foreach (var word in words ?? [])
        {
            if (!string.IsNullOrWhiteSpace(word) && (at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                break;
            }
        }

        // A third of the passage before the word, the rest after it.
        var start = at < 0 ? 0 : Math.Clamp(at - (length / 3), 0, text.Length - length);
        var end = start + length;

        // Between words, without losing the word found.
        if (start > 0)
        {
            var space = FirstWhiteSpace(text, start, Math.Min(end, start + WordSnap));

            if (space >= 0 && (at < 0 || space < at))
            {
                start = space + 1;
            }
        }

        if (end < text.Length)
        {
            var space = LastWhiteSpace(text, Math.Max(start, end - WordSnap), end);

            if (space > start && (at < 0 || space > at))
            {
                end = space;
            }
        }

        return (start > 0 ? "…" : string.Empty) + text[start..end].Trim() + (end < text.Length ? "…" : string.Empty);
    }

    /// <summary>
    /// Cuts a text to a length.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The most characters.</param>
    /// <param name="cut">Whether the text was cut.</param>
    /// <returns>The text, cut when longer.</returns>
    public static string Cut(string text, int length, out bool cut)
    {
        cut = text?.Length > length;

        return cut ? text[..length] : text;
    }

    private static int FirstWhiteSpace(string text, int from, int to)
    {
        for (var index = from; index < to; index++)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static int LastWhiteSpace(string text, int from, int to)
    {
        for (var index = to - 1; index >= from; index--)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
