using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Teams;
using CrestApps.OrchardCore.Users;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Staff",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = StaffConstants.Features.Staff,
    Name = "Staff",
    Description = "One record for the people who work for the business, with or without a user account: employment details, the reporting line, dated team memberships, capabilities other modules register, a staff directory and picker, employment history and a Staff role for working staff. Workforce, Contact Center, field service, Point of Sale and other modules build on it.",
    Category = "Organization",
    Dependencies =
    [
        UsersConstants.Feature.Area,
        TeamsConstants.Features.Teams,
        OrchardCoreFeatureIds.Roles,
    ]
)]

[assembly: Feature(
    Id = StaffConstants.Features.Compensation,
    Name = "Staff Compensation",
    Description = "Dated hourly pay rates (for all work or one kind of work), an annual salary and the loaded cost of an hour of work on each staff member, for payroll exports and labor costing.",
    Category = "Organization",
    Dependencies =
    [
        StaffConstants.Features.Staff,
    ]
)]

[assembly: Feature(
    Id = StaffConstants.Features.Badges,
    Name = "Staff Badges",
    Description = "A PIN for each staff member, used with their employee number as a badge at time clock kiosks and registers. PINs are stored hashed and a badge locks after too many wrong PINs.",
    Category = "Organization",
    Dependencies =
    [
        StaffConstants.Features.Staff,
    ]
)]

[assembly: Feature(
    Id = StaffConstants.Features.OrgChart,
    Name = "Staff Organization Chart",
    Description = "An organization chart drawn from the reporting line: who reports to whom, with job titles and teams.",
    Category = "Organization",
    Dependencies =
    [
        StaffConstants.Features.Staff,
    ]
)]
