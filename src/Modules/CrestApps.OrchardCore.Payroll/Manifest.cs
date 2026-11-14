using CrestApps.OrchardCore;
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
