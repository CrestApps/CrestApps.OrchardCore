namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Provides well-known keys for <see cref="Models.AICompletionContext.AdditionalProperties"/>.
/// These keys are used by orchestration handlers to communicate context availability
/// to downstream consumers such as <see cref="IToolRegistryProvider"/> implementations.
/// </summary>
public static class AICompletionContextKeys
{
    /// <summary>
    /// When set to <see langword="true"/> in <see cref="Models.AICompletionContext.AdditionalProperties"/>,
    /// indicates that documents are available for the current session. This enables
    /// document processing system tools (e.g., <c>search_documents</c>, <c>list_documents</c>)
    /// to be included in the tool registry.
    /// </summary>
    public const string HasDocuments = "HasDocuments";
}
