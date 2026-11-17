using System.Text;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Media.Indexing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// Returns the details of one media file and, when asked, its text. The text comes from the text providers the media
/// indexing features register (text, Markdown, PDF, Word, PowerPoint), or from the file itself for a small plain-text
/// file.
/// </summary>
public sealed class GetMediaFileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getMediaFile";

    /// <summary>
    /// The number of characters of text the tool returns.
    /// </summary>
    internal const int MaxTextLength = 20000;

    /// <summary>
    /// The size, in bytes, of the largest document the tool gives to a text provider.
    /// </summary>
    internal const long MaxDocumentSize = 10 * 1024 * 1024;

    /// <summary>
    /// The size, in bytes, of the largest plain-text file the tool reads without a text provider.
    /// </summary>
    internal const long MaxPlainTextSize = 1024 * 1024;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "path": {
          "type": "string",
          "description": "The path of the file from the root of the media library, for example brand/logo.png, from listMediaFiles."
        },
        "includeText": {
          "type": "boolean",
          "description": "Whether the text of the file is returned too. The default is false. Only the first 20000 characters are returned."
        }
      },
      "required": [
        "path"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one media file's details and public URL and, when asked, its text content (text, Markdown, PDF, Word and PowerPoint when the matching media indexing feature is enabled; small plain-text files otherwise). Use it to check that a file exists or to read a document stored in the library.";

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

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();

        if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, givenPath, out var path))
        {
            return MediaToolSupport.DescribeInvalidPath(givenPath);
        }

        if (!await MediaToolSupport.CanManagePathAsync(services, path))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var file = await mediaFileStore.GetFileInfoAsync(path);

        if (file is null)
        {
            return $"There is no media file '{path}'. Use listMediaFiles to find the path of the file.";
        }

        string text = null;
        string textNote = null;
        var textTruncated = false;

        if (arguments.GetFirstValueOrDefault("includeText", false))
        {
            (text, textNote) = await ReadTextAsync(services, mediaFileStore, file, cancellationToken);

            if (text is not null && text.Length > MaxTextLength)
            {
                text = text[..MaxTextLength];
                textTruncated = true;
            }
        }

        return AgentToolResults.Json(new
        {
            file = await MediaToolSupport.DescribeFileAsync(services, mediaFileStore, file),
            text,
            textTruncated = textTruncated
                ? true
                : (bool?)null,
            textNote,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(services.GetRequiredService<ILocalClock>()),
        });
    }

    private static async Task<(string Text, string Note)> ReadTextAsync(
        IServiceProvider services,
        IMediaFileStore mediaFileStore,
        IFileStoreEntry file,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.Name);

        if (string.IsNullOrEmpty(extension))
        {
            return (null, "The file has no extension, so its text cannot be read.");
        }

        // The text providers of the media indexing features: text and Markdown, PDF, Word and PowerPoint.
        var providerType = services.GetService<IOptions<MediaFileIndexingOptions>>()?.Value.GetRegisteredMediaFileTextProvider(extension);

        try
        {
            if (providerType is not null)
            {
                if (file.Length > MaxDocumentSize)
                {
                    return (null, $"The file is larger than {MaxDocumentSize / (1024 * 1024)} MB, so its text is not read.");
                }

                await using var stream = await mediaFileStore.GetFileStreamAsync(file);

                var provider = (IMediaFileTextProvider)ActivatorUtilities.CreateInstance(services, providerType);

                return (await provider.GetTextAsync(file.Path, stream) ?? string.Empty, null);
            }

            if (!MediaToolSupport.TextFileExtensions.Contains(extension))
            {
                return (null, $"The text of a '{extension}' file cannot be read. The media indexing features add PDF, Word and PowerPoint; they are not enabled for this extension.");
            }

            if (file.Length > MaxPlainTextSize)
            {
                return (null, $"The file is larger than {MaxPlainTextSize / 1024} KB, so its text is not read.");
            }

            await using var textStream = await mediaFileStore.GetFileStreamAsync(file);
            using var reader = new StreamReader(textStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            // One character more than the limit shows that the text was cut.
            var buffer = new char[MaxTextLength + 1];
            var length = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);

            return (new string(buffer, 0, length), null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A damaged or protected document makes its text provider throw. The details of the file are still returned.
            services.GetService<ILoggerFactory>()?
                .CreateLogger<GetMediaFileTool>()
                .LogWarning(exception, "The text of a media file could not be read.");

            return (null, "The text of the file could not be read. The file may be damaged or protected.");
        }
    }
}
