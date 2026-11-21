using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Qti;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Reads bank questions from IMS QTI 2.1 and 3.0 items and packages. Images of a package go to the media library, in a
/// folder of the import's own.
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
    public async Task<SurveyImportResult> ReadAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var result = new SurveyImportResult();

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var read = QtiReader.Read(buffer, fileName);

        if (!read.Succeeded)
        {
            result.Errors.Add(S["The file could not be read as QTI ({0}).", read.Error]);

            return result;
        }

        var folder = MediaFolder + "/" + IdGenerator.GenerateId().ToLowerInvariant();

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

            var error = await StoreMediaAsync(item, question, folder, cancellationToken);

            if (error is not null)
            {
                result.Errors.Add(S["Item {0} was not imported: {1}", item.Identifier ?? item.Path, error]);

                continue;
            }

            result.Questions.Add(question);
        }

        return result;
    }

    private async Task<string> StoreMediaAsync(QtiItem item, BankQuestion question, string folder, CancellationToken cancellationToken)
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

            var path = folder + "/" + SafePath(file.Path);

            using (var stream = new MemoryStream(file.Content, writable: false))
            {
                await _mediaFileStore.CreateFileFromStreamAsync(path, stream, overwrite: true);
            }

            var url = _mediaFileStore.MapPathToPublicUrl(path);
            json = json.Replace(reference, WebUtility.HtmlEncode(url), StringComparison.Ordinal)
                .Replace(file.Reference, url, StringComparison.Ordinal);
        }

        if (json.Contains(QtiMediaFile.Scheme, StringComparison.Ordinal))
        {
            return S["an image of the item is missing from the package."];
        }

        cancellationToken.ThrowIfCancellationRequested();
        question.Question = JsonSerializer.Deserialize<SurveyQuestion>(json);

        return null;
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
