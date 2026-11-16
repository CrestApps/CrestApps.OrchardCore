using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Banking.Disbursements;
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
    Name = "Bank Accounts",
    Description = "Bank accounts for any module that sends money by bank: the business's own accounts and those of the people and organizations it pays. Account numbers are protected at rest and show only their last digits, routing numbers are checked, accounts are verified, and showing a full number needs its own permission and is written to the Audit Trail.",
    Category = "Finance",
    Dependencies =
    [
        ResourcesConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = ChecksConstants.Features.Checks,
    Name = "Checks",
    Description = "Check printing for any module that pays by check: layouts for blank or pre-printed stock with calibration, signatures and the check line (MICR), a check register numbered per bank account, printing to one PDF with vouchers, void, reissue, reprint, stop payment, stale and cleared checks, and a Check Register data set for the bank's positive pay file.",
    Category = "Finance",
    Dependencies =
    [
        BankingConstants.Features.Banking,
    ]
)]

[assembly: Feature(
    Id = DisbursementsConstants.Features.Disbursements,
    Name = "Disbursements",
    Description = "Money the business sends out, for any module that pays people or businesses: disbursements recorded once by a stable key, batches released through a provider (a printed check when Checks is on, or paid outside the system), returns, a Disbursements data set, recipe and deployment steps and workflow activities.",
    Category = "Finance",
    Dependencies =
    [
        BankingConstants.Features.Banking,
    ]
)]

[assembly: Feature(
    Id = DisbursementsConstants.Features.DirectDeposit,
    Name = "Direct Deposit",
    Description = "Sends disbursements to US dollar bank accounts by ACH: a bank file in the Nacha format, the bank's return and change files, payee accounts verified with test entries (prenotes), and the Read a disbursement batch file step for Data Pipelines so a pipeline delivers the files. Other countries' bank file formats come as further providers.",
    Category = "Finance",
    Dependencies =
    [
        DisbursementsConstants.Features.Disbursements,
    ]
)]
