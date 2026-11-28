using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.DncRegistry.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DncRegistry.AI;

/// <summary>
/// Creates a local do-not-call list for a country and queues it for the background import, as the Upload form of the
/// Local DNC Registry page does (<see cref="ILocalDncListManager.QueueImportAsync"/>). The numbers come either inline,
/// written as a one-column file the way the <c>LocalDncLists</c> recipe step writes them, or from a CSV file already in
/// the media library; never as raw file content.
/// </summary>
internal sealed class ImportLocalDncListTool : DncToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "importLocalDncList";

    /// <summary>
    /// The most numbers one call takes inline.
    /// </summary>
    public const int MaxInlineNumbers = 1000;

    /// <summary>
    /// The largest media file a call imports.
    /// </summary>
    public const long MaxFileBytes = 50L * 1024 * 1024;

    private const string PublicPrefix = "media/";

    private static readonly string[] _fileExtensions = [".csv", ".txt"];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The name of the new list, for example Complaints June."
        },
        "countryCode": {
          "type": "string",
          "description": "The two-letter code of the list's country, for example US. Numbers without a country code are read as this country's."
        },
        "phoneNumbers": {
          "type": "array",
          "items": { "type": "string" },
          "maxItems": 1000,
          "description": "The phone numbers to put on the list, at most 1000. Give these or mediaPath, not both."
        },
        "mediaPath": {
          "type": "string",
          "description": "The path of a CSV file in the site's media library, one number per row, for example dnc/state-list.csv. Give this or phoneNumbers, not both."
        }
      },
      "required": ["name", "countryCode"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a local do-not-call list for a country from phone numbers given inline or from a CSV file in the media library, and queues it for background import. The numbers are screened out of calls once the import finishes.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => DncRegistryPermissions.ManageDncRegistrySettings;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(Arguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var name = arguments.GetString("name");
        var countryCode = arguments.GetString("countryCode")?.ToUpperInvariant();

        if (name is null)
        {
            return "Nothing was imported: a list name is required.";
        }

        if (countryCode is null || countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetterUpper) ||
            services.GetService<IPhoneNumberService>() is { } phoneNumbers && phoneNumbers.GetCountryCode(countryCode) <= 0)
        {
            return "Nothing was imported: countryCode must be the two-letter code of a country, for example US.";
        }

        var numbers = arguments.GetStringList("phoneNumbers");
        var mediaPath = arguments.GetString("mediaPath");

        if ((numbers.Count == 0) == (mediaPath is null))
        {
            return "Nothing was imported: give either phoneNumbers or mediaPath.";
        }

        var manager = services.GetRequiredService<ILocalDncListManager>();
        Models.LocalDncList list;

        if (mediaPath is not null)
        {
            var (store, path, error) = await FindFileAsync(services, user, mediaPath);

            if (error is not null)
            {
                return $"Nothing was imported: {error}";
            }

            await using var stream = await store.GetFileStreamAsync(path);

            list = await manager.QueueImportAsync(name, countryCode, Path.GetFileName(path), stream, cancellationToken);
        }
        else
        {
            if (numbers.Count > MaxInlineNumbers)
            {
                return $"Nothing was imported: give at most {MaxInlineNumbers} numbers per call, or import a CSV file from the media library.";
            }

            var invalid = numbers.Where(number => !number.All(IsPhoneCharacter) || !number.Any(char.IsAsciiDigit)).ToList();

            if (invalid.Count > 0)
            {
                return $"Nothing was imported: these are not phone numbers: {string.Join(", ", invalid.Take(10))}.";
            }

            // One number per row under a header, the shape of an uploaded file, so the list goes through the same import.
            var csv = new StringBuilder("PhoneNumber");

            foreach (var number in numbers)
            {
                csv.Append('\n').Append(number);
            }

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

            list = await manager.QueueImportAsync(name, countryCode, $"{name}.csv", stream, cancellationToken);
        }

        var started = LocalDncListJobs.QueueImport(list.ListId);

        return Json(new
        {
            queued = await ListLocalDncListsTool.DescribeAsync(list, services.GetService<ILocalClock>()),
            note = started
                ? "The list was created and its import starts in the background when this reply ends. Numbers are screened once the import finishes; check progress with listLocalDncLists."
                : "The list was created and queued; the import starts within about 10 minutes. Check progress with listLocalDncLists.",
        });
    }

    private static bool IsPhoneCharacter(char value)
        => char.IsAsciiDigit(value) || value is '+' or ' ' or '-' or '(' or ')' or '.';

    // The CSV file the numbers are read from: a media library file the caller may manage.
    private static async Task<(IMediaFileStore Store, string Path, string Error)> FindFileAsync(IServiceProvider services, ClaimsPrincipal user, string mediaPath)
    {
        var store = services.GetService<IMediaFileStore>();

        if (store is null)
        {
            return (null, null, "the media library is not available on this site. Give the numbers inline instead.");
        }

        if (!TryNormalizePath(store, mediaPath, out var path))
        {
            return (null, null, $"'{mediaPath}' is not a media path. Give the path from the root of the media library, for example dnc/state-list.csv, without a drive or a '..' segment, and not URL-encoded.");
        }

        var file = await store.GetFileInfoAsync(path);

        // A path copied from a file's public address starts with the media library's address prefix.
        if ((file is null || file.IsDirectory) && path.StartsWith(PublicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var withoutPrefix = path[PublicPrefix.Length..];
            var candidate = await store.GetFileInfoAsync(withoutPrefix);

            if (candidate is not null && !candidate.IsDirectory)
            {
                path = withoutPrefix;
                file = candidate;
            }
        }

        if (file is null || file.IsDirectory)
        {
            return (null, null, $"there is no media file at '{path}'.");
        }

        // The Media Library checks the folder permission with the path as the resource.
        var authorizationService = services.GetRequiredService<IAuthorizationService>();

        if (!await authorizationService.AuthorizeAsync(user, MediaPermissions.ManageMedia) ||
            !await authorizationService.AuthorizeAsync(user, MediaPermissions.ManageMediaFolder, (object)path))
        {
            return (null, null, $"you are not allowed to use the media file '{path}'.");
        }

        if (!_fileExtensions.Contains(Path.GetExtension(file.Name), StringComparer.OrdinalIgnoreCase))
        {
            return (null, null, $"'{path}' is not a CSV file. Use a .csv or .txt file with one number per row.");
        }

        if (file.Length <= 0)
        {
            return (null, null, $"the media file '{path}' is empty.");
        }

        if (file.Length > MaxFileBytes)
        {
            return (null, null, $"the media file '{path}' is larger than 50 MB. Upload it on the Local DNC Registry page instead.");
        }

        return (store, path, null);
    }

    private static bool TryNormalizePath(IMediaFileStore store, string value, out string path)
    {
        path = store.NormalizePath(value.Trim().Replace('\\', '/').Trim('/')) ?? string.Empty;

        // A path the permission check could read as another folder than the store does is refused: a drive or scheme,
        // a '.' or '..' segment, an empty segment and URL-encoded or control characters.
        return path.Length > 0 &&
            !path.Contains(':', StringComparison.Ordinal) &&
            !path.Contains('%', StringComparison.Ordinal) &&
            !path.Any(char.IsControl) &&
            path.Split('/').All(segment => !string.IsNullOrWhiteSpace(segment) && segment is not "." and not ".." && !segment.EndsWith('.') && !segment.EndsWith(' '));
    }
}
