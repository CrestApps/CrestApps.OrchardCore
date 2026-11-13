using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Gives the picture of a user, so screens that show people (pickers, organization charts, lists) can show their face
/// without knowing where the picture is kept. The User Avatars feature provides one.
/// </summary>
public interface IUserAvatarProvider
{
    /// <summary>
    /// Gets the address of the user's picture.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The picture's address; <see langword="null"/> when the user has none.</returns>
    Task<string> GetAvatarUrlAsync(IUser user, CancellationToken cancellationToken = default);
}
