using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// Moves, renames or copies media files through the media store, with the checks of the Media Library. Every file is
/// checked before the first one is moved or copied, so a refused call changes nothing.
/// </summary>
internal sealed class MoveMediaFilesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "moveMediaFiles";

    /// <summary>
    /// The action that moves or renames the files.
    /// </summary>
    public const string MoveAction = "move";

    /// <summary>
    /// The action that copies the files.
    /// </summary>
    public const string CopyAction = "copy";

    /// <summary>
    /// The number of files one call moves or copies.
    /// </summary>
    internal const int MaxFiles = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "action": {
          "type": "string",
          "enum": [ "move", "copy" ],
          "description": "move to move or rename the files (the old paths stop existing), copy to make copies at the new paths. The default is move."
        },
        "files": {
          "type": "array",
          "minItems": 1,
          "maxItems": 50,
          "description": "The files to move, rename or copy, at most 50.",
          "items": {
            "type": "object",
            "properties": {
              "path": {
                "type": "string",
                "description": "The current path of the file from the root of the media library, for example brand/logo-old.png, from listMediaFiles."
              },
              "newPath": {
                "type": "string",
                "description": "The new path of the file or of its copy, with its name and extension, for example brand/logo-2025.png (rename) or archive/logo-old.png (move). The folder must exist and no file may be at that path."
              }
            },
            "required": [
              "path",
              "newPath"
            ],
            "additionalProperties": false
          }
        }
      },
      "required": [
        "files"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Moves, renames or copies media files: give the action (move or copy) and each file's current path and new path (same folder with a new name to rename). A move does not update content that references the old path, so report the references that need fixing. Fails when a target exists, its folder does not exist or the new extension is not allowed.";

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

        var action = arguments.TryGetFirstString("action", out var givenAction)
            ? givenAction.Trim().ToLowerInvariant()
            : MoveAction;

        if (action is not MoveAction and not CopyAction)
        {
            return $"Nothing was changed: '{givenAction}' is not an action. Use move or copy.";
        }

        var copy = action == CopyAction;
        var verb = copy ? "copied" : "moved";

        if (!AgentToolArguments.TryReadJsonArray(arguments, "files", out var files) || files is not { Count: > 0 })
        {
            return $"Nothing was {verb}: files must be a list of objects, each with path and newPath.";
        }

        if (files.Count > MaxFiles)
        {
            return $"Nothing was {verb}: one call handles at most {MaxFiles} files. Use several calls.";
        }

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();
        var changes = new List<(string Path, string NewPath)>();

        foreach (var entry in files)
        {
            var givenPath = ReadString(entry, "path");
            var givenNewPath = ReadString(entry, "newPath");

            if (string.IsNullOrWhiteSpace(givenPath) || string.IsNullOrWhiteSpace(givenNewPath))
            {
                return $"Nothing was {verb}: every entry of files needs path and newPath.";
            }

            if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, givenPath, out var path))
            {
                return $"Nothing was {verb}: {MediaToolSupport.DescribeInvalidPath(givenPath)}";
            }

            if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, givenNewPath, out var newPath))
            {
                return $"Nothing was {verb}: {MediaToolSupport.DescribeInvalidPath(givenNewPath)}";
            }

            changes.Add((path, newPath));
        }

        // The Media Library checks the folder permission on the old and on the new path of every file, for a move and
        // for a copy.
        foreach (var (path, newPath) in changes)
        {
            if (!await MediaToolSupport.CanManagePathAsync(services, path) || !await MediaToolSupport.CanManagePathAsync(services, newPath))
            {
                return AIToolCallerAuthorization.NotAllowedMessage;
            }
        }

        var mediaOptions = services.GetRequiredService<IOptions<MediaOptions>>().Value;
        var canUploadRestrictedMedia = await MediaToolSupport.CanUploadRestrictedMediaAsync(services);
        var problems = new List<string>();
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, newPath) in changes)
        {
            if (string.Equals(path, newPath, StringComparison.Ordinal))
            {
                problems.Add($"'{path}' has the same old and new path.");
            }
            else if (await mediaFileStore.GetFileInfoAsync(path) is null)
            {
                problems.Add($"There is no media file '{path}'. Use listMediaFiles to find the path of the file.");
            }
            else if (!targets.Add(newPath))
            {
                problems.Add($"More than one file goes to '{newPath}'.");
            }
            else if (await MediaToolSupport.CheckNewFilePathAsync(mediaFileStore, mediaOptions, newPath, canUploadRestrictedMedia) is { } problem)
            {
                problems.Add(problem);
            }
        }

        if (problems.Count > 0)
        {
            return $"Nothing was {verb}: {string.Join(" ", problems)}";
        }

        var done = new List<object>();
        var failed = new List<object>();

        foreach (var (path, newPath) in changes)
        {
            try
            {
                if (copy)
                {
                    await mediaFileStore.CopyFileAsync(path, newPath);
                }
                else
                {
                    await mediaFileStore.MoveFileAsync(path, newPath);

                    // As the Media Library does after a move, a remote store's local cache gets the moved file.
                    var file = await mediaFileStore.GetFileInfoAsync(newPath);

                    await MediaToolSupport.PreCacheAsync(services, mediaFileStore, file, cancellationToken);
                }

                done.Add(new
                {
                    from = path,
                    to = newPath,
                    url = mediaFileStore.MapPathToPublicUrl(newPath),
                });
            }
            catch (FileStoreException exception)
            {
                failed.Add(new
                {
                    from = path,
                    to = newPath,
                    error = exception.Message,
                });
            }
        }

        return AgentToolResults.Json(new
        {
            action,
            files = done,
            count = done.Count,
            failed = failed.Count > 0
                ? failed
                : null,
            note = !copy && done.Count > 0
                ? "Content that references an old path is not updated: media fields and HTML or Markdown bodies store the path as text. Find the content that uses the old paths and update it."
                : null,
        });
    }

    private static string ReadString(JsonNode node, string name)
    {
        return node is JsonObject entry && entry[name] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
    }
}
