namespace CrestApps.Core.Infrastructure.Indexing.Models;

/// <summary>
/// Describes a field in a search index, including its type, whether it is the key,
/// and vector-specific settings.
/// </summary>
public sealed class SearchIndexField
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the field data type.
    /// </summary>
    public SearchFieldType FieldType { get; set; }

    /// <summary>
    /// Gets or sets whether this field is the document key.
    /// </summary>
    public bool IsKey { get; set; }

    /// <summary>
    /// Gets or sets whether this field supports filtering.
    /// </summary>
    public bool IsFilterable { get; set; }

    /// <summary>
    /// Gets or sets whether this field supports full-text search.
    /// </summary>
    public bool IsSearchable { get; set; }

    /// <summary>
    /// Gets or sets the number of dimensions for a vector field.
    /// Only applicable when <see cref="FieldType"/> is <see cref="SearchFieldType.Vector"/>.
    /// </summary>
    public int? VectorDimensions { get; set; }
}
