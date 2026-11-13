using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// Spots, in a template the AI saved, the mistakes that make a theme look broken although the template is valid:
/// a layout that no longer loads the theme's own stylesheets and scripts, and styling written inline instead of in
/// the stylesheet.
/// </summary>
internal static partial class ThemeTemplateReview
{
    /// <summary>
    /// The number of inline style attributes above which a template is told to move them into the stylesheet.
    /// </summary>
    public const int MaxInlineStyles = 3;

    /// <summary>
    /// Gets whether a template renders the page layout: <c>Layout</c> or one of its alternates.
    /// </summary>
    /// <param name="name">The template name.</param>
    public static bool IsLayout(string name)
        => string.Equals(name, "Layout", StringComparison.OrdinalIgnoreCase) ||
            (name?.StartsWith("Layout_", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>
    /// Gets the names of the resources a Liquid or Razor template asks the resource manager for, such as
    /// <c>{% style name: "theblogtheme" %}</c> or <c>&lt;script asp-name="theblogtheme"&gt;</c>.
    /// </summary>
    /// <param name="template">The template's source.</param>
    public static IReadOnlyList<string> GetResourceNames(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return [];
        }

        return LiquidResourcePattern().Matches(template)
            .Concat(RazorResourcePattern().Matches(template))
            .Select(match => match.Groups["name"].Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Gets the resources the theme's own layout loads that <paramref name="layout"/> leaves out.
    /// </summary>
    /// <param name="themeLayout">The source of the theme's own layout.</param>
    /// <param name="layout">The layout the AI saved.</param>
    public static IReadOnlyList<string> GetDroppedResources(string themeLayout, string layout)
    {
        var kept = GetResourceNames(layout).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return GetResourceNames(themeLayout)
            .Where(name => !kept.Contains(name))
            .ToArray();
    }

    /// <summary>
    /// Gets the Liquid tag that loads <paramref name="resource"/> the way the theme's own layout does: its own tag when
    /// that layout is Liquid, or the Liquid equivalent of a Razor tag.
    /// </summary>
    /// <param name="themeLayout">The source of the theme's own layout.</param>
    /// <param name="resource">The resource name.</param>
    public static string GetLiquidTag(string themeLayout, string resource)
    {
        foreach (Match match in LiquidResourcePattern().Matches(themeLayout ?? string.Empty))
        {
            if (string.Equals(match.Groups["name"].Value, resource, StringComparison.OrdinalIgnoreCase))
            {
                var end = themeLayout.IndexOf("%}", match.Index, StringComparison.Ordinal);

                return end < 0 ? match.Value : themeLayout[match.Index..(end + 2)];
            }
        }

        foreach (Match match in RazorResourcePattern().Matches(themeLayout ?? string.Empty))
        {
            if (string.Equals(match.Groups["name"].Value, resource, StringComparison.OrdinalIgnoreCase))
            {
                return match.Value.StartsWith("<script", StringComparison.OrdinalIgnoreCase)
                    ? $"{{% script name: \"{resource}\", at: \"Foot\" %}}"
                    : $"{{% style name: \"{resource}\" %}}";
            }
        }

        return $"{{% style name: \"{resource}\" %}}";
    }

    /// <summary>
    /// The size under which the site's own stylesheets can't carry a new theme, in bytes.
    /// </summary>
    public const int MinNewThemeStylesheetBytes = 3000;

    /// <summary>
    /// Gets whether a draft layout builds a new theme: it leaves out stylesheets or scripts the theme's own layout
    /// loads.
    /// </summary>
    /// <param name="themeLayout">The source of the theme's own layout.</param>
    /// <param name="draftLayout">The draft's layout.</param>
    public static bool IsNewTheme(string themeLayout, string draftLayout)
        => !string.IsNullOrEmpty(draftLayout) && GetDroppedResources(themeLayout, draftLayout).Count > 0;

    /// <summary>
    /// Builds the to-do list a preview of a new theme returns: the parts of the page the theme still draws and a
    /// stylesheet too small to style the page.
    /// </summary>
    /// <param name="rendered">The templates that drew the page.</param>
    /// <param name="siteStylesheetBytes">The size of the site's own stylesheets on the page.</param>
    public static IReadOnlyList<string> GetNewThemeAdvice(IEnumerable<(string Name, string Source)> rendered, long siteStylesheetBytes)
    {
        var advice = new List<string>();

        var themeDrawn = rendered
            .Where(template => template.Source is not ("theme draft" or "published template"))
            .Where(template => !IsLayout(template.Name))
            .Select(template => $"{template.Name} ({template.Source})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (themeDrawn.Length > 0)
        {
            advice.Add("These parts of the page are still drawn by the theme's own templates, with its markup and class names: " +
                string.Join(", ", themeDrawn) +
                ". Replace the ones that show content or the old look (banners, lists, item pages) under exactly these names, with your own markup and classes.");
        }

        if (siteStylesheetBytes < MinNewThemeStylesheetBytes)
        {
            advice.Add($"The site's own stylesheets on this page add up to {siteStylesheetBytes:N0} bytes, too little to style a new theme, so most of the page keeps Bootstrap's defaults. Write the complete stylesheet: base text and colors, header and menu (also collapsed on phones), the hero, the cards, the item page, buttons, forms, the pager, the footer and @media rules for phones.");
        }

        return advice;
    }

    /// <summary>
    /// Counts the inline <c>style</c> attributes in a template.
    /// </summary>
    /// <param name="template">The template's source.</param>
    public static int CountInlineStyles(string template)
        => string.IsNullOrEmpty(template) ? 0 : InlineStylePattern().Count(template);

    [GeneratedRegex(@"\{%-?\s*(?:style|script)\b[^%]*?\bname\s*:\s*[""'](?<name>[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LiquidResourcePattern();

    [GeneratedRegex(@"<(?:style|script)\b[^>]*\basp-name\s*=\s*[""'](?<name>[^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RazorResourcePattern();

    [GeneratedRegex(@"\sstyle\s*=\s*[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InlineStylePattern();
}
