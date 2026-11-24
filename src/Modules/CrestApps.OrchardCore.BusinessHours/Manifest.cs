using CrestApps.OrchardCore;
using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.Resources;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Business Hours",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = BusinessHoursConstants.Features.BusinessHours,
    Name = "Business Hours",
    Description = "Business hours schedules any module can use: weekly hours with lunch breaks, holidays and days with special hours, in a time zone. Queues, service levels and automations read them through the shared open-hours service; a schedule picker and a check-business-hours workflow task come with it.",
    Category = "Organization",
    Dependencies =
    [
        ResourcesConstants.Feature.Area,
    ]
)]
