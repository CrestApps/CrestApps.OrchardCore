using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Resources;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.Teams;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Users;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Tickets",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = TicketsConstants.Features.Tickets,
    Name = "Tickets",
    Description = "A help desk: tickets of any ticket type filed under desks that keep IT, HR and customer support apart, with statuses, priorities and saved views, replies and internal notes, team assignment, service-level targets on business hours, merge and links.",
    Category = "Service Desk",
    Dependencies =
    [
        TimelineConstants.Features.Timeline,
        ServiceLevelsConstants.Features.ServiceLevels,
        TeamsConstants.Features.Teams,
        ResourcesConstants.Feature.Area,
        UsersConstants.Feature.Area,
        OrchardCoreFeatureIds.Contents,
        OrchardCoreFeatureIds.Title,
    ]
)]
