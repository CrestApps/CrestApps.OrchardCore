namespace CrestApps.Core.Infrastructure.Indexing.Models;

/// <summary>
/// Defines the data type of a field in a search index.
/// </summary>
public enum SearchFieldType
{
    /// <summary>
    /// Full-text searchable content.
    /// </summary>
    Text,

    /// <summary>
    /// Exact-match keyword (not tokenized).
    /// </summary>
    Keyword,

    /// <summary>
    /// Integer numeric value.
    /// </summary>
    Integer,

    /// <summary>
    /// Floating-point numeric value.
    /// </summary>
    Float,

    /// <summary>
    /// Date/time value.
    /// </summary>
    DateTime,

    /// <summary>
    /// Dense vector for similarity search.
    /// </summary>
    Vector,
}
