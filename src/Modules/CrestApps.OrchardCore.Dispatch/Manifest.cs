using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.Workforce;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Dispatch",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = DispatchConstants.Features.Dispatch,
    Name = "Dispatch",
    Description = "Visits for work orders, technicians found by skills, certifications, territory, shifts, time off and travel, assignment that never double-books, a job on each technician's calendar, and a live dispatch board with a map.",
    Category = "Field Service",
    Dependencies =
    [
        WorkOrdersConstants.Features.WorkOrders,
        WorkforceConstants.Features.Workforce,
        GeoConstants.Features.Geo,
    ]
)]

[assembly: Feature(
    Id = DispatchConstants.Features.Mobile,
    Name = "Technician Mobile",
    Description = "A Field page in My Work for technicians: today's visits offline, status, checklists, parts, notes, photos and the customer's signature, sent once back online; and the technician's position while working, with their consent.",
    Category = "Field Service",
    Dependencies =
    [
        DispatchConstants.Features.Dispatch,
        WorkforceConstants.Features.Portal,
        OrchardCoreFeatureIds.Media,
    ]
)]

[assembly: Feature(
    Id = DispatchConstants.Features.Api,
    Name = "Field Service API",
    Description = "Work orders at api/work-orders/v1, visits, candidates, assignment and optimizing at api/dispatch/v1, and geocoding and territories at api/geo/v1, for other systems and apps, with the admin's permissions.",
    Category = "Field Service",
    Dependencies =
    [
        DispatchConstants.Features.Dispatch,
    ]
)]
