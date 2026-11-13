using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Disbursements;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Disbursements",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = DisbursementsConstants.Features.Disbursements,
    Name = "Disbursements",
    Description = "Money the business sends out, for any module that pays people or businesses: disbursements recorded once by a stable key, batches released through a provider (a printed check when Checks is on, or paid outside the system), a Disbursements data set, recipe and deployment steps and workflow activities.",
    Category = "Finance",
    Dependencies =
    [
        BankingConstants.Features.Banking,
    ]
)]

[assembly: Feature(
    Id = DisbursementsConstants.Features.Nacha,
    Name = "Disbursements - Bank File (ACH)",
    Description = "Sends disbursements as a bank file in the Nacha format, imports the bank's return and change files, verifies payee accounts with test entries (prenotes), and adds the Read a disbursement batch file step to Data Pipelines so a pipeline delivers the files.",
    Category = "Finance",
    Dependencies =
    [
        DisbursementsConstants.Features.Disbursements,
    ]
)]
