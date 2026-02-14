namespace CrestApps.OrchardCore.AI;

public sealed class AIToolDefinitionEntry
{
    public AIToolDefinitionEntry(Type type)
    {
        ToolType = type;
    }

    public Type ToolType { get; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Category { get; set; }

    public string Name { get; internal set; }

    /// <summary>
    /// Gets or sets whether this tool is a system tool. System tools are automatically
    /// included by the orchestrator based on context availability and are not shown
    /// in the UI tool selection.
    /// </summary>
    public bool IsSystemTool { get; set; }

    /// <summary>
    /// Gets or sets the purpose tag for this tool. Use well-known constants from
    /// <see cref="AIToolPurposes"/> or custom strings for domain-specific grouping.
    /// The orchestrator uses this to dynamically discover tools by purpose
    /// (e.g., document processing tools for enriching system messages).
    /// </summary>
    public string Purpose { get; set; }

    public bool HasPurpose(string purpose)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        if (Purpose == null)
        {
            return false;
        }

        return string.Equals(Purpose, purpose, StringComparison.OrdinalIgnoreCase);
    }
}
