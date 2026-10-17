using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Customers",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = CustomersConstants.Features.Customers,
    Name = "Customers",
    Description = "Keeps a record of everyone who buys from the store, with an account or as a guest, and lists them under Commerce > Customers.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        "OrchardCore.Users",
        AddressConstants.Feature.ModuleId,
    ]
)]
