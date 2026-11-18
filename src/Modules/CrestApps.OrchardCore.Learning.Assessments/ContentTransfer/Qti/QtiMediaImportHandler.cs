using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;

/// <summary>
/// Stores the images of an imported QTI item in the media library, in a folder of the import, and writes their addresses
/// in the prompt and the feedback. A row whose images cannot be stored fails rather than keeping a broken picture. Only a
/// row the QTI reader wrote is read: the media column of any other file (a spreadsheet, say) is ignored, and every image
/// must be of the parser's image types, with the bytes of one.
/// </summary>
internal sealed class QtiMediaImportHandler : IContentImportHandler
{
    /// <summary>
    /// The media folder that holds a folder per import.
    /// </summary>
    public const string MediaFolder = "qti-imports";

    private static readonly string[] _htmlColumns = ["QuestionPart_Prompt", "QuestionPart_GeneralFeedback"];

    private readonly IMediaFileStore _mediaFileStore;
    private readonly MediaOptions _mediaOptions;
    private readonly IEnumerable<IContentTransferFileFormatProvider> _formats;

    internal readonly IStringLocalizer S;

    public QtiMediaImportHandler(
        IMediaFileStore mediaFileStore,
        IOptions<MediaOptions> mediaOptions,
        IEnumerable<IContentTransferFileFormatProvider> formats,
        IStringLocalizer<QtiMediaImportHandler> stringLocalizer)
    {
        _mediaFileStore = mediaFileStore;
        _mediaOptions = mediaOptions.Value;
        _formats = formats;
        S = stringLocalizer;
    }

    /// <summary>
    /// Tells whether an import's file was read by a QTI format: only such rows carry the QTI columns the reader wrote.
    /// </summary>
    /// <param name="formats">The file formats.</param>
    /// <param name="entry">The import.</param>
    /// <returns><see langword="true"/> for a QTI package or item file.</returns>
    internal static bool ReadByQti(IEnumerable<IContentTransferFileFormatProvider> formats, ContentTransferEntry entry)
    {
        var fileName = string.IsNullOrWhiteSpace(entry?.StoredFileName) ? entry?.UploadedFileName : entry.StoredFileName;

        return !string.IsNullOrWhiteSpace(fileName) && (formats ?? []).OfType<QtiFileFormatProvider>().Any(format => format.CanHandle(fileName));
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(ImportContentContext context)
        => Task.FromResult<IReadOnlyCollection<ImportColumn>>([]);

    /// <inheritdoc />
    public async Task ImportAsync(ContentImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Errors.Count > 0 || context.Columns is null || !context.Columns.Contains(QtiColumns.Media) || !ReadByQti(_formats, context.Entry))
        {
            return;
        }

        var cell = context.Row[QtiColumns.Media]?.ToString();
        var files = string.IsNullOrWhiteSpace(cell) ? [] : JsonSerializer.Deserialize<List<QtiMediaCell>>(cell) ?? [];

        foreach (var file in files)
        {
            var path = MediaPath(context.Entry?.EntryId, file.Path);

            if (path is null || !QtiMediaFile.ImageExtensions.Contains(Path.GetExtension(path)) || !_mediaOptions.AllowedFileExtensions.Contains(Path.GetExtension(path)))
            {
                context.Fail(S["The image {0} is not a kind of file the media library accepts.", file.Path], S["Not imported from QTI"]);

                return;
            }

            byte[] content;

            try
            {
                content = Convert.FromBase64String(file.Data ?? string.Empty);
            }
            catch (FormatException)
            {
                context.Fail(S["The image {0} could not be read.", file.Path], S["Not imported from QTI"]);

                return;
            }

            // The bytes must be an image of those types too: the extension is the package's word only.
            if (!QtiMediaFile.HasImageSignature(content))
            {
                context.Fail(S["The image {0} is not a PNG, JPEG, GIF or WebP image.", file.Path], S["Not imported from QTI"]);

                return;
            }

            if (_mediaOptions.MaxFileSize > 0 && content.LongLength > _mediaOptions.MaxFileSize)
            {
                context.Fail(S["The image {0} is larger than the media library accepts.", file.Path], S["Not imported from QTI"]);

                return;
            }

            try
            {
                using var stream = new MemoryStream(content, writable: false);

                // The folder is the import's own, so a run of the same import writes the same file again.
                await _mediaFileStore.CreateFileFromStreamAsync(path, stream, overwrite: true);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                context.Fail(S["The image {0} could not be stored in the media library: {1}", file.Path, exception.Message], S["Not imported from QTI"]);

                return;
            }

            var reference = WebUtility.HtmlEncode(QtiMediaFile.Scheme + file.Path);
            var url = WebUtility.HtmlEncode(_mediaFileStore.MapPathToPublicUrl(path));

            foreach (var column in _htmlColumns)
            {
                if (context.Columns.Contains(column) && context.Row[column]?.ToString() is { Length: > 0 } html)
                {
                    context.Row[column] = html.Replace(reference, url, StringComparison.Ordinal);
                }
            }
        }

        foreach (var column in _htmlColumns)
        {
            if (context.Columns.Contains(column) && (context.Row[column]?.ToString() ?? string.Empty).Contains(QtiMediaFile.Scheme, StringComparison.Ordinal))
            {
                context.Fail(S["An image of the item is missing from the package."], S["Not imported from QTI"]);

                return;
            }
        }
    }

    /// <inheritdoc />
    public Task ExportAsync(ContentExportContext context)
        => Task.CompletedTask;

    /// <summary>
    /// Returns the media path of an image of a package: under the folder of the import, each segment of its package path
    /// kept to letters, digits, dots, dashes and underscores.
    /// </summary>
    /// <param name="entryId">The import.</param>
    /// <param name="packagePath">The path of the image in the package.</param>
    /// <returns>The media path, or <see langword="null"/> when nothing safe is left.</returns>
    internal static string MediaPath(string entryId, string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return null;
        }

        var segments = new List<string> { MediaFolder, Safe(string.IsNullOrWhiteSpace(entryId) ? "import" : entryId) };

        foreach (var segment in packagePath.Split('/', '\\'))
        {
            var safe = Safe(segment);

            if (safe.Length == 0 || safe.Trim('.').Length == 0)
            {
                return null;
            }

            segments.Add(safe);
        }

        return segments.Count > 2 ? string.Join('/', segments) : null;
    }

    private static string Safe(string segment)
    {
        var builder = new StringBuilder(segment.Length);

        foreach (var character in segment)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-');
        }

        return builder.ToString().Trim('-');
    }
}
