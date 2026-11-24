using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Creates or updates a clip of the voice media library, as the Voice media editor does, with its audio taken from a
/// file already in the site's media library instead of an upload. The audio goes through the editor's own upload
/// rules (<see cref="VoiceMediaAudioUpload"/>).
/// </summary>
internal sealed class SaveVoiceMediaTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveVoiceMedia";

    // The address prefix of the media library's files, which a path copied from a file's address starts with.
    private const string PublicPrefix = "media/";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id or name of the clip to update. Leave it out to create a clip."
        },
        "name": {
          "type": "string",
          "description": "The clip's name, unique in the library. Required for a new clip."
        },
        "description": {
          "type": "string",
          "description": "What the clip is for. An empty text clears it."
        },
        "mediaPath": {
          "type": "string",
          "description": "The path of an audio file in the site's media library (mp3, wav or ogg, at most 20 MB), for example audio/holiday.wav. Its audio replaces the clip's audio and is copied to the telephony provider."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a voice media clip from an audio file that is already in the site's media library (path), or renames one. Audio is copied to the telephony provider, so it needs a provider or media storage that can host it. Reading the file also needs the right to manage its media folder.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ManageVoiceMedia;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = new ContactCenterToolArguments(arguments);
        var manager = services.GetRequiredService<IVoiceMediaItemManager>();
        var id = reader.GetString("id");
        var mediaPath = reader.GetString("mediaPath");

        VoiceMediaItem clip;

        if (id is null)
        {
            if (reader.GetString("name") is null)
            {
                return "Nothing was saved: give the new clip a name.";
            }

            clip = await manager.NewAsync(cancellationToken: cancellationToken);
        }
        else
        {
            clip = await ContactCenterSetupLookups.FindAsync(manager, id, record => record.Name, cancellationToken);

            if (clip is null)
            {
                return $"Nothing was saved: no voice media clip has the id or name '{id}'. Use listVoiceMedia to find it.";
            }
        }

        if (!reader.Has("name") && !reader.Has("description") && mediaPath is null)
        {
            return "Nothing was saved: give a new name, description or mediaPath.";
        }

        MediaAudio audio = null;

        if (mediaPath is not null)
        {
            var (file, error) = await FindAudioAsync(services, user, mediaPath);

            if (file is null)
            {
                return $"Nothing was saved: {error}";
            }

            audio = file;
        }

        var originalName = clip.Name;
        var originalDescription = clip.Description;

        if (reader.Has("name"))
        {
            clip.Name = reader.GetString("name");
        }

        if (reader.Has("description"))
        {
            clip.Description = reader.GetString("description");
        }

        // The library's rules (a name, unique) are checked before any audio is sent to a provider.
        var validation = await manager.ValidateAsync(clip, cancellationToken);

        if (!validation.Succeeded)
        {
            Restore(clip, originalName, originalDescription);

            return CatalogWriteResult<VoiceMediaItem>.Invalid(validation).Message;
        }

        VoiceMediaAudioUploadResult upload = null;

        if (audio is not null)
        {
            await using (var stream = await audio.Store.GetFileStreamAsync(audio.Path))
            {
                upload = await VoiceMediaAudioUpload.UploadAsync(
                    services.GetRequiredService<IVoiceMediaProvisioningService>(),
                    clip,
                    stream,
                    audio.Length,
                    audio.ContentType,
                    VoiceMediaUploadFormat.Resolve(audio.Path, audio.ContentType),
                    CancellationToken.None);
            }

            if (!upload.Succeeded)
            {
                Restore(clip, originalName, originalDescription);

                return "Nothing was saved: " + upload.Status switch
                {
                    VoiceMediaAudioUploadStatus.TooLarge => "the audio is too large. It must be 20 MB or less.",
                    VoiceMediaAudioUploadStatus.UnsupportedFormat => "the audio format is not supported. Use an mp3, wav or ogg file.",
                    VoiceMediaAudioUploadStatus.Unavailable => "audio uploads are unavailable because no media storage and no telephony provider capable of hosting media is configured.",
                    _ => "the audio could not be uploaded. Try again.",
                };
            }
        }

        if (id is null)
        {
            await manager.CreateAsync(clip, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(clip, cancellationToken: cancellationToken);
        }

        var warnings = new List<string>();

        if (upload?.Store is { } store)
        {
            if (store.ProviderUploadFailed)
            {
                warnings.Add($"The audio was stored, but {store.ProviderName} did not accept a copy. The provider is given one when the clip is next played, or use resyncVoiceMedia.");
            }
            else if (!store.MasterStored)
            {
                warnings.Add($"The audio was sent to {store.ProviderName}, but no master copy could be kept. Keep the original file: the clip has to be uploaded again if the provider changes.");
            }
        }

        return Json(new
        {
            saved = true,
            created = id is null,
            audioReplaced = upload is not null,
            warnings,
            clip = await ListVoiceMediaTool.DescribeAsync(clip, services.GetService<ILocalClock>()),
        });
    }

    private static void Restore(VoiceMediaItem clip, string name, string description)
    {
        clip.Name = name;
        clip.Description = description;
    }

    // The file the audio is read from: a media library file the caller may manage, of an audio type the library takes.
    private static async Task<(MediaAudio File, string Error)> FindAudioAsync(IServiceProvider services, ClaimsPrincipal user, string mediaPath)
    {
        var store = services.GetService<IMediaFileStore>();

        if (store is null)
        {
            return (null, "the media library is not available on this site, so there is no audio file to read.");
        }

        if (!TryNormalizePath(store, mediaPath, out var path))
        {
            return (null, $"'{mediaPath}' is not a media path. Give the path from the root of the media library, for example audio/holiday.wav, without a drive or a '..' segment, and not URL-encoded.");
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
            return (null, $"there is no media file at '{path}'.");
        }

        // The Media Library checks the folder permission with the path as the resource, which also covers the attached
        // media folder, the user's own folder and the folders of other users.
        var authorizationService = services.GetRequiredService<IAuthorizationService>();

        if (!await authorizationService.AuthorizeAsync(user, MediaPermissions.ManageMedia) ||
            !await authorizationService.AuthorizeAsync(user, MediaPermissions.ManageMediaFolder, (object)path))
        {
            return (null, $"you are not allowed to use the media file '{path}'.");
        }

        // Only a shell scope runs the step that deletes the audio a new upload replaces once the clip is saved.
        if (ShellScope.Current is null)
        {
            return (null, "the audio cannot be replaced from here. Upload it on the Voice media page.");
        }

        if (file.Length <= 0)
        {
            return (null, $"the media file '{path}' is empty.");
        }

        if (!new FileExtensionContentTypeProvider().TryGetContentType(file.Name, out var contentType) ||
            !VoiceMediaAudioUpload.IsAllowedContentType(contentType))
        {
            return (null, $"'{path}' is not an audio file the voice media library takes. Use an mp3, wav or ogg file.");
        }

        return (new MediaAudio(store, path, file.Length, contentType), null);
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

    private sealed record MediaAudio(IMediaFileStore Store, string Path, long Length, string ContentType);
}
