namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines well-known purpose identifiers for AI tools.
/// Use these constants with <see cref="AIToolBuilder{TTool}.WithPurpose(string)"/>
/// to tag tools with their intended purpose.
/// </summary>
public static class AIToolPurposes
{
    /// <summary>
    /// Tools that process, read, search, or manage documents attached to a chat session.
    /// </summary>
    public const string DocumentProcessing = "document_processing";

    /// <summary>
    /// Tools that generate content such as images or charts.
    /// </summary>
    public const string ContentGeneration = "content_generation";
}
