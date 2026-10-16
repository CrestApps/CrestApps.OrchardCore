using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Rendering;
using CrestApps.Core.Templates.Services;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Manages the selection and composition of prompt templates for AI profiles.
/// Templates come from the template service, which includes the System Prompt AI templates
/// under their names.
/// </summary>
public sealed class PromptTemplateSelectionService
{
    private readonly ITemplateService _aiTemplateService;
    private readonly ITemplateEngine _aiTemplateEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptTemplateSelectionService"/> class.
    /// </summary>
    /// <param name="aiTemplateService">The template service for listing templates.</param>
    /// <param name="aiTemplateEngine">The template engine for rendering template content.</param>
    public PromptTemplateSelectionService(
        ITemplateService aiTemplateService,
        ITemplateEngine aiTemplateEngine)
    {
        _aiTemplateService = aiTemplateService;
        _aiTemplateEngine = aiTemplateEngine;
    }

    /// <summary>
    /// Lists all available prompt templates.
    /// </summary>
    public Task<IReadOnlyList<Template>> ListAsync()
    {
        return _aiTemplateService.ListAsync();
    }

    /// <summary>
    /// Retrieves a single prompt template by its identifier or one of its aliases.
    /// </summary>
    /// <param name="id">The template identifier.</param>
    public Task<Template> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return _aiTemplateService.GetAsync(id);
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
}
