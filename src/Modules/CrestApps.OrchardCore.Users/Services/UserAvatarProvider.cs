using CrestApps.OrchardCore.Users.Models;
using OrchardCore.Entities;
using OrchardCore.Media;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Gives the picture a user picked with the User Avatars feature.
/// </summary>
internal sealed class UserAvatarProvider : IUserAvatarProvider
{
    private readonly IMediaFileStore _mediaFileStore;

    public UserAvatarProvider(IMediaFileStore mediaFileStore)
    {
        _mediaFileStore = mediaFileStore;
    }

    public Task<string> GetAvatarUrlAsync(IUser user, CancellationToken cancellationToken = default)
    {
        if (user is User found && found.TryGet<UserAvatarPart>(out var part) && part.Avatar?.Paths is { Length: > 0 } paths && !string.IsNullOrEmpty(paths[0]))
        {
            return Task.FromResult(_mediaFileStore.MapPathToPublicUrl(paths[0]));
        }

        return Task.FromResult<string>(null);
    }
}
