using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Plaid;
using CrestApps.OrchardCore.Webhooks;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Plaid",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = PlaidConstants.Features.Plaid,
    Name = "Plaid Bank Account Linking",
    Description = "Instant bank account verification with Plaid: a person signs in to their bank in Plaid Link, picks the account, and the bank account is saved with the bank's own numbers and verified at once. The Plaid secret stays in Orchard Secrets.",
    Category = "Finance",
    Dependencies =
    [
        BankingConstants.Features.Banking,
        OrchardCoreFeatureIds.Secrets,
        WebhooksConstants.Feature.ProviderInbox,
    ]
)]
