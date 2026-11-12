using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Resources;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Accounts",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = AccountsConstants.Feature,
    Name = "Accounts",
    Description = "Keeps the businesses and households that contacts, orders, assets and work orders belong to as Account content items, with a picker, a search and an account page that other modules add to.",
    Category = "Content Management",
    Dependencies =
    [
        ResourcesConstants.Feature.Area,
        OrchardCoreFeatureIds.Contents,
        OrchardCoreFeatureIds.ContentTypes,
        OrchardCoreFeatureIds.Lists,
        OrchardCoreFeatureIds.Title,
    ]
)]

[assembly: Feature(
    Id = AccountsConstants.MembersFeature,
    Name = "Account Members",
    Description = "Lets users sign in for an account: who its members are, and which of them manage it for the others. Other modules read the members to know who acts for an account.",
    Category = "Content Management",
    Dependencies =
    [
        AccountsConstants.Feature,
        OrchardCoreFeatureIds.Users,
    ]
)]

[assembly: Feature(
    Id = AccountsConstants.TeamFeature,
    Name = "Account Team",
    Description = "Keeps the staff who look after each account: the owner (the account manager or sales rep who answers for it) and the rest of the account team, picked from staff and shown on the account page.",
    Category = "Content Management",
    Dependencies =
    [
        AccountsConstants.Feature,
        CrestApps.OrchardCore.Staff.StaffConstants.Features.Staff,
    ]
)]

[assembly: Feature(
    Id = AccountsConstants.LocationsFeature,
    Name = "Account Locations",
    Description = "Keeps the places an account has, such as its head office, its warehouses or the sites where it is serviced, with addresses checked against each country's rules.",
    Category = "Content Management",
    Dependencies =
    [
        AccountsConstants.Feature,

        // A location's address is entered and checked with the shared address editor.
        AddressConstants.Feature.ModuleId,
    ]
)]
