using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Imports;
using CrestApps.OrchardCore.Surveys.Core.Qti;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Reads bank questions from IMS QTI 2.1 and 3.0 items and packages, up to <see cref="QuestionImportUploads.MaxPackageBytes"/>.
/// Images of a package go to the media library, in a folder named after each image's content, so a re-import adds no copy.
/// </summary>
public sealed class QtiQuestionImportFormat : ISurveyImportFormat
{
    /// <summary>
    /// The technical name of the format.
    /// </summary>
    public const string FormatName = "Qti";

    /// <summary>
    /// The media folder images of QTI packages are stored under.
    /// </summary>
    public const string MediaFolder = "surveys/qti";

    private readonly IMediaFileStore _mediaFileStore;
    private readonly MediaOptions _mediaOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QtiQuestionImportFormat"/> class.
    /// </summary>
    /// <param name="mediaFileStore">The media file store.</param>
    /// <param name="mediaOptions">The media options.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public QtiQuestionImportFormat(
        IMediaFileStore mediaFileStore,
        IOptions<MediaOptions> mediaOptions,
        IStringLocalizer<QtiQuestionImportFormat> stringLocalizer)
    {
        _mediaFileStore = mediaFileStore;
        _mediaOptions = mediaOptions.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public string Name => FormatName;

    /// <inheritdoc />
    public string DisplayName => S["QTI package or item (zip or XML)"];

    /// <inheritdoc />
    public IReadOnlyCollection<string> Extensions { get; } = [".zip", ".xml"];

    /// <inheritdoc />
    public Task<SurveyImportResult> ReadAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
        => ReadCoreAsync(fileName, content, storeMedia: true, cancellationToken);

    /// <inheritdoc />
    /// <remarks>The images are checked as for an import, but nothing is written to the media library.</remarks>
    public Task<SurveyImportResult> CheckAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
        => ReadCoreAsync(fileName, content, storeMedia: false, cancellationToken);

    private async Task<SurveyImportResult> ReadCoreAsync(string fileName, Stream content, bool storeMedia, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var result = new SurveyImportResult();

        // The archive reader needs the whole package; the copy stops at the size limit, so an upload of any size is never
        // read whole.
        using var buffer = await QuestionImportUploads.ReadAtMostAsync(content, QuestionImportUploads.MaxPackageBytes, cancellationToken);

        if (buffer is null)
        {
            result.Errors.Add(S["The file is larger than {0} MB.", QuestionImportUploads.MaxPackageBytes / 1024 / 1024]);

            return result;
        }

        var read = QtiReader.Read(buffer, fileName);

        if (!read.Succeeded)
        {
            result.Errors.Add(S["The file could not be read as QTI ({0}).", read.Error]);

            return result;
        }

        foreach (var item in read.Items)
        {
            if (!item.Imported)
            {
                result.Errors.Add(S["Item {0} was not imported ({1}).", item.Identifier ?? item.Path, item.Refusal]);

                continue;
            }

            var question = QtiQuestionMapper.Map(item);

            if (question is null)
            {
                result.Errors.Add(S["Item {0} was not imported (unsupported question).", item.Identifier ?? item.Path]);

                continue;
            }

            var error = await StoreMediaAsync(item, question, storeMedia, cancellationToken);

            if (error is not null)
            {
                result.Errors.Add(S["Item {0} was not imported: {1}", item.Identifier ?? item.Path, error]);

                continue;
            }

            result.Questions.Add(question);
        }

        return result;
    }

    // Checks the images an item refers to and, when storeMedia is set, stores them in the media library and points the
    // question at them. A dry run checks the same images and leaves the question as it was.
    private async Task<string> StoreMediaAsync(QtiItem item, BankQuestion question, bool storeMedia, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(question.Question);

        if (!json.Contains(QtiMediaFile.Scheme, StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var file in item.Media)
        {
            var reference = WebUtility.HtmlEncode(file.Reference);

            if (!json.Contains(reference, StringComparison.Ordinal) && !json.Contains(file.Reference, StringComparison.Ordinal))
            {
                continue;
            }

            var extension = Path.GetExtension(file.Path);

            if (!QtiMediaFile.ImageExtensions.Contains(extension) || !_mediaOptions.AllowedFileExtensions.Contains(extension) || !QtiMediaFile.HasImageSignature(file.Content))
            {
                return S["the image {0} is not a PNG, JPEG, GIF or WebP image the media library accepts.", file.Path];
            }

            if (_mediaOptions.MaxFileSize > 0 && file.Content.LongLength > _mediaOptions.MaxFileSize)
            {
                return S["the image {0} is larger than the media library accepts.", file.Path];
            }

            var path = MediaPath(file);
            var url = string.Empty;

            if (storeMedia)
            {
                // The same image, imported again, is already there under the same path: it is stored once.
                if (await _mediaFileStore.GetFileInfoAsync(path) is null)
                {
                    using var stream = new MemoryStream(file.Content, writable: false);
                    await _mediaFileStore.CreateFileFromStreamAsync(path, stream, overwrite: true);
                }

                url = _mediaFileStore.MapPathToPublicUrl(path);
            }

            json = json.Replace(reference, WebUtility.HtmlEncode(url), StringComparison.Ordinal)
                .Replace(file.Reference, url, StringComparison.Ordinal);
        }

        if (json.Contains(QtiMediaFile.Scheme, StringComparison.Ordinal))
        {
            return S["an image of the item is missing from the package."];
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (storeMedia)
        {
            question.Question = JsonSerializer.Deserialize<SurveyQuestion>(json);
        }

        return null;
    }

    /// <summary>
    /// Gets where an image of a package is stored: in a folder named after its content, so importing the same package
    /// (or another holding the same image) again reuses the file instead of adding a copy.
    /// </summary>
    /// <param name="file">The image.</param>
    /// <returns>The media path.</returns>
    internal static string MediaPath(QtiMediaFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file.Content ?? []), 0, 12).ToLowerInvariant();
        var name = SafePath(file.Path).Split('/')[^1];

        return MediaFolder + "/" + hash + "/" + name;
    }

    private static string SafePath(string packagePath)
    {
        var segments = packagePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment =>
            {
                var builder = new StringBuilder();

                foreach (var character in segment)
                {
                    builder.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_');
                }

                return builder.ToString().Trim('.');
            })
            .Where(segment => segment.Length > 0);

        var path = string.Join('/', segments);

        return path.Length == 0 ? IdGenerator.GenerateId() : path;
    }
}
