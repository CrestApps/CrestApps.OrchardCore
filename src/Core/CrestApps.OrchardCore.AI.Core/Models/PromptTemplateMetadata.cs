namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class PromptTemplateMetadata
{
    /// <summary>
    /// Gets or sets the ordered list of selected prompt templates.
    /// </summary>
    public List<PromptTemplateSelectionEntry> Templates { get; set; } = [];

    public void SetSelections(IEnumerable<PromptTemplateSelectionEntry> selections)
    {
        Templates = selections?
            .Where(selection => !string.IsNullOrWhiteSpace(selection.TemplateId))
            .Select(selection => new PromptTemplateSelectionEntry
            {
                TemplateId = selection.TemplateId,
                Parameters = selection.Parameters is { Count: > 0 }
                    ? new Dictionary<string, object>(selection.Parameters, StringComparer.OrdinalIgnoreCase)
                    : null,
            })
            .ToList() ?? [];
    }
}
