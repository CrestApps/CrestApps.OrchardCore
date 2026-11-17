using System.Text;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// Creates a text file in a media folder from text the model supplies. It is the chat form of the upload button of
/// the Media Library: the same permissions, extension rule, size limit and file creation pipeline.
/// </summary>
public sealed class SaveMediaTextFileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveMediaTextFile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "path": {
          "type": "string",
          "description": "The path of the file from the root of the media library, with its name and extension, for example exports/prices.csv. The folder must exist. The extension must be a text extension (.css, .csv, .htm, .html, .js, .json, .md, .srt, .svg, .tsv, .txt, .vtt, .xml, .yaml, .yml) that the site allows."
        },
        "text": {
          "type": "string",
          "description": "The content of the file. It is stored as UTF-8 text. The file is served publicly, so it must hold no secret."
        },
        "overwrite": {
          "type": "boolean",
          "description": "Whether a file that already exists at that path is replaced. The default is false."
        }
      },
      "required": [
        "path",
        "text"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a text-based media file (for example .csv, .svg, .css or .js) from the supplied text and returns its path and public URL; replaces an existing file only when overwrite is set. The extension must be allowed for the user and the folder must exist, and the file is served publicly from the site, so never put secrets or unreviewed script in it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await MediaToolSupport.CanManageMediaAsync(services))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("path", out var givenPath))
        {
            return "The path argument is required.";
        }

        if (!arguments.TryGetFirstString("text", out var text))
        {
            return "Nothing was saved: the text argument is required and cannot be empty.";
        }

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();

        if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, givenPath, out var path))
        {
            return $"Nothing was saved: {MediaToolSupport.DescribeInvalidPath(givenPath)}";
        }

        var folder = MediaToolSupport.GetFolder(path);

        if (!await MediaToolSupport.CanManagePathAsync(services, folder))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // A file is stored in a folder that exists, as an upload in the Media Library is.
        if (folder.Length > 0 && await mediaFileStore.GetDirectoryInfoAsync(folder) is null)
        {
            return $"Nothing was saved: there is no media folder '{folder}'. Create the folder in the Media Library first, or use a folder that listMediaFiles returns.";
        }

        // The name is normalized as an uploaded file name is.
        var fileName = Path.GetFileName(path);

        fileName = services.GetService<IMediaNameNormalizerService>()?.NormalizeFileName(fileName) ?? fileName;

        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fileName)) ||
            !MediaToolSupport.TryNormalizeFilePath(mediaFileStore, mediaFileStore.Combine(folder, fileName), out path))
        {
            return $"Nothing was saved: '{Path.GetFileName(givenPath)}' cannot be used as a file name. Give a plain name and extension, for example prices.csv.";
        }

        var extension = Path.GetExtension(fileName);

        if (!MediaToolSupport.TextFileExtensions.Contains(extension))
        {
            return $"Nothing was saved: this tool writes text files only, and '{extension}' is not a text extension. Use one of {string.Join(", ", MediaToolSupport.TextFileExtensions.Order(StringComparer.Ordinal))}.";
        }

        var mediaOptions = services.GetRequiredService<IOptions<MediaOptions>>().Value;

        if (!MediaToolSupport.IsExtensionAllowed(mediaOptions, extension, await MediaToolSupport.CanUploadRestrictedMediaAsync(services)))
        {
            return $"Nothing was saved: {MediaToolSupport.DescribeRefusedExtension(mediaOptions, extension)}";
        }

        var content = Encoding.UTF8.GetBytes(text);

        if (content.Length > mediaOptions.MaxFileSize)
        {
            return $"Nothing was saved: the text is {content.Length} bytes, more than the site's upload limit of {mediaOptions.MaxFileSize} bytes.";
        }

        if (await mediaFileStore.GetDirectoryInfoAsync(path) is not null)
        {
            return $"Nothing was saved: '{path}' is a folder. Give another file name.";
        }

        var overwrite = arguments.GetFirstValueOrDefault("overwrite", false);
        var replaced = await mediaFileStore.GetFileInfoAsync(path) is not null;

        if (replaced && !overwrite)
        {
            return $"Nothing was saved: the media file '{path}' already exists. Give another path, or set overwrite to true to replace it.";
        }

        IFileStoreEntry file;

        try
        {
            using var stream = new MemoryStream(content, writable: false);

            // The file creation pipeline of an upload: the file event handlers can refuse or change the file.
            var createdPath = await mediaFileStore.CreateFileFromStreamAsync(
                services.GetRequiredService<FileCreationService>(),
                path,
                stream,
                overwrite,
                content.LongLength,
                MediaToolSupport.GetMimeType(services, fileName),
                cancellationToken);

            file = await mediaFileStore.GetFileInfoAsync(createdPath);
        }
        catch (FileStoreException exception)
        {
            return $"Nothing was saved: the media library refused the file. {exception.Message}";
        }

        await MediaToolSupport.PreCacheAsync(services, mediaFileStore, file, cancellationToken);

        return AgentToolResults.Json(new
        {
            saved = true,
            replaced,
            file = await MediaToolSupport.DescribeFileAsync(services, mediaFileStore, file),
            note = "The file is served from the site: everyone who may view the folder can read it at its URL.",
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(services.GetRequiredService<ILocalClock>()),
        });
    }
}
