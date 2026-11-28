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
/// Downloads a file from a public http or https address into a media folder. It is the chat form of the upload
/// button of the Media Library: the same permissions, extension rule, size limit and file creation pipeline, and
/// <see cref="RemoteMediaDownloader"/> keeps the download off the site's own network.
/// </summary>
public sealed class ImportMediaFromUrlTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "importMediaFromUrl";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "url": {
          "type": "string",
          "description": "The public http or https address of the file, for example https://example.com/press/logo.png. Addresses on the site's own network are refused."
        },
        "folder": {
          "type": "string",
          "description": "The media folder to store the file in, from the root of the media library, for example brand. The folder must exist. Leave it out to store the file in the root folder."
        },
        "fileName": {
          "type": "string",
          "description": "The name to store the file under, with its extension, for example logo.png. Leave it out to use the file name of the address."
        },
        "overwrite": {
          "type": "boolean",
          "description": "Whether a file that already exists at that path is replaced. The default is false."
        }
      },
      "required": [
        "url"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Downloads a file from a public http(s) URL and stores it in a media folder, returning its media path and public URL. Use it when the user gives a link to an image or document; the extension must be allowed, the size within the upload limit, the folder must exist, and an existing file is only replaced when overwrite is set.";

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

        if (!arguments.TryGetFirstString("url", out var url))
        {
            return "The url argument is required.";
        }

        var urlRefusal = RemoteMediaDownloader.CheckUrl(url, out var uri);

        if (urlRefusal is not null)
        {
            return $"Nothing was imported: {urlRefusal}";
        }

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();

        arguments.TryGetFirstString("folder", out var givenFolder);

        if (!MediaToolSupport.TryNormalizeFolderPath(mediaFileStore, givenFolder, out var folder))
        {
            return $"Nothing was imported: {MediaToolSupport.DescribeInvalidPath(givenFolder)}";
        }

        if (!await MediaToolSupport.CanManagePathAsync(services, folder))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // A file is stored in a folder that exists, as an upload in the Media Library is.
        if (folder.Length > 0 && await mediaFileStore.GetDirectoryInfoAsync(folder) is null)
        {
            return $"Nothing was imported: there is no media folder '{folder}'. Create the folder in the Media Library first, or use a folder that listMediaFiles returns.";
        }

        if (!arguments.TryGetFirstString("fileName", out var fileName))
        {
            fileName = Uri.UnescapeDataString(uri.AbsolutePath);
        }

        // Only the name is kept, and it is normalized as an uploaded file name is.
        fileName = Path.GetFileName(mediaFileStore.NormalizePath(fileName)) ?? string.Empty;
        fileName = services.GetService<IMediaNameNormalizerService>()?.NormalizeFileName(fileName) ?? fileName;

        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fileName)) || string.IsNullOrEmpty(Path.GetExtension(fileName)))
        {
            return "Nothing was imported: the file needs a name with an extension, and the address does not end in one. Give fileName with the name and extension to store the file under, for example logo.png.";
        }

        if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, mediaFileStore.Combine(folder, fileName), out var path))
        {
            return $"Nothing was imported: '{fileName}' cannot be used as a file name. Give fileName with a plain name and extension, for example logo.png.";
        }

        var mediaOptions = services.GetRequiredService<IOptions<MediaOptions>>().Value;
        var extension = Path.GetExtension(fileName);

        if (!MediaToolSupport.IsExtensionAllowed(mediaOptions, extension, await MediaToolSupport.CanUploadRestrictedMediaAsync(services)))
        {
            return $"Nothing was imported: {MediaToolSupport.DescribeRefusedExtension(mediaOptions, extension)}";
        }

        if (await mediaFileStore.GetDirectoryInfoAsync(path) is not null)
        {
            return $"Nothing was imported: '{path}' is a folder. Give another fileName.";
        }

        var overwrite = arguments.GetFirstValueOrDefault("overwrite", false);
        var replaced = await mediaFileStore.GetFileInfoAsync(path) is not null;

        if (replaced && !overwrite)
        {
            return $"Nothing was imported: the media file '{path}' already exists. Give another fileName, or set overwrite to true to replace it.";
        }

        await using var download = await services.GetRequiredService<RemoteMediaDownloader>().DownloadAsync(uri, mediaOptions.MaxFileSize, cancellationToken);

        if (download.Error is not null)
        {
            return $"Nothing was imported: {download.Error}";
        }

        // A web page in place of the file is what a sign-in page or an error page looks like.
        if (string.Equals(download.ContentType, "text/html", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".html", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was imported: the address returned a web page, not a '{extension}' file. Give the direct address of the file.";
        }

        IFileStoreEntry file;

        try
        {
            // The file creation pipeline of an upload: the file event handlers can refuse or change the file.
            var createdPath = await mediaFileStore.CreateFileFromStreamAsync(
                services.GetRequiredService<FileCreationService>(),
                path,
                download.Content,
                overwrite,
                download.Length,
                download.ContentType ?? MediaToolSupport.GetMimeType(services, fileName),
                cancellationToken);

            file = await mediaFileStore.GetFileInfoAsync(createdPath);
        }
        catch (FileStoreException exception)
        {
            return $"Nothing was imported: the media library refused the file. {exception.Message}";
        }

        await MediaToolSupport.PreCacheAsync(services, mediaFileStore, file, cancellationToken);

        return AgentToolResults.Json(new
        {
            imported = true,
            replaced,
            file = await MediaToolSupport.DescribeFileAsync(services, mediaFileStore, file),
            source = download.FinalUri.GetLeftPart(UriPartial.Path),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(services.GetRequiredService<ILocalClock>()),
        });
    }
}
