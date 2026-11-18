using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Users.Models;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Media;
using OrchardCore.Media.Fields;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.AI;

/// <summary>
/// Sets or clears a user's avatar from a file already in the media library, as the avatar card of the user editor does:
/// it needs <c>ManageMedia</c>, which the card needs, and <c>EditUsers</c> on that user, which the editor needs. The file
/// must exist and have an image extension the media library allows, and clearing is refused when avatars are required.
/// </summary>
internal sealed class SetUserAvatarTool : UsersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setUserAvatar";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "userId": {
          "type": "string",
          "description": "The id of the user, from searchForUsers. A user name or email is accepted too."
        },
        "mediaPath": {
          "type": "string",
          "description": "The path of an image in the media library, such as people/ana.png. Leave it out, with clear set to true, to remove the avatar."
        },
        "clear": {
          "type": "boolean",
          "description": "true to remove the user's avatar instead of setting one."
        }
      },
      "required": ["userId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets or clears a user's avatar from a media library path. The file must already be in the media library and have an allowed image extension; clearing fails when avatars are required.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, MediaPermissions.ManageMedia))
        {
            return NotAllowedMessage;
        }

        var userId = ReadString(arguments, "userId");
        var user = await FindUserAsync(services, userId);

        if (user is null)
        {
            return await IsAuthorizedAsync(services, caller, UsersPermissions.EditUsers)
                ? $"No user has the id '{userId}'. Call searchForUsers to find it."
                : NotAllowedMessage;
        }

        if (!await IsAuthorizedAsync(services, caller, UsersPermissions.EditUsers, user))
        {
            return NotAllowedMessage;
        }

        var clear = ReadBool(arguments, "clear") == true;
        var mediaPath = ReadString(arguments, "mediaPath");

        if (clear == (mediaPath is not null))
        {
            return "Nothing was changed: give either a mediaPath to set the avatar, or clear set to true to remove it.";
        }

        var part = user.GetOrCreate<UserAvatarPart>();
        part.Avatar ??= new MediaField();

        if (clear)
        {
            if (services.GetRequiredService<IOptionsMonitor<UserAvatarOptions>>().CurrentValue.Required)
            {
                return "Nothing was changed: the site requires an avatar for every user, so it cannot be removed. Set another image instead.";
            }

            part.Avatar.Paths = [];
            part.Avatar.MediaTexts = [];
        }
        else
        {
            var mediaFileStore = services.GetRequiredService<IMediaFileStore>();
            var path = NormalizeMediaPath(mediaPath);

            // A public media URL, such as /media/people/ana.png, names the same file as its path.
            var publicPrefix = mediaFileStore.MapPathToPublicUrl(string.Empty).TrimEnd('/') + "/";

            if (mediaPath.StartsWith(publicPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = NormalizeMediaPath(mediaPath[publicPrefix.Length..]);
            }

            if (path.Length == 0 || path.Split('/').Any(segment => segment is "." or ".."))
            {
                return $"Nothing was changed: '{mediaPath}' is not a media library path. Use a path such as people/ana.png.";
            }

            var allowedExtensions = UserAvatarRules.GetAllowedExtensions(
                services.GetRequiredService<IOptions<MediaOptions>>().Value,
                services.GetRequiredService<IContentTypeProvider>());

            if (!UserAvatarRules.IsAllowedExtension(path, allowedExtensions))
            {
                return $"Nothing was changed: '{path}' is not an image the media library allows as an avatar. Allowed extensions: {string.Join(", ", allowedExtensions)}.";
            }

            var file = await mediaFileStore.GetFileInfoAsync(path);

            if (file is null || file.IsDirectory)
            {
                return $"Nothing was changed: there is no file '{path}' in the media library. Upload it first, or use the path of an existing image.";
            }

            part.Avatar.Paths = [file.Path];
            part.Avatar.MediaTexts = [];
        }

        user.Put(part);

        var errors = await UpdateUserAsync(services, user);

        if (errors.Count > 0)
        {
            return $"The user was not saved: {string.Join(" ", errors)}";
        }

        return Json(new
        {
            userId = user.UserId,
            userName = user.UserName,
            avatarPath = part.Avatar.Paths.FirstOrDefault(),
            avatarUrl = part.Avatar.Paths.Length > 0 ? services.GetRequiredService<IMediaFileStore>().MapPathToPublicUrl(part.Avatar.Paths[0]) : null,
            cleared = clear,
            saved = true,
        });
    }

    private static string NormalizeMediaPath(string path)
        => path.Replace('\\', '/').Trim().Trim('/');
}
