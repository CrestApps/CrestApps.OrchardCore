using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer.Services;

/// <summary>
/// Opens a file for import through the transformers, so a file another system laid out is read as the content type's
/// columns everywhere it's read: when it's imported, and when its failed rows are downloaded.
/// </summary>
public static class ContentTransferFileReaders
{
    /// <summary>
    /// Returns the reader of the first transformer that recognizes the file, or <paramref name="reader"/> when none
    /// does. A transformer reads the file through first, so <paramref name="createReader"/> opens it again for the
    /// next one.
    /// </summary>
    public static async Task<IContentTransferFileReader> TransformAsync(
        IEnumerable<IContentTransferFileReaderTransformer> transformers,
        Func<IContentTransferFileReader> createReader,
        ContentTypeDefinition contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createReader);

        foreach (var transformer in transformers ?? [])
        {
            using var source = createReader();

            if (await transformer.TransformAsync(source, contentType, cancellationToken) is { } transformed)
            {
                return transformed;
            }
        }

        return createReader();
    }
}

/// <summary>
/// A file already read into columns and rows, such as one a transformer reshaped.
/// </summary>
public sealed class InMemoryContentTransferFileReader : IContentTransferFileReader
{
    private readonly IReadOnlyList<string> _columns;
    private readonly IReadOnlyList<string[]> _rows;

    public InMemoryContentTransferFileReader(IReadOnlyList<string> columns, IReadOnlyList<string[]> rows)
    {
        _columns = columns ?? [];
        _rows = rows ?? [];
    }

    public IReadOnlyList<string> GetColumnNames()
        => _columns;

    public int GetRowCount()
        => _rows.Count;

    public IEnumerable<string[]> ReadRows()
        => _rows;

    public void Dispose()
    {
    }
}
