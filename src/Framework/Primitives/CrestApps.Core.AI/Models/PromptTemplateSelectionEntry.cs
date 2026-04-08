namespace CrestApps.Core.AI.Models;

public sealed class PromptTemplateSelectionEntry
{
    public string TemplateId { get; set; }

    public Dictionary<string, object> Parameters { get; set; }
}
