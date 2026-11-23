using CrestApps.OrchardCore;
using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.MailingLists.Mailchimp;
using CrestApps.OrchardCore.Webhooks;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Mailing Lists Mailchimp",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = MailchimpConstants.Features.Mailchimp,
    Name = "Mailing Lists Mailchimp",
    Description = "Keeps mailing lists in Mailchimp audiences: members are pushed to Mailchimp, Mailchimp's own double opt-in can confirm sign-ups, and Mailchimp's webhooks and a daily reconciliation bring unsubscribes and bounces back. The API key stays in Orchard Secrets.",
    Category = "Communication",
    Dependencies =
    [
        MailingListsConstants.Features.MailingLists,
        WebhooksConstants.Feature.ProviderInbox,
        OrchardCoreFeatureIds.Secrets,
    ]
)]
