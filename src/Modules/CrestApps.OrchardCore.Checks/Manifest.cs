using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Checks;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Checks",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ChecksConstants.Features.Checks,
    Name = "Checks",
    Description = "Check printing for any module that pays by check: layouts for blank or pre-printed stock with calibration, signatures and the check line (MICR), a check register numbered per bank account, printing to one PDF with vouchers, void, reissue, stop payment and cleared checks, and a Check Register data set for the bank's positive pay file.",
    Category = "Finance",
    Dependencies =
    [
        BankingConstants.Features.Banking,
    ]
)]
