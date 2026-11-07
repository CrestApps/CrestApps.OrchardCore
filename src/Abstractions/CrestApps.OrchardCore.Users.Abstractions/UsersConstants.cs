namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Contains constant values for users.
/// </summary>
public static class UsersConstants
{
    /// <summary>
    /// The cache tag of the user display names, removed when a user's name changes.
    /// </summary>
    public const string UserDisplayNameCacheTag = "user-display-name";

    /// <summary>
    /// Represents the feature.
    /// </summary>
    public static class Feature
    {
        public const string Area = "CrestApps.OrchardCore.Users";

        public const string DisplayName = "CrestApps.OrchardCore.Users.DisplayName";

        public const string EmailCodeSignIn = "CrestApps.OrchardCore.Users.EmailCodeSignIn";

        public const string Avatars = "CrestApps.OrchardCore.Users.Avatars";
    }
}
