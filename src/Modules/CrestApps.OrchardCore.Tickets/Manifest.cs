using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContentFields;
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

        // Ticket categories and the questions that follow the answers (show-when rules on the type's fields).
        ContentFieldsConstants.Features.ConditionalFields,

        // Agents and signed-in requesters are told through Notifications; its e-mail method (which brings Notifications
        // along) makes sure they also get them by e-mail, unless they turn a kind off.
        OrchardCoreFeatureIds.NotificationsEmail,
    ]
)]

[assembly: Feature(
    Id = TicketsConstants.Features.Portal,
    Name = "Tickets Portal",
    Description = "Lets requesters open and follow their requests on the site: a Requests page in My account with the ticket types customers may submit, their requests and their organization's for account administrators, and the conversation with replies and files. A requester without a login follows the link in their e-mails.",
    Category = "Service Desk",
    Dependencies =
    [
        TicketsConstants.Features.Tickets,
        CrestApps.OrchardCore.Users.UsersConstants.Feature.Account,

        // The requester posts are limited through Orchard Core's Rate Limits, so it is always on with the portal.
        OrchardCoreFeatureIds.RateLimits,
    ]
)]

[assembly: Feature(
    Id = TicketsConstants.Features.WebForm,
    Name = "Ticket Web Form",
    Description = "A Ticket Form widget that opens a ticket of a chosen type on a chosen desk from any page, for visitors with or without a login, with a hidden trap field, a limit per visitor and, with ReCaptcha on, a challenge.",
    Category = "Service Desk",
    Dependencies =
    [
        TicketsConstants.Features.Tickets,

        // The anonymous form is limited through Orchard Core's Rate Limits, so it is always on with the form.
        OrchardCoreFeatureIds.RateLimits,
    ]
)]

[assembly: Feature(
    Id = TicketsConstants.Features.Approvals,
    Name = "Ticket Approvals",
    Description = "Lets a ticket type ask for approval before work goes on: steps, one after the other, each decided by one of its approvers or the requester's manager, on the ticket or a page of its own. A refusal solves the ticket and tells the requester why.",
    Category = "Service Desk",
    Dependencies =
    [
        TicketsConstants.Features.Tickets,
    ]
)]

[assembly: Feature(
    Id = TicketsConstants.Features.Api,
    Name = "Tickets API",
    Description = "Lets other systems list, read, open, change, reply to and add notes to tickets over a JSON API, api/tickets/v1, with the caller's ticket permissions. Enable OpenAPI to document it.",
    Category = "Service Desk",
    Dependencies =
    [
        TicketsConstants.Features.Tickets,
    ]
)]
