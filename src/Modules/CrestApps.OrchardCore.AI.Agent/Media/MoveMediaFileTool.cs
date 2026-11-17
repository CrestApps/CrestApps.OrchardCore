using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// Moves or renames media files through the media store, with the checks of the Media Library. Every file is checked
/// before the first one is moved, so a refused call moves nothing.
/// </summary>
public sealed class MoveMediaFileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "moveMediaFile";

    /// <summary>
    /// The number of files one call moves.
    /// </summary>
    internal const int MaxFiles = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "files": {
          "type": "array",
          "minItems": 1,
          "maxItems": 50,
          "description": "The files to move or rename, at most 50.",
          "items": {
            "type": "object",
            "properties": {
              "path": {
                "type": "string",
                "description": "The current path of the file from the root of the media library, for example brand/logo-old.png, from listMediaFiles."
              },
              "newPath": {
                "type": "string",
                "description": "The new path of the file, with its name and extension, for example brand/logo-2025.png (rename) or archive/logo-old.png (move). The folder must exist and no file may be at that path."
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
    public override string Description => "Moves or renames media files: give each file's current path and its new path (same folder with a new name to rename). Content that references the old path is not updated, so report the references that need fixing; fails when the target exists, its folder does not exist or the new extension is not allowed.";

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

        if (!AgentToolArguments.TryReadJsonArray(arguments, "files", out var files) || files is not { Count: > 0 })
        {
            return "Nothing was moved: files must be a list of objects, each with path and newPath.";
        }

        if (files.Count > MaxFiles)
        {
            return $"Nothing was moved: one call moves at most {MaxFiles} files. Move the files in several calls.";
        }

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();
        var moves = new List<(string Path, string NewPath)>();

        foreach (var entry in files)
        {
            var givenPath = ReadString(entry, "path");
            var givenNewPath = ReadString(entry, "newPath");

            if (string.IsNullOrWhiteSpace(givenPath) || string.IsNullOrWhiteSpace(givenNewPath))
            {
                return "Nothing was moved: every entry of files needs path and newPath.";
            }

            if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, givenPath, out var path))
            {
                return $"Nothing was moved: {MediaToolSupport.DescribeInvalidPath(givenPath)}";
            }

            if (!MediaToolSupport.TryNormalizeFilePath(mediaFileStore, givenNewPath, out var newPath))
            {
                return $"Nothing was moved: {MediaToolSupport.DescribeInvalidPath(givenNewPath)}";
            }

            moves.Add((path, newPath));
        }

        // The Media Library checks the folder permission on the old and on the new path of every file.
        foreach (var (path, newPath) in moves)
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

        foreach (var (path, newPath) in moves)
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
                problems.Add($"More than one file is moved to '{newPath}'.");
            }
            else if (await MediaToolSupport.CheckNewFilePathAsync(mediaFileStore, mediaOptions, newPath, canUploadRestrictedMedia) is { } problem)
            {
                problems.Add(problem);
            }
        }

        if (problems.Count > 0)
        {
            return $"Nothing was moved: {string.Join(" ", problems)}";
        }

        var moved = new List<object>();
        var failed = new List<object>();

        foreach (var (path, newPath) in moves)
        {
            try
            {
                await mediaFileStore.MoveFileAsync(path, newPath);

                var file = await mediaFileStore.GetFileInfoAsync(newPath);

                await MediaToolSupport.PreCacheAsync(services, mediaFileStore, file, cancellationToken);

                moved.Add(new
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
            moved,
            movedCount = moved.Count,
            failed = failed.Count > 0
                ? failed
                : null,
            note = moved.Count > 0
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
