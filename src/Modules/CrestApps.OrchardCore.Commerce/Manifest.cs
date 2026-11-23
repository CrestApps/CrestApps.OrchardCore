using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Commerce",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Commerce",
    Id = CommerceConstants.Features.Area,
    Description = "The store layer: the store's setup guide under Commerce > Set Up Your Store, and the roles it adds for a store's staff.",
    Category = "Commerce",
    EnabledByDependencyOnly = true,
    Dependencies =
    [
        CommerceConstants.Features.Foundation,
    ]
)]

[assembly: Feature(
    Name = "Commerce Foundation",
    Id = CommerceConstants.Features.Foundation,
    Description = "What billing and the store share: the Commerce admin menu and its icon, the customer account, the commerce admin list helpers, customers' access to their own admin pages, and the export and anonymization of a person's commerce data.",
    Category = "Commerce",
    Dependencies =
    [
        CrestApps.OrchardCore.Users.UsersConstants.Feature.Account,
    ],
    EnabledByDependencyOnly = true
)]
