namespace CrestApps.Core.Infrastructure.Indexing;

/// <summary>
/// Well-known index profile type identifiers.
/// </summary>
public static class IndexProfileTypes
{
    /// <summary>
    /// Index type for AI document chunks (uploaded files chunked and embedded).
    /// </summary>
    public const string AIDocuments = "AIDocuments";

    /// <summary>
    /// Index type for data source knowledge base entries.
    /// </summary>
    public const string DataSource = "DataSourceIndex";

    /// <summary>
    /// Index type for AI memory entries.
    /// </summary>
    public const string AIMemory = "AIMemory";

    /// <summary>
    /// Index type for custom article records.
    /// </summary>
    public const string Articles = "Articles";
}
