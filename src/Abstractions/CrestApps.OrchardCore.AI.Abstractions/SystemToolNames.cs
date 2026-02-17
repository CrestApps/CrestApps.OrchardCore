namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Provides well-known names for system tools that are automatically
/// included by the orchestrator based on context availability.
/// </summary>
public static class SystemToolNames
{
    public const string ListDocuments = "list_documents";
    public const string ReadDocument = "read_document";
    public const string SearchDocuments = "search_documents";
    public const string SearchDataSources = "search_data_sources";
    public const string ReadTabularData = "read_tabular_data";
    public const string GenerateImage = "generate_image";
    public const string GenerateChart = "generate_chart";
}
