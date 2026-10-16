using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Providers;

namespace CrestApps.OrchardCore.AI.Prompting.Providers;

/// <summary>
/// Exposes the System Prompt AI templates (created under AI Templates or shipped as
/// profile template files) through the template service, so a prompt selection stored
/// on an AI profile or chat interaction renders at completion time.
/// Template identifiers are the template names, matching the identifiers the prompt
/// selection editor stores.
/// </summary>
/// <remarks>
/// These templates can change at any time, so <see cref="Services.OrchardCoreTemplateService"/>
/// reads them once per scope instead of keeping them in its tenant-wide cache.
/// This provider is therefore not registered as an <see cref="ITemplateProvider"/>.
/// </remarks>
public sealed class AIProfileSystemPromptTemplateProvider : ITemplateProvider
{
    private readonly IAIProfileTemplateManager _templateManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileSystemPromptTemplateProvider"/> class.
    /// </summary>
    /// <param name="templateManager">The AI profile template manager.</param>
    public AIProfileSystemPromptTemplateProvider(IAIProfileTemplateManager templateManager)
    {
        _templateManager = templateManager;
    }

    /// <summary>
    /// Gets the System Prompt AI templates.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<Template>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _templateManager.GetAllAsync(cancellationToken);

        return (templates ?? [])
            .Where(template =>
                template != null &&
                string.Equals(template.Source, AITemplateSources.SystemPrompt, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(template.Name))
            .Select(MapTemplate)
            .ToList();
    }

    private static Template MapTemplate(AIProfileTemplate template)
    {
        template.TryGet<SystemPromptTemplateMetadata>(out var metadata);

        return new Template
        {
            Id = template.Name,
            Kind = template.Source,
            Source = template.Source,
            Content = metadata?.SystemMessage ?? string.Empty,
            Metadata = new TemplateMetadata
            {
                Title = string.IsNullOrWhiteSpace(template.DisplayText) ? template.Name : template.DisplayText,
                Description = template.Description,
                Category = template.Category,
                IsListable = template.IsListable,
            },
        };
    }
}
