using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.AI.Core;
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
        AddressConstants.Feature.ModuleId,
        TaxationConstants.Feature.IncomeTax,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.UnitedStates,
    Name = "United States Payroll Filings",
    Description = "The employer's federal payroll returns made from pay statements: Forms 941, 940 and 945, W-2 and W-3, 1099-NEC and 1096, the SSA EFW2 wage file and year end. The taxes themselves come from Taxation's United States Income Tax.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.Payroll,
        TaxationConstants.Feature.IncomeTaxUnitedStates,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.Canada,
    Name = "Canada Payroll Filings",
    Description = "The employer's Canadian payroll filings made from pay statements: source deductions remitted to the CRA and to Revenu Québec on the employer's remitter schedule, T4 slips and the T4 Summary with the T4 XML file, and RL-1 slips with the RL-1 Summary, shown to people in My Pay. The deductions themselves come from Taxation's Canada Income Tax.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.Payroll,
        TaxationConstants.Feature.IncomeTaxCanada,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.Garnishments,
    Name = "Garnishments",
    Description = "Garnishment orders (support, tax levies, bankruptcy, student loans, creditors) withheld from pay within the limits the law sets on disposable earnings, in order of priority, and sent to their payees by check or deposit.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.Payroll,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.AI,
    Name = "Payroll AI Tools",
    Description = "Tools an AI assistant uses to explain a person's own pay statement and what changed in a pay run since the last one, each checking the permission of the page it mirrors. Turn it on only if pay may go to the site's AI provider.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.Payroll,
        AIConstants.Feature.Area,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.SelfService,
    Name = "My Pay",
    Description = "A person's own pay in the admin: their pay statements and stubs, their tax elections and where their pay goes, linked from Staff > My Profile.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.Payroll,
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
