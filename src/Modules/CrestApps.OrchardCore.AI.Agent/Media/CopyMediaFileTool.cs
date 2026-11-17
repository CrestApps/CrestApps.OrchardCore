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
/// Copies a media file to a new path through the media store, with the checks of the Media Library.
/// </summary>
public sealed class CopyMediaFileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "copyMediaFile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "path": {
          "type": "string",
          "description": "The path of the file to copy, from the root of the media library, for example brand/logo.png, from listMediaFiles."
        },
        "newPath": {
          "type": "string",
          "description": "The path of the copy, with its name and extension, for example brand/logo-copy.png. The folder must exist and no file may be at that path."
        }
      },
      "required": [
        "path",
        "newPath"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Copies a media file to a new path and returns the copy's path and public URL. Fails when the target exists, its folder does not exist or its extension is not allowed.";

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

        if (!arguments.TryGetFirstString("newPath", out var givenNewPath))
        {
            return "The newPath argument is required.";
        }

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();

        if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, givenPath, out var path))
        {
            return $"Nothing was copied: {MediaToolSupport.DescribeInvalidPath(givenPath)}";
        }

        if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, givenNewPath, out var newPath))
        {
            return $"Nothing was copied: {MediaToolSupport.DescribeInvalidPath(givenNewPath)}";
        }

        // The Media Library checks the folder permission on the path of the file and on the path of its copy.
        if (!await MediaToolSupport.CanManagePathAsync(services, path) || !await MediaToolSupport.CanManagePathAsync(services, newPath))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (await mediaFileStore.GetFileInfoAsync(path) is null)
        {
            return $"Nothing was copied: there is no media file '{path}'. Use listMediaFiles to find the path of the file.";
        }

        var mediaOptions = services.GetRequiredService<IOptions<MediaOptions>>().Value;
        var problem = await MediaToolSupport.CheckNewFilePathAsync(mediaFileStore, mediaOptions, newPath, await MediaToolSupport.CanUploadRestrictedMediaAsync(services));

        if (problem is not null)
        {
            return $"Nothing was copied: {problem}";
        }

        IFileStoreEntry copy;

        try
        {
            await mediaFileStore.CopyFileAsync(path, newPath);

            copy = await mediaFileStore.GetFileInfoAsync(newPath);
        }
        catch (FileStoreException exception)
        {
            return $"Nothing was copied: the media library refused the copy. {exception.Message}";
        }

        return AgentToolResults.Json(new
        {
            copied = true,
            from = path,
            file = await MediaToolSupport.DescribeFileAsync(services, mediaFileStore, copy),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(services.GetRequiredService<ILocalClock>()),
        });
    }
}
