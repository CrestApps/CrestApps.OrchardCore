using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Services;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Lists the prompt templates of a category. Speaking styles and guardrail topics are prompt templates in their own
/// category, so the ones a module ships and the ones saved under <c>Artificial Intelligence &gt; Templates</c> are
/// listed together.
/// </summary>
public static class AITemplateCategoryExtensions
{
    /// <summary>
    /// Lists the templates whose category is <paramref name="category"/>, by title.
    /// </summary>
    /// <param name="templateService">The template service.</param>
    /// <param name="category">The category, compared ignoring case.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The templates, once each.</returns>
    public static async Task<IReadOnlyList<Template>> ListByCategoryAsync(this ITemplateService templateService, string category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(templateService);

        var templates = await templateService.ListAsync(cancellationToken) ?? [];

        return templates
            .Where(template => !string.IsNullOrEmpty(template.Id) &&
                string.Equals(template.Metadata?.Category?.Trim(), category, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(template => template.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(template => GetTitle(template), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Lists the templates whose category is <paramref name="category"/> as choices for an editor, by title.
    /// </summary>
    /// <param name="templateService">The template service.</param>
    /// <param name="category">The category, compared ignoring case.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The choices.</returns>
    public static async Task<IReadOnlyList<AITemplateOption>> ListOptionsByCategoryAsync(this ITemplateService templateService, string category, CancellationToken cancellationToken = default)
    {
        var templates = await templateService.ListByCategoryAsync(category, cancellationToken);

        return templates.Select(template => new AITemplateOption(template.Id, GetTitle(template), template.Metadata?.Description)).ToList();
    }

    /// <summary>
    /// Gets the title a template is shown with: its own, or its identifier.
    /// </summary>
    /// <param name="template">The template.</param>
    public static string GetTitle(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return string.IsNullOrWhiteSpace(template.Metadata?.Title) ? template.Id : template.Metadata.Title.Trim();
    }
}
