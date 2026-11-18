using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Users;
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
    Name = "User Audiences",
    Category = "Users",
    Description = "Lets administrators define audiences, groups of people chosen by rules such as their roles, their team, their hire date or the skills they hold, that other features target, such as required learning.",
    Id = UsersConstants.Feature.Audiences,
    Dependencies =
    [
        OrchardCoreFeatureIds.Users,
        OrchardCoreFeatureIds.Roles,
        UsersConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Name = "My Account",
    Category = "Users",
    Description = "The \"my account\" area on the site (/account): a home page, a menu and a page frame that other modules add their pages to, such as orders or tickets.",
    Id = UsersConstants.Feature.Account,
    Dependencies =
    [
        OrchardCoreFeatureIds.Users,
        UsersConstants.Feature.Area,
    ],
    EnabledByDependencyOnly = true
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
