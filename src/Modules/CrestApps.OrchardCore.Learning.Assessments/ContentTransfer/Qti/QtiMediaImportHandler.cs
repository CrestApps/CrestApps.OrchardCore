using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;

/// <summary>
/// Stores the images of an imported QTI item and writes their addresses in the prompt and the feedback. A picture the
/// prompt shows goes to the media library, in a folder of the import, as learners see it while they answer. A picture
/// only the general feedback shows goes to a protected file area (<see cref="QtiFeedbackImages"/>) and is served to
/// staff, and to a learner once their review shows the feedback, so it cannot give the answer away before (review round
/// 9). A row whose images cannot be stored fails rather than keeping a broken picture. Only a row the QTI reader wrote is
/// read: the media column of any other file (a spreadsheet, say) is ignored, and every image must be of the parser's
/// image types, with the bytes of one.
/// </summary>
internal sealed class QtiMediaImportHandler : IContentImportHandler
{
    /// <summary>
    /// The media folder that holds a folder per import.
    /// </summary>
    public const string MediaFolder = "qti-imports";

    private const string PromptColumn = "QuestionPart_Prompt";
    private const string FeedbackColumn = "QuestionPart_GeneralFeedback";

    private static readonly string[] _htmlColumns = [PromptColumn, FeedbackColumn];

    private readonly IMediaFileStore _mediaFileStore;
    private readonly IProtectedFileStore<QtiFeedbackImages> _feedbackImages;
    private readonly ShellSettings _shellSettings;
    private readonly MediaOptions _mediaOptions;
    private readonly IEnumerable<IContentTransferFileFormatProvider> _formats;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public QtiMediaImportHandler(
        IMediaFileStore mediaFileStore,
        IProtectedFileStore<QtiFeedbackImages> feedbackImages,
        ShellSettings shellSettings,
        IOptions<MediaOptions> mediaOptions,
        IEnumerable<IContentTransferFileFormatProvider> formats,
        IStringLocalizer<QtiMediaImportHandler> stringLocalizer,
        ILogger<QtiMediaImportHandler> logger)
    {
        _mediaFileStore = mediaFileStore;
        _feedbackImages = feedbackImages;
        _shellSettings = shellSettings;
        _mediaOptions = mediaOptions.Value;
        _formats = formats;
        S = stringLocalizer;
        _logger = logger;
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

            var reference = WebUtility.HtmlEncode(QtiMediaFile.Scheme + file.Path);

            // A picture the prompt does not show is the feedback's alone: it could give the answer away, so it is kept
            // out of the media library.
            var feedbackOnly = !(context.Columns.Contains(PromptColumn) && (context.Row[PromptColumn]?.ToString() ?? string.Empty).Contains(reference, StringComparison.Ordinal));
            string url;

            try
            {
                using var stream = new MemoryStream(content, writable: false);

                if (feedbackOnly)
                {
                    var stored = await _feedbackImages.SaveAsync(Path.GetFileName(path), stream);
                    url = FeedbackImageUrl(stored.FileId);
                }
                else
                {
                    // The folder is the import's own, so a run of the same import writes the same file again.
                    await _mediaFileStore.CreateFileFromStreamAsync(path, stream, overwrite: true);
                    url = _mediaFileStore.MapPathToPublicUrl(path);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The store's own message can name server paths, so it goes to the log and the row says what failed.
                _logger.LogWarning(exception, "An image of an imported QTI item could not be stored.");
                context.Fail(S["The image {0} could not be stored.", file.Path], S["Not imported from QTI"]);

                return;
            }

            url = WebUtility.HtmlEncode(url);

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

    // The address of a feedback picture, under the tenant's prefix as the media library's are.
    private string FeedbackImageUrl(string fileId)
        => (string.IsNullOrEmpty(_shellSettings.RequestUrlPrefix) ? string.Empty : "/" + _shellSettings.RequestUrlPrefix) + "/" + QtiFeedbackImages.PathOf(fileId);

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
