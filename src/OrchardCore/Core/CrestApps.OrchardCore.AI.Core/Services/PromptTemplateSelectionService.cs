using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using CrestApps.Templates.Models;
using CrestApps.Templates.Rendering;
using CrestApps.Templates.Services;
namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class PromptTemplateSelectionService
{
    private readonly ITemplateService _aiTemplateService;
    private readonly ITemplateEngine _aiTemplateEngine;
    private readonly IAIProfileTemplateManager _profileTemplateManager;
    public PromptTemplateSelectionService(
        ITemplateService aiTemplateService,
        ITemplateEngine aiTemplateEngine,
        IAIProfileTemplateManager profileTemplateManager)
    {
        _aiTemplateService = aiTemplateService;
        _aiTemplateEngine = aiTemplateEngine;
        _profileTemplateManager = profileTemplateManager;
    }
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
    public async Task<Template> GetAsync(string id)
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
        var metadata = template.As<SystemPromptTemplateMetadata>();
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
