using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ProfileTemplates;

/// <summary>
/// What the AI profile template tools share.
/// </summary>
internal static class ProfileTemplateToolSupport
{
    /// <summary>
    /// Gets the IDs of the templates stored on the site. A template the manager lists that is not stored is shipped
    /// with a module and cannot be changed or deleted.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The IDs.</returns>
    public static async Task<HashSet<string>> GetStoredIdsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var catalog = services.GetService<INamedSourceCatalog<AIProfileTemplate>>();

        if (catalog is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return (await catalog.GetAllAsync(cancellationToken))
            .Select(template => template.ItemId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Finds a stored template by its ID.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="templateId">The ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored template, or <see langword="null"/> when none is stored with the ID.</returns>
    public static async Task<AIProfileTemplate> FindStoredAsync(IServiceProvider services, string templateId, CancellationToken cancellationToken)
    {
        var catalog = services.GetService<INamedSourceCatalog<AIProfileTemplate>>();

        return catalog is null || string.IsNullOrWhiteSpace(templateId)
            ? null
            : await catalog.FindByIdAsync(templateId.Trim(), cancellationToken);
    }

    /// <summary>
    /// Gets the title of a template for a message.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>The title, or the name when it has none.</returns>
    public static string GetTitle(AIProfileTemplate template)
        => string.IsNullOrWhiteSpace(template.DisplayText) ? template.Name : template.DisplayText;
}
