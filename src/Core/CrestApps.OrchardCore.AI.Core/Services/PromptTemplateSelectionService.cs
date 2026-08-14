using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Rendering;
using CrestApps.Core.Templates.Services;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Manages the selection and composition of prompt templates for AI profiles,
/// merging runtime templates from the profile template store with static templates
/// from the template service.
/// </summary>
public sealed class PromptTemplateSelectionService
{
    private readonly ITemplateService _aiTemplateService;
    private readonly ITemplateEngine _aiTemplateEngine;

    private readonly IAIProfileTemplateManager _profileTemplateManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptTemplateSelectionService"/> class.
    /// </summary>
    /// <param name="aiTemplateService">The template service for listing static templates.</param>
    /// <param name="aiTemplateEngine">The template engine for rendering template content.</param>
    /// <param name="profileTemplateManager">The manager for runtime AI profile templates.</param>
    public PromptTemplateSelectionService(
        ITemplateService aiTemplateService,
        ITemplateEngine aiTemplateEngine,
        IAIProfileTemplateManager profileTemplateManager)
    {
        _aiTemplateService = aiTemplateService;
        _aiTemplateEngine = aiTemplateEngine;
        _profileTemplateManager = profileTemplateManager;
    }

    /// <summary>
    /// Lists all available prompt templates, giving precedence to runtime templates over static ones.
    /// </summary>
    public async Task<IReadOnlyList<Template>> ListAsync()
    {
        var templates = new List<Template>();

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in await GetRuntimeTemplatesAsync())
        {
            AddTemplate(templates, seenIds, template);
        }

        foreach (var template in await _aiTemplateService.ListAsync())
        {
            AddTemplate(templates, seenIds, template);
        }

        return templates;
    }

    /// <summary>
    /// Retrieves a single prompt template by its identifier.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    public async Task<Template> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var templates = await ListAsync();

        return templates.FirstOrDefault(template => string.Equals(template.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Composes a full system message by rendering each selected prompt template
    /// and appending the provided system message.
    /// </summary>
    /// <param name="systemMessage">The base system message to append, or <c>null</c> to omit.</param>
    /// <param name="metadata">The template selection metadata containing template IDs and parameters.</param>
    public async Task<string> ComposeSystemMessageAsync(string systemMessage, PromptTemplateMetadata metadata)
    {
        var parts = new List<string>();
        var selections = metadata?.Templates?
            .Where(selection => !string.IsNullOrWhiteSpace(selection.TemplateId))
            .ToList() ?? [];

        foreach (var selection in selections)
        {
            var template = await GetAsync(selection.TemplateId)

            ?? throw new KeyNotFoundException($"Prompt template '{selection.TemplateId}' was not found.");

            var rendered = await _aiTemplateEngine.RenderAsync(template.Content, selection.Parameters);

            if (!string.IsNullOrWhiteSpace(rendered))
            {
                parts.Add(rendered);
            }
        }

        if (!string.IsNullOrWhiteSpace(systemMessage))
        {
            parts.Add(systemMessage);
        }

        return parts.Count == 0
        ? null
        : string.Join(Environment.NewLine + Environment.NewLine, parts);
    }
    private async Task<IEnumerable<Template>> GetRuntimeTemplatesAsync()
    {
        var templates = await _profileTemplateManager.GetAllAsync();

        return templates
            .Where(template =>
        string.Equals(template.Source, AITemplateSources.SystemPrompt, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(template.Name))
            .Select(ConvertToTemplate)
            .ToList();
    }

    private static void AddTemplate(List<Template> templates, HashSet<string> seenIds, Template template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.Id) || !seenIds.Add(template.Id))
        {
            return;
        }

        templates.Add(template);
    }
    private static Template ConvertToTemplate(AIProfileTemplate template)
    {
        var metadata = template.GetOrCreate<SystemPromptTemplateMetadata>();

        return new Template
        {
            Id = template.Name,
            Content = metadata.SystemMessage,
            Source = template.Source,
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
