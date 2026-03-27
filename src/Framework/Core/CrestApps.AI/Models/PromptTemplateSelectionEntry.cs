namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class PromptTemplateSelectionEntry
{
    public string TemplateId { get; set; }

    public Dictionary<string, object> Parameters { get; set; }
}
