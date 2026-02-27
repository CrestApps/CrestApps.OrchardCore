namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class PromptTemplateMetadata
{
    /// <summary>
    /// Gets or sets the identifier of the selected AI template.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the template parameters as key-value pairs.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; }
}
