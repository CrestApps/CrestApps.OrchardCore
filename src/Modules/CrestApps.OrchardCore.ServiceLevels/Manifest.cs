using CrestApps.OrchardCore;
using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.Resources;
using CrestApps.OrchardCore.ServiceLevels;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Service Levels",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = ServiceLevelsConstants.Features.ServiceLevels,
    Name = "Service Levels",
    Description = "Service level policies for any kind of work: how long a first response, a next response or a resolution may take at each priority, counted on a business-hours schedule or around the clock. Each record gets a timer per metric that pauses and resumes, and the modules that own the work are told when a timer nears or passes its target.",
    Category = "Organization",
    Dependencies =
    [
        BusinessHoursConstants.Features.BusinessHours,
        ResourcesConstants.Feature.Area,
    ]
)]
