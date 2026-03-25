using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Rendering;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class PromptTemplateSelectionService
{
    private readonly IAITemplateService _aiTemplateService;
    private readonly IAITemplateEngine _aiTemplateEngine;
    private readonly IAIProfileTemplateManager _profileTemplateManager;

    public PromptTemplateSelectionService(
        IAITemplateService aiTemplateService,
        IAITemplateEngine aiTemplateEngine,
        IAIProfileTemplateManager profileTemplateManager)
    {
        _aiTemplateService = aiTemplateService;
        _aiTemplateEngine = aiTemplateEngine;
        _profileTemplateManager = profileTemplateManager;
    }

    public async Task<IReadOnlyList<AITemplate>> ListAsync()
    {
        var templates = new List<AITemplate>();
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

    public async Task<AITemplate> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var templates = await ListAsync();

        return templates.FirstOrDefault(template => string.Equals(template.Id, id, StringComparison.OrdinalIgnoreCase));
    }

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

    private async Task<IEnumerable<AITemplate>> GetRuntimeTemplatesAsync()
    {
        var templates = await _profileTemplateManager.GetAllAsync();

        return templates
            .Where(template =>
                string.Equals(template.Source, AITemplateSources.SystemPrompt, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(template.Name))
            .Select(ConvertToTemplate)
            .ToList();
    }

    private static void AddTemplate(List<AITemplate> templates, HashSet<string> seenIds, AITemplate template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.Id) || !seenIds.Add(template.Id))
        {
            return;
        }

        templates.Add(template);
    }

    private static AITemplate ConvertToTemplate(AIProfileTemplate template)
    {
        var metadata = template.As<SystemPromptTemplateMetadata>();

        return new AITemplate
        {
            Id = template.Name,
            Content = metadata.SystemMessage,
            Source = template.Source,
            Metadata = new AITemplateMetadata
            {
                Title = string.IsNullOrWhiteSpace(template.DisplayText) ? template.Name : template.DisplayText,
                Description = template.Description,
                Category = template.Category,
                IsListable = template.IsListable,
            },
        };
    }
}
