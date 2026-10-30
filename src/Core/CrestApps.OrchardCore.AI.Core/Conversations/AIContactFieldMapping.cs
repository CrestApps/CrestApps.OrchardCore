using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Which extracted fields of a profile's conversations are written to fields of the contact, and how.
/// </summary>
public sealed class AIProfileContactFieldMappingSettings
{
    /// <summary>
    /// Gets or sets the mappings, one per extracted field.
    /// </summary>
    public List<AIContactFieldMapping> Mappings { get; set; } = [];
}

/// <summary>
/// An extracted field written to a field of the contact.
/// </summary>
public sealed class AIContactFieldMapping
{
    /// <summary>
    /// Gets or sets the extracted field's name.
    /// </summary>
    public string FieldName { get; set; }

    /// <summary>
    /// Gets or sets the contact field, as <c>PartName.FieldName</c>, such as <c>Contact.Company</c>.
    /// </summary>
    public string ContactField { get; set; }

    /// <summary>
    /// Gets or sets how the value is written.
    /// </summary>
    public AIContactFieldMappingMode Mode { get; set; }
}

/// <summary>
/// How an extracted value is written to a contact field.
/// </summary>
public enum AIContactFieldMappingMode
{
    /// <summary>
    /// The value replaces what the field held.
    /// </summary>
    Overwrite,

    /// <summary>
    /// The value is written only when the field is empty.
    /// </summary>
    FillIfEmpty,

    /// <summary>
    /// The value is added after what a text field held, separated by a semicolon.
    /// </summary>
    Append,
}

/// <summary>
/// Runs when an AI conversation is closed, after its analysis and contact memory.
/// </summary>
public interface IAIConversationClosedHandler
{
    /// <summary>
    /// Handles a closed conversation.
    /// </summary>
    /// <param name="profile">The conversation's profile.</param>
    /// <param name="session">The conversation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the conversation is handled.</returns>
    Task ClosedAsync(AIProfile profile, AIChatSession session, CancellationToken cancellationToken = default);
}
