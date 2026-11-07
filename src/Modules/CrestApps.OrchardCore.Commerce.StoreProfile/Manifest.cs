using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Commerce;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Store Profile",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Store Profile",
    Id = CommerceConstants.Features.StoreProfile,
    Description = "The store's name, logo, tax ID, contact details and address in one place, which receipts, the address parcels ship from and the tax origin use when their own settings are empty.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        AddressConstants.Feature.ModuleId,
    ]
)]
