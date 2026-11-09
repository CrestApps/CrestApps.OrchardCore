using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Users Core Components",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Users Core Components",
    Id = UsersConstants.Feature.Area,
    Category = "Users",
    Description = "Provides user components core services",
    EnabledByDependencyOnly = true
)]

[assembly: Feature(
    Name = "User Display Name",
    Category = "Users",
    Description = "Provides a way to change how the user name is displayed.",
    Id = UsersConstants.Feature.DisplayName,
    Dependencies =
    [
        OrchardCoreFeatureIds.ContentFields,
        UsersConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Name = "Sign in with an email code",
    Category = "Users",
    Description = "Lets people sign in with a code emailed to them instead of a password. Accounts with two-factor sign-in keep using their password.",
    Id = UsersConstants.Feature.EmailCodeSignIn,
    Dependencies =
    [
        OrchardCoreFeatureIds.Users,
        OrchardCoreFeatureIds.Email,
        UsersConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Name = "User Avatar",
    Category = "Users",
    Description = "Provides a way to display an avatar for each user.",
    Id = UsersConstants.Feature.Avatars,
    Dependencies =
    [
        OrchardCoreFeatureIds.Media,
        UsersConstants.Feature.Area,
    ]
)]
