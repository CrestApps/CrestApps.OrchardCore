using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// A fluent builder for configuring an AI tool registration.
/// By default, tools are registered as system tools (not visible in the UI).
/// Call <see cref="Selectable"/> to make the tool visible in the UI for user selection.
/// </summary>
/// <typeparam name="TTool">The tool type implementing <see cref="AITool"/>.</typeparam>
public sealed class AIToolBuilder<TTool>
    where TTool : AITool
{
    private readonly AIToolDefinitionEntry _entry;

    internal AIToolBuilder(AIToolDefinitionEntry entry)
    {
        _entry = entry;
    }

    /// <summary>
    /// Sets the display title for this tool.
    /// </summary>
    public AIToolBuilder<TTool> WithTitle(string title)
    {
        _entry.Title = title;
        return this;
    }

    /// <summary>
    /// Sets the description for this tool.
    /// </summary>
    public AIToolBuilder<TTool> WithDescription(string description)
    {
        _entry.Description = description;
        return this;
    }

    /// <summary>
    /// Sets the category for grouping this tool in the UI.
    /// </summary>
    public AIToolBuilder<TTool> WithCategory(string category)
    {
        _entry.Category = category;
        return this;
    }

    /// <summary>
    /// Sets the purpose tag for this tool. Use well-known constants from <see cref="AIToolPurposes"/>
    /// or define custom purpose strings for domain-specific tool grouping.
    /// </summary>
    public AIToolBuilder<TTool> WithPurpose(string purpose)
    {
        _entry.Purpose = purpose;
        return this;
    }

    /// <summary>
    /// Makes this tool visible in the UI for user selection.
    /// By default, tools are system tools managed by the orchestrator and are not shown in the UI.
    /// </summary>
    public AIToolBuilder<TTool> Selectable()
    {
        _entry.IsSystemTool = false;
        return this;
    }
}
