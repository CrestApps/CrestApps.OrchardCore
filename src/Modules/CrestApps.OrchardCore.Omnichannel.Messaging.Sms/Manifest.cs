using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "SMS Messaging Channel",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Sends and receives text messages in the Omnichannel Messaging workspace.",
    Category = "Contact Center"
)]

[assembly: Feature(
    Id = MessagingConstants.Feature.Sms,
    Name = "SMS Messaging Channel",
    Description = "Adds SMS as a channel of the Omnichannel Messaging workspace: SMS numbers as channel endpoints with a per-number provider, the per-number provider dispatcher, two-way send and receive through every SMS provider that raises inbound messages, the carrier keywords (STOP, START, HELP), and a Send SMS button beside phone-number fields.",
    Category = "Contact Center",
    Dependencies =
    [
        MessagingConstants.Feature.Workspace,
        ContactCenterConstants.Feature.ProviderInbox,
        "OrchardCore.Sms",

        // Orchard Core's Twilio provider only sends; the webhook that receives Twilio texts is its own feature, so
        // the workspace hears from Twilio numbers without SMS Omnichannel Automation being on.
        OmnichannelConstants.Features.TwilioSms,
    ]
)]
