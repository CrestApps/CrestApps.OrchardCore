using CrestApps.OrchardCore;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Email Omnichannel Automation",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Name = "Email Omnichannel Automation",
    Id = "CrestApps.OrchardCore.Omnichannel.Email",
    Description = "Handles automated omnichannel activities by email: the AI sends a campaign's opening email, answers the customer's replies in the same thread, follows up on the campaign's cadence within business hours, concludes with a disposition and hands the customer to a person or a queue. An email entry point can route a customer's first email to an AI agent.",
    Category = "Contact Center",
    Dependencies =
    [
        AIConstants.Feature.Area,
        AIConstants.Feature.ChatCore,
        OmnichannelConstants.Features.Managements,
        MessagingConstants.Feature.Email,

        // Follow-ups are sent only within the campaign's business hours, so the feature that registers the gate and the
        // calendars comes with this one. Referenced by feature id to avoid an assembly reference.
        "CrestApps.OrchardCore.ContactCenter.BusinessHours",
    ]
)]
