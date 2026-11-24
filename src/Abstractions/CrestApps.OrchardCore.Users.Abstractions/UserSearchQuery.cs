namespace CrestApps.OrchardCore.Users;

/// <summary>
/// What an <see cref="IUserSearchService"/> looks for.
/// </summary>
public sealed class UserSearchQuery
{
    /// <summary>
    /// The number of users a search returns unless <see cref="MaxResults"/> says otherwise.
    /// </summary>
    public const int DefaultMaxResults = 50;

    /// <summary>
    /// Gets or sets the text to look for in the user name, or <see langword="null"/> to list users.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text is also looked for in the e-mail address.
    /// </summary>
    public bool MatchEmail { get; set; }

    /// <summary>
    /// Gets or sets the role names a user must have one of, or an empty list for any role.
    /// </summary>
    public IList<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets a named group of users another module defines through an <see cref="IUserAudienceProvider"/>, such as
    /// <c>Staff</c>. When no module defines the audience, it is ignored and every user can be found.
    /// </summary>
    public string Audience { get; set; }

    /// <summary>
    /// Gets or sets the largest number of users to return.
    /// </summary>
    public int MaxResults { get; set; } = DefaultMaxResults;
}
