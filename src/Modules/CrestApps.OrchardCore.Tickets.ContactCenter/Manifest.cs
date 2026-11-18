using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.ContactCenter;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Tickets for Contact Center",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = TicketsContactCenterConstants.Feature,
    Name = "Tickets for Contact Center",
    Description = "Offers new tickets of a desk to agents through a Contact Center queue, so presence, capacity and skills routing apply, and gives a ticket to the agent who accepts it.",
    Category = "Service Desk",
    Dependencies =
    [
        TicketsConstants.Features.Tickets,
        ContactCenterConstants.Feature.Queues,
    ]
)]
