using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Approvals;
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
    Description = "Payroll for the company's own staff: the company with its registrations and the bank account it pays from (Payroll > Management > Company), pay profiles, pay runs and statements, ledger codes and work site jurisdictions on teams, and Payroll's settings. Turn on Multiple Employers to run payroll for other employers too.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.PaySetup,
        StaffConstants.Features.Staff,
        StaffConstants.Features.Compensation,
        ApprovalsConstants.Features.Approvals,
        AddressConstants.Feature.ModuleId,
        TaxationConstants.Feature.IncomeTax,
        TaxationConstants.Feature.TaxForms,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.MultipleEmployers,
    Name = "Multiple Employers",
    Description = "Payroll for more than one employer, such as the companies of a group or the clients of a payroll service: Payroll > Management > Employers, an employer on each pay run and pay profile (a person may have one pay profile with each employer), employer filters on the payroll pages, and permissions per employer.",
    Category = "Payroll",
    Dependencies =
    [
        PayrollConstants.Features.Payroll,
    ]
)]

[assembly: Feature(
    Id = PayrollConstants.Features.UnitedStates,
    Name = "United States Payroll Filings",
    Description = "The employer's federal payroll returns made from pay statements: adds Forms 941, 940 and 945, W-2 and W-3, 1099-NEC and 1096 as tax forms you can edit under Commerce > Taxation > Tax Forms, and the SSA EFW2 wage file. The taxes themselves come from Taxation's United States Income Tax.",
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
    Description = "The employer's Canadian payroll filings made from pay statements: source deductions remitted to the CRA and to Revenu Québec on the employer's remitter schedule, T4 slips and the T4 Summary with the T4 XML file, and RL-1 slips with the RL-1 Summary, added as tax forms you can edit under Commerce > Taxation > Tax Forms and shown to people in My Pay. The deductions themselves come from Taxation's Canada Income Tax.",
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
