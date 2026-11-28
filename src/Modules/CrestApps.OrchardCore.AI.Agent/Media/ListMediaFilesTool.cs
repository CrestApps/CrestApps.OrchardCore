using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// Lists the sub-folders and the files of a media folder, as the Media Library does: through the media store, and
/// only the folders the caller may manage.
/// </summary>
public sealed class ListMediaFilesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listMediaFiles";

    /// <summary>
    /// The number of files the tool reads before it stops, so a recursive listing of a large library stays bounded.
    /// </summary>
    internal const int MaxScannedFiles = 5000;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "path": {
          "type": "string",
          "description": "The media folder to list, from the root of the media library, for example blog/2026. Leave it out to list the root folder."
        },
        "name": {
          "type": "string",
          "description": "Keep the files whose name contains this text. The case is ignored."
        },
        "extensions": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "description": "Keep the files with one of these extensions, for example [\".png\", \".jpg\"]. Leave it out to list every file."
        },
        "recursive": {
          "type": "boolean",
          "description": "Whether the files of the sub-folders are listed too. The default is false."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page of files to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of files on a page. The default is 20 and the maximum is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the sub-folders and files of a media folder (the root when no path is given), with optional name and extension filter, recursion and paging; each file has its path, size, MIME type, date and public URL. Use it to find a file's path before using it in content or before moving or deleting it.";

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

        var mediaFileStore = services.GetRequiredService<IMediaFileStore>();

        arguments.TryGetFirstString("path", out var givenPath);

        if (!MediaToolSupport.TryNormalizeFolderPath(mediaFileStore, givenPath, out var path))
        {
            return $"Nothing was listed: {MediaToolSupport.DescribeInvalidPath(givenPath)}";
        }

        if (!await MediaToolSupport.CanManagePathAsync(services, path))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // The root folder always exists.
        if (path.Length > 0 && await mediaFileStore.GetDirectoryInfoAsync(path) is null)
        {
            return $"Nothing was listed: there is no media folder '{path}'. List its parent folder to see the folders that exist.";
        }

        HashSet<string> extensions = null;

        if (arguments.TryGetFirst<string[]>("extensions", out var givenExtensions) && givenExtensions is { Length: > 0 })
        {
            extensions = givenExtensions
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(extension => extension.Trim())
                .Select(extension => $".{extension.TrimStart('.')}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        arguments.TryGetFirstString("name", out var name);
        name = name?.Trim();

        var recursive = arguments.GetFirstValueOrDefault("recursive", false);
        var folders = new List<object>();
        var files = new List<IFileStoreEntry>();
        var scanned = 0;
        var truncated = false;
        var pending = new Queue<string>();

        pending.Enqueue(path);

        while (pending.Count > 0 && !truncated)
        {
            var current = pending.Dequeue();

            await foreach (var entry in mediaFileStore.GetDirectoryContentAsync(current).WithCancellation(cancellationToken))
            {
                if (entry.IsDirectory)
                {
                    // As in the Media Library, a folder the caller may not manage is neither listed nor entered.
                    if (!await MediaToolSupport.CanManagePathAsync(services, entry.Path))
                    {
                        continue;
                    }

                    if (current == path)
                    {
                        folders.Add(new
                        {
                            name = entry.Name,
                            path = mediaFileStore.NormalizePath(entry.Path),
                        });
                    }

                    if (recursive)
                    {
                        pending.Enqueue(entry.Path);
                    }

                    continue;
                }

                if (++scanned > MaxScannedFiles)
                {
                    truncated = true;

                    break;
                }

                if (extensions is not null && !extensions.Contains(Path.GetExtension(entry.Name)))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(name) && !entry.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                files.Add(entry);
            }
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var items = new List<MediaFileSummary>();

        foreach (var file in files
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .Skip((page - 1) * pageSize)
            .Take(pageSize))
        {
            items.Add(await MediaToolSupport.DescribeFileAsync(services, mediaFileStore, file));
        }

        return AgentToolResults.Json(new
        {
            path,
            folders,
            items,
            total = files.Count,
            page,
            pageSize,
            truncated = truncated
                ? true
                : (bool?)null,
            note = truncated
                ? $"The listing stopped after {MaxScannedFiles} files, so the total is not complete. List one sub-folder at a time."
                : null,
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(services.GetRequiredService<ILocalClock>()),
        });
    }
}
