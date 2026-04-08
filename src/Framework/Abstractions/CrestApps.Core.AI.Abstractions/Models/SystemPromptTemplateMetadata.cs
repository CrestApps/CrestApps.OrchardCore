namespace CrestApps.Core.AI.Models;

/// <summary>
/// Metadata for templates with a "SystemPrompt" source.
/// Stored in the template's <see cref="OrchardCore.Entities.Entity.Properties"/> via
/// <c>Put&lt;SystemPromptTemplateMetadata&gt;</c> / <c>As&lt;SystemPromptTemplateMetadata&gt;</c>.
/// </summary>
public sealed class SystemPromptTemplateMetadata
{
    /// <summary>
    /// Gets or sets the system prompt content.
    /// </summary>
    public string SystemMessage { get; set; }
}
