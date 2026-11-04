using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Webhooks;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Webhooks",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Webhooks both ways: a durable inbox for provider callbacks, and signed outbound events.",
    Category = "Webhooks"
)]

[assembly: Feature(
    Id = WebhooksConstants.Feature.Webhooks,
    Name = "Webhooks",
    Description = "Tells other systems when something happens on the site, such as an order being paid or an AI conversation ending, by posting signed JSON to the URLs you subscribe, retried with backoff until they answer, with a log of every delivery. Modules declare the events they publish.",
    Category = "Webhooks"
)]

[assembly: Feature(
    Id = WebhooksConstants.Feature.ProviderInbox,
    Name = "Provider Webhook Inbox",
    Description = "Durably commits an authenticated provider webhook delivery before any handler runs, deduplicates redeliveries on the provider's own delivery id, and retries processing from storage so a callback is never lost to a restart. Shared by every module that ingests provider callbacks.",
    Category = "Webhooks",
    EnabledByDependencyOnly = true
)]
