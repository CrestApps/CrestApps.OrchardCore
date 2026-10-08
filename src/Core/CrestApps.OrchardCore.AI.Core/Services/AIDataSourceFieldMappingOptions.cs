namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// The data source types whose editor offers the shared field mapping: which column or field carries the
/// content, the title and the reference key.
/// </summary>
/// <remarks>
/// <para>
/// Opt-in, not opt-out. A source only needs the mapping when it reads rows it did not shape itself and has
/// to be told which field is which -- a search index, a table. A source that produces fully-formed
/// documents, such as the <c>Web</c> and <c>File</c> sources, already knows what its title, content and key
/// are, so asking is a question with no answer: the fields save, reload and are never read.
/// </para>
/// <para>
/// It was the other way round, and every source type got the mapping unless it was named as an exception.
/// That meant a new source type inherited a section it had no use for simply by existing, which is how the
/// <c>File</c> source came to show one.
/// </para>
/// </remarks>
public sealed class AIDataSourceFieldMappingOptions
{
    private readonly HashSet<string> _sourceTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the data source types that require the field mapping.
    /// </summary>
    public IReadOnlyCollection<string> SourceTypes => _sourceTypes;

    /// <summary>
    /// Declares that a data source type requires the field mapping.
    /// </summary>
    /// <param name="sourceType">The data source type.</param>
    /// <remarks>
    /// Called by the feature that owns the source type, so a source that needs the mapping says so itself
    /// and one that does not says nothing.
    /// </remarks>
    public void Require(string sourceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);

        _sourceTypes.Add(sourceType);
    }

    /// <summary>
    /// Determines whether a data source type requires the field mapping.
    /// </summary>
    /// <param name="sourceType">The data source type.</param>
    /// <returns><see langword="true"/> when the source type asked for the mapping.</returns>
    public bool IsRequired(string sourceType)
        => !string.IsNullOrWhiteSpace(sourceType) && _sourceTypes.Contains(sourceType);
}
