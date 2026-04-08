namespace CrestApps.Core.AI.Models;

/// <summary>
/// Stores the selected agent profile names on an AI profile or template.
/// Stored in Properties via Put/As pattern, parallel to <see cref="FunctionInvocationMetadata"/>.
/// </summary>
public sealed class AgentInvocationMetadata
{
    /// <summary>
    /// Gets or sets the names of agent profiles to include.
    /// </summary>
    public string[] Names { get; set; }
}
