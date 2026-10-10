using CrestApps.OrchardCore;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Email Messaging Channel",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Sends and receives email in the Omnichannel Messaging workspace.",
    Category = "Contact Center"
)]

[assembly: Feature(
    Id = MessagingConstants.Feature.Email,
    Name = "Email Messaging Channel",
    Description = "Adds email as a channel of the Omnichannel Messaging workspace: email addresses as omnichannel addresses, each sending through Orchard Core's email service or its own SMTP server; inbound email from provider webhooks (SendGrid, Mailgun, Postmark, Amazon SES, raw MIME or JSON) or from the address's own mailbox over IMAP; email entry points that route each address's mail to a queue, an agent or an AI agent; threaded replies with subjects and attachments; bounce handling; loop protection against automatic replies; and one-click unsubscribe on bulk mail.",
    Category = "Contact Center",
    Dependencies =
    [
        MessagingConstants.Feature.Workspace,
        ContactCenterConstants.Feature.ProviderInbox,
        "OrchardCore.Email",
    ]
)]
