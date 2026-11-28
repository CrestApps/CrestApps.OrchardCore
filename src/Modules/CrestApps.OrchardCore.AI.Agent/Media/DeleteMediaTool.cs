using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.FileStorage;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// Permanently deletes media files through the media store, with the checks of the Media Library. Every path is
/// checked before the first file is deleted, so a refused call deletes nothing. Folders are not deleted yet: the
/// Media Library deletes a folder through its own controller, which this tool cannot call.
/// </summary>
internal sealed class DeleteMediaTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteMedia";

    /// <summary>
    /// The number of files one call deletes.
    /// </summary>
    internal const int MaxFiles = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "paths": {
          "type": "array",
          "minItems": 1,
          "maxItems": 50,
          "items": {
            "type": "string"
          },
          "description": "The paths of the files to delete, from the root of the media library, for example [\"tmp/draft.png\"], from listMediaFiles. At most 50. Folders are not deleted: a folder path is refused."
        }
      },
      "required": [
        "paths"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Permanently deletes one or more media files by path. There is no recycle bin and content that references the files will show broken media, so confirm the exact paths first. Folders cannot be deleted with this tool; delete them in the Media Library.";

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

        if (!arguments.TryGetFirst<string[]>("paths", out var givenPaths) || givenPaths is not { Length: > 0 })
        {
            return "Nothing was deleted: paths must be a list of media file paths.";
        }

        if (givenPaths.Length > MaxFiles)
        {
            return $"Nothing was deleted: one call deletes at most {MaxFiles} files. Delete the files in several calls.";
        }

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();
        var paths = new List<string>();

        foreach (var givenPath in givenPaths)
        {
            if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, givenPath, out var path))
            {
                return $"Nothing was deleted: {MediaToolSupport.DescribeInvalidPath(givenPath)}";
            }

            if (!paths.Contains(path, StringComparer.Ordinal))
            {
                paths.Add(path);
            }
        }

        // As in the Media Library, the folder permission is checked on every path before anything is deleted.
        foreach (var path in paths)
        {
            if (!await MediaToolSupport.CanManagePathAsync(services, path))
            {
                return AIToolCallerAuthorization.NotAllowedMessage;
            }
        }

        var problems = new List<string>();

        foreach (var path in paths)
        {
            if (await mediaFileStore.GetFileInfoAsync(path) is not null)
            {
                continue;
            }

            problems.Add(await mediaFileStore.GetDirectoryInfoAsync(path) is not null
                ? $"'{path}' is a folder; this tool deletes files only. Delete folders in the Media Library."
                : $"There is no media file '{path}'.");
        }

        if (problems.Count > 0)
        {
            return $"Nothing was deleted: {string.Join(" ", problems)} Use listMediaFiles to find the paths of the files.";
        }

        var deleted = new List<string>();
        var failed = new List<object>();

        foreach (var path in paths)
        {
            try
            {
                if (await mediaFileStore.TryDeleteFileAsync(path))
                {
                    deleted.Add(path);
                }
                else
                {
                    failed.Add(new
                    {
                        path,
                        error = "The file was not found any more.",
                    });
                }
            }
            catch (FileStoreException exception)
            {
                failed.Add(new
                {
                    path,
                    error = exception.Message,
                });
            }
        }

        return AgentToolResults.Json(new
        {
            deleted,
            deletedCount = deleted.Count,
            failed = failed.Count > 0
                ? failed
                : null,
            note = deleted.Count > 0
                ? "The files cannot be restored. Content that referenced them now shows broken media."
                : null,
        });
    }
}
