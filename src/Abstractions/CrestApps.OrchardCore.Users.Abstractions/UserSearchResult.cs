namespace CrestApps.OrchardCore.Users;

/// <summary>
/// A user found by an <see cref="IUserSearchService"/>.
/// </summary>
public sealed class UserSearchResult
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the normalized user name.
    /// </summary>
    public string NormalizedUserName { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the name to show, from the <see cref="IDisplayNameProvider"/>.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may sign in.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the address of the user's picture, from an <see cref="IUserAvatarProvider"/>; <see langword="null"/>
    /// when the user has none or no provider is registered.
    /// </summary>
    public string AvatarUrl { get; set; }
}
