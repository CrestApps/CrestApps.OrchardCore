namespace CrestApps.OrchardCore.Surveys.Core.Imports;

/// <summary>
/// Reads an uploaded question file into memory up to a size limit, so no import format reads an upload of any size:
/// the readers need the whole file (a workbook or a package is a zip archive, and a CSV is parsed whole), and the copy
/// stops as soon as the file passes the limit.
/// </summary>
public static class QuestionImportUploads
{
    /// <summary>
    /// The largest spreadsheet (CSV or Excel) a question import reads: 10 MB.
    /// </summary>
    public const long MaxSpreadsheetBytes = 10 * 1024 * 1024;

    /// <summary>
    /// The largest QTI item or package (with its images) a question import reads: 50 MB.
    /// </summary>
    public const long MaxPackageBytes = 50 * 1024 * 1024;

    /// <summary>
    /// Copies a stream into a memory buffer, stopping when it passes a size.
    /// </summary>
    /// <param name="source">The upload.</param>
    /// <param name="maxBytes">The most bytes read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The buffer at position 0, or <see langword="null"/> when the upload is larger.</returns>
    public static async Task<MemoryStream> ReadAtMostAsync(Stream source, long maxBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;

            if (total > maxBytes)
            {
                await buffer.DisposeAsync();

                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        buffer.Position = 0;

        return buffer;
    }
}
