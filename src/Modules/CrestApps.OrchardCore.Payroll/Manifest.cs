using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Checks;
using CrestApps.OrchardCore.Disbursements;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Taxation;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Payroll",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = PayrollConstants.Features.PaySetup,
    Name = "Pay Setup",
    Description = "Pay schedules (how often people are paid, the overtime week and each period's pay date) and pay codes (earnings, deductions, contributions and the rest, by kind), shared by Workforce's time clock and by Payroll.",
    Category = "Payroll"
)]

[assembly: Feature(
    Id = PayrollConstants.Features.Payroll,
    Name = "Payroll",
    Description = "Employers with their registrations and the bank account they pay from, permissions per employer, ledger codes and work site jurisdictions on teams, and Payroll's settings; pay profiles, pay runs and statements build on it.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.PaySetup,
        StaffConstants.Features.Staff,
        StaffConstants.Features.Compensation,
        ApprovalsConstants.Features.Approvals,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.TaxTables,
    Name = "Payroll Tax Tables",
    Description = "The built-in tax provider: payroll tax rules for any jurisdiction (a rate, a fixed amount, a table or brackets, a wage base, elections and a supplemental rate), worked out over Taxation's tax tables.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.Payroll,
        TaxationConstants.Feature.Tables,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.DirectDeposit,
    Name = "Direct Deposit",
    Description = "Pays net pay to people's bank accounts: each deposit row of a processed statement becomes an ACH disbursement from the employer's funding account, sent in the bank file of Disbursements.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.Payroll,
        BankingConstants.Features.Banking,
        DisbursementsConstants.Features.Disbursements,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.Paychecks,
    Name = "Paychecks",
    Description = "Pays net pay by printed checks: each check row of a processed statement, or a deposit with no way to pay it, becomes a check on the employer's funding account with the statement printed as its voucher.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.Payroll,
        ChecksConstants.Features.Checks,
    ]
)]
