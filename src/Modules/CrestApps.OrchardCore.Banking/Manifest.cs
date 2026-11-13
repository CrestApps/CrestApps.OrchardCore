using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Resources;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Banking",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = BankingConstants.Features.Banking,
    Name = "Banking",
    Description = "Bank accounts for any module that sends money by bank: the business's own accounts and those of the people and organizations it pays. Account numbers are protected at rest and show only their last digits, routing numbers are checked, accounts are verified, and showing a full number needs its own permission and is written to the Audit Trail.",
    Category = "Finance",
    Dependencies =
    [
        ResourcesConstants.Feature.Area,
    ]
)]
