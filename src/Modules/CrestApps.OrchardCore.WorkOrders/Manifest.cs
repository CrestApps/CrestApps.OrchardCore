using CrestApps.OrchardCore;
using CrestApps.OrchardCore.WorkOrders;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Work Orders",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = WorkOrdersConstants.Features.WorkOrders,
    Name = "Work Orders",
    Description = "Jobs to do for customers: types with their defaults, checklists, service levels with warnings and breaches, parts used, signatures, follow-ups and invoices.",
    Category = "Field Service"
)]

[assembly: Feature(
    Id = WorkOrdersConstants.Features.Agreements,
    Name = "Service Agreements",
    Description = "Recurring visits, such as preventive maintenance, for a customer over a period: their work orders are made ahead of time, once each, included or billed per visit.",
    Category = "Field Service",
    Dependencies =
    [
        WorkOrdersConstants.Features.WorkOrders,
    ]
)]
