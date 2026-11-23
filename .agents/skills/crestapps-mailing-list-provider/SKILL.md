---
name: crestapps-mailing-list-provider
description: >
  Skill for adding an outside e-mail marketing provider (Klaviyo, Brevo, Constant Contact, SendGrid Marketing,
  ActiveCampaign or any list API) to the CrestApps.OrchardCore Mailing Lists module, so a mailing list can live at
  that service while the site keeps every member and the evidence of consent. Covers the provider contract in
  CrestApps.OrchardCore.MailingLists.Abstractions — IMailingListProvider (Name, DisplayName, Capabilities,
  IsConfiguredAsync, an idempotent PushAsync with MailingListPushKind Upsert/Unsubscribe/Erase and
  MailingListPushResult.Success/Failure(canRetry), GetChangesAsync for the daily reconciliation) and
  MailingListProviderCapabilities (ConfirmsAddresses, DeliversWelcomeOffer, ReportsChanges, SendsCampaigns) — what the
  provider outbox, the reconciliation and ApplyProviderChangeAsync do for you, the ordering rule for provider changes,
  the connection card on the mailing-lists settings group with the API key in Orchard Core Secrets, the list card as a
  DisplayDriver<MailingList> that stores its settings in the list's Properties, webhooks through the provider webhook
  inbox (IProviderWebhookInbox, IProviderWebhookInboxHandler, AfterResponseWork), HTTP resilience and concurrency,
  registration with AddMailingListProvider<T>(), the module and feature layout, tests with a fake HttpMessageHandler,
  and docs. The Mailchimp module is the reference. Use this skill whenever the request is to "add a mailing list
  provider", "sync mailing lists with Klaviyo / Brevo", "integrate a newsletter service", implement
  IMailingListProvider, or change how mailing lists reach an outside service.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Adding a mailing list provider

The **Mailing Lists** feature (`CrestApps.OrchardCore.MailingLists`) owns lists, members, sign-ups, verification, the welcome coupon, consent and unsubscribing. A **provider** decides where a list lives. The built-in **Local** provider keeps the list on the site. An outside provider (Mailchimp is the first) receives every member change and reports its own changes back. The site always keeps every member record and the evidence of consent, whatever the provider is.

Read first; they are authoritative:

- Contracts: `src/Abstractions/CrestApps.OrchardCore.MailingLists.Abstractions/Services/IMailingListProvider.cs` (the provider, `MailingListProviderCapabilities`, `MailingListPushKind`, `MailingListPushResult`, `MailingListProviderChangePage`), `MailingListProviderChange.cs`, `IMailingListMemberService.cs` (`ApplyProviderChangeAsync`), `IMailingListProviderResolver.cs`, `MailingListsServiceCollectionExtensions.cs`, `MailingListsConstants.cs`, `Models/` (`MailingList`, `MailingListMember`, `MailingListMemberCoupon`, `MailingListWelcomeCoupon`).
- What the feature does around you: `src/Core/CrestApps.OrchardCore.MailingLists.Core/Sync/` (`MailingListSyncQueue`, `MailingListSyncProcessor`, `MailingListReconciliation`) and `Services/DefaultMailingListMemberService.cs` (`ApplyProviderChangeAsync`), `Services/MailingListProviderChanges.cs` (the ordering rule), `Services/MailingListMemberTransitions.cs`.
- The reference provider: `src/Core/CrestApps.OrchardCore.MailingLists.Mailchimp.Core/` (client, provider, push planner, change mapper, webhook parser, signature, inbox handler, credentials) and `src/Modules/CrestApps.OrchardCore.MailingLists.Mailchimp/` (manifest, startup, settings and list drivers, admin controller, webhook endpoint, recipe schema).
- Tests to copy: `tests/CrestApps.OrchardCore.Tests/MailingLists/Mailchimp/` and `tests/CrestApps.OrchardCore.Tests/MailingLists/MailingListProviderChangesTests.cs`.
- Docs: `src/CrestApps.Docs/docs/modules/mailing-lists.md` (providers, outbox, reconciliation) and `mailing-lists-mailchimp.md`; the provider webhook inbox in `docs/modules/webhooks.md`.

## 1. What the feature does, and what the provider does

The feature, for you:

1. **Queues every member change.** `MailingListSyncQueue.QueueAsync` adds a `MailingListSyncItem` (list, member, kind) to the leased work queue in the same unit of work as the change, and marks the member `SyncState.Pending`. A list whose provider resolves to Local is never queued.
2. **Sends it.** After the request commits, the change is sent at once; the **Send mailing list changes to providers** background task (every minute) sends what is still due. Each change is sent in a scope of its own: the processor loads the list and the member **fresh from the store** and calls your `PushAsync`.
3. **Retries.** `Failure(error, canRetry: true)` is tried again with a back-off (1 minute, doubling, up to 6 hours between tries) until `SyncMaxAttempts` (8 by default); `canRetry: false` gives up at once. A given-up change marks the member `SyncState.Failed` (**Not synced**) with your error text, shown in the admin.
4. **Reconciles.** Once a day, for every list whose provider has `ReportsChanges` and `IsConfiguredAsync`, it pages your `GetChangesAsync` (up to 200 pages) and applies each change through `ApplyProviderChangeAsync`. The run's start is the next `sinceUtc`; a run that throws keeps the old one.
5. **Applies changes.** `ApplyProviderChangeAsync` locks the list and address, ignores stale changes, follows the transition table, creates new members, verifies, records the marketing opt-in, runs the observers (the welcome coupon, workflows, the timeline) and never pushes back to you.

The provider only: talks to the outside API, maps a member to the service and back, keeps its own connection and list settings, and receives the service's webhooks. It never writes member records itself; every change goes through `IMailingListMemberService`.

## 2. The contract

| Member | What to implement |
| --- | --- |
| `Name` | A permanent technical name stored on each list (`ProviderName`), such as `Klaviyo`. Never rename it. Put it in your constants. |
| `DisplayName` | `S["Klaviyo"]`, shown in **Where the list lives**. |
| `Capabilities` | The flags the service supports (see below). Declare only what you implement. |
| `IsConfiguredAsync` | Whether the tenant's connection is usable (the secret exists and holds a key). While it returns false, the outbox keeps the list's changes waiting (not counted as failed attempts, the member shows the reason) and reconciliation is skipped; they go out once the provider is set up. |
| `PushAsync(list, member, kind)` | Send one change. Must be safe to call twice for the same change (see 4). |
| `GetChangesAsync(list, sinceUtc, cursor)` | A page of changes since a time, with `NextCursor` (`null` on the last page). Return `MailingListProviderChangePage.Empty` when the list has no remote list yet. Without `ReportsChanges`, return `Empty`. |

| Capability | What the feature does with it |
| --- | --- |
| `ConfirmsAddresses` | The list's **The provider sends its own confirmation e-mail** works: a sign-up is pushed as pending and the service sends its double opt-in, then reports the confirmation (`Verified = true`). Without it, such a list falls back to the site's double opt-in. |
| `DeliversWelcomeOffer` | The welcome coupon's **Delivered by** the provider: the site issues the code, stores it on the member as `MailingListMemberCoupon` (in `member.Properties`), sends no coupon e-mail, and your push writes the code to a profile field. Without it, the site e-mails the code. |
| `ReportsChanges` | The daily reconciliation calls `GetChangesAsync`. |
| `SendsCampaigns` | Informational: the service sends campaigns from its own tools. Nothing reads it today. |

## 3. Module and feature layout

```
src/Core/CrestApps.OrchardCore.MailingLists.Klaviyo.Core/   client, provider, push planner, change mapper, webhook parser and handler, settings models
src/Modules/CrestApps.OrchardCore.MailingLists.Klaviyo/      manifest, startup, drivers, admin controller, webhook endpoint, views, recipe schema
```

- One feature, `CrestApps.OrchardCore.MailingLists.Klaviyo`, category `Communication`, depending on `MailingListsConstants.Features.MailingLists`, `WebhooksConstants.Feature.ProviderInbox` (when it receives webhooks) and `OrchardCoreFeatureIds.Secrets`.
- The `.Core` project references only `.Abstractions` and `.Core` projects (MailingLists, Webhooks, the platform). The module references `.Core` and `.Abstractions` projects, and module projects only to carry a feature dependency (`PrivateAssets="none"`); module code never uses a type from another module assembly.
- Add the module to `src/Targets/CrestApps.OrchardCore.Cms.Core.Targets` and the solution.

```csharp
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddKlaviyoMailingLists(); // .Core: HttpClient + resilience, gate, client, AddMailingListProvider<T>(), inbox handler
        services.AddScoped<IKlaviyoCredentialsProvider, KlaviyoCredentialsProvider>();

        services.AddSiteDisplayDriver<KlaviyoSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(MailingListsConstants.SettingsGroupId, MailingListsPermissions.ManageMailingListSettings);
        services.AddDisplayDriver<MailingList, KlaviyoMailingListDisplayDriver>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddKlaviyoWebhookEndpoint();
}
```

`services.AddMailingListProvider<TProvider>()` registers a scoped `IMailingListProvider` (it takes no name: the name is the provider's `Name`). Don't register the outbox, the processor or the reconciliation; Mailing Lists does.

## 4. Pushing: idempotent, and mapped from the member's state

The outbox may call `PushAsync` twice for the same change (a crash after the call, a lease that expired). Make every call safe to repeat:

- **Upsert** on a stable key: the service's "create or update by e-mail" endpoint, or a key derived from the normalized address (Mailchimp: `PUT /lists/{id}/members/{md5(lower-case address)}`). Never "create" then fail on "already exists".
- **Set the status only for a new address** where the API allows it (Mailchimp's `status_if_new`), so a repeated push changes nothing and the service never sends a second confirmation e-mail. When the service answers that it holds the address as unsubscribed or bounced, decide explicitly what a new sign-up does there (Mailchimp: a second call to `pending`, so the person confirms with the service first). Never move a confirmed subscriber back to pending.
- **Unsubscribe** changes an existing member only; a member the service doesn't have is success.
- **Erase** permanently deletes the member; "not found" is success. The member may already be gone from the site: the outbox passes a stand-in with `NormalizedEmail` and the last `ProviderMemberId`, so derive your key from those.
- Map by `kind` first, then by `member.Status` (copy `MailchimpPushPlanner`):

| Member | Push |
| --- | --- |
| `Pending` on a list with `Confirmation == Provider` | Add as pending; the service sends its confirmation. |
| `Pending` on a list the site confirms | Nothing (`Success`). The member reaches the service once subscribed, so nobody gets two confirmation e-mails. |
| `Subscribed` | Upsert as subscribed, with the name and the list's tags or fields. |
| `Unsubscribed`, or kind `Unsubscribe` | Unsubscribe. |
| `Cleaned` | Nothing: the service cleaned it, or will. |
| kind `Erase` | Permanent delete. |

- With `DeliversWelcomeOffer`, write the code when `list.TryGet<MailingListWelcomeCoupon>(out var c) && c is { IsEnabled: true, Delivery: MailingListCouponDelivery.Provider }` and `member.TryGet<MailingListMemberCoupon>(out var issued)`.
- Return `MailingListPushResult.Success(providerMemberId)`; the processor stores the id on the member.
- Return `Failure(error, canRetry)` for every expected failure; don't throw. `canRetry: true` for time-outs, 408, 429, 5xx, a broken circuit, a missing connection; `false` for refusals (validation, a deleted remote list). The error text is shown to admins: make it say what to fix, and never include the key.

## 5. HTTP, credentials and concurrency

- A named `HttpClient` with `AddStandardResilienceHandler`. Disable retries for unsafe methods, and mark the safe ones (reads, upserts on a key, status changes, deletes) as retryable on the request; never retry a call that creates something (a webhook, a field). See `MailchimpCoreServiceCollectionExtensions.ConfigureResilience`.
- The API key lives in **Orchard Core Secrets** as a text secret the settings name (`TextSecretSelectList.BuildAsync` in `CrestApps.OrchardCore.Core.Security` builds the picker). Read it per request through a credentials provider, so a rotated key is used at once; a missing or empty secret means "not configured" and a logged warning. Send it in a header only, never in a URL, a body, an exception or a log; override `ToString` on your credentials type.
- Cap concurrent calls per tenant with a singleton gate (Mailchimp: 3) when the service limits simultaneous connections.

## 6. The connection card and the list card

**Connection** (`SiteDisplayDriver<KlaviyoSettings>` with `SettingsGroupId => MailingListsConstants.SettingsGroupId`): the secret name and any account setting, `RenderWhen` the user has `ManageMailingListSettings`, re-checked in `UpdateAsync`, and `AddSiteSettingsPermission` in the same startup (an architecture test fails without it). Add a "Test the connection" button that posts (`formaction`) to an admin controller action protected by the same permission. Describe the settings for the `Settings` recipe step with an `ISiteSettingsSchemaDefinition` under `[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]`.

**List card** (`DisplayDriver<MailingList>`):

- `Edit` returns `null` unless `list.ProviderName` is yours, so the card shows only on your lists.
- Override `BuildPrefix` to give the card its own prefix, so its fields can't collide with another card.
- Keep the settings in the list's properties: `list.Put(new KlaviyoListSettings { ... })` and read them with `list.TryGet<KlaviyoListSettings>(out var s)`. They travel with the `MailingLists` recipe step under `Properties.KlaviyoListSettings`.
- Never call `AddModelError` in the card. Check the settings in an `IMailingListSettingsValidator` registered in your `.Core` (`services.TryAddEnumerable(ServiceDescriptor.Scoped<IMailingListSettingsValidator, KlaviyoMailingListSettingsValidator>())`); `MailingListHandler` calls every validator, so the editor and the `MailingLists` recipe step check the same rules. Return nothing for lists that aren't yours. An architecture test fails when a driver of a catalog entry adds a model error.
- A hidden `Rendered` field tells `UpdateAsync` whether the card was on the page: in the post that switches a list to your provider it wasn't, so keep the settings as they are.
- Load remote choices (lists, audiences) with a short time-out, and fall back to a text box with a warning when the service can't be reached.
- Buttons that call the service (create a field, register the webhook) post with `formaction` to an admin controller protected by `ManageMailingLists`, and redirect back with a notice.
- Protect stored secrets (a webhook URL secret, a signing secret) with `IDataProtectionProvider.ProtectSetting` and a fixed purpose. Tell admins that a list imported to another site must register its webhook again: the new site can't read them.

## 7. Webhooks through the provider inbox

Copy `MailchimpWebhookEndpoint`:

1. `MapPost` a route with the list id and a random URL secret (`mailing-lists/{provider}/webhooks/{listId}/{secret}`), `AllowAnonymous()`, `DisableAntiforgery()`.
2. Cap the body size; use the inbox's `IProviderWebhookIngressLimiter` when present.
3. Find the list; it must be one of yours. Compare the URL secret in constant time (`WebhookSignatures.FixedTimeEquals`); answer 404 otherwise.
4. Verify the service's signature over the raw body before decoding it, with a replay window, when the service signs.
5. Parse; ignore (200) events you don't handle; refuse an event for another remote list.
6. `IProviderWebhookInbox.AcceptAsync` with your provider name, the service's delivery id (or the SHA-256 of the list id and the body when it has none), your handler's name and a payload with the list id, the body and the received time. Answer 200 for `Accepted` and `Duplicate`, 503 for anything else so the service sends it again.
7. Dispatch after the response with `AfterResponseWork.Schedule`.

Register the webhook for the events from people and admins only, never for changes your own API calls make, so your pushes don't echo back.

The handler (`IProviderWebhookInboxHandler`, `ReplaySafety => WebhookHandlerReplaySafety.NaturallyIdempotent`) maps each event to a `MailingListProviderChange` and calls `ApplyProviderChangeAsync`.

### The ordering rule

`ApplyProviderChangeAsync` ignores a change whose `OccurredUtc` is older than the member's `StatusChangedUtc`, so a late or replayed webhook never subscribes someone who unsubscribed since.

- Set `OccurredUtc` to **the service's own event time** (Mailchimp's `fired_at`, a member's `last_changed`). Use the received time only when the service gives none. Never use "now" for a change the service timed.
- `Status` is the new state, or `null` for a profile-only change. A move the transition table refuses is not made: a `Cleaned` address must confirm again on the site, whatever the service says.
- `Verified = true` only for the service's own confirmed double opt-in (or a sign-up through the service's form). From the reconciliation, a subscribed member counts as verified only on a list whose confirmation is the provider's, because the site's own pushes make members subscribed there too.
- A bounce is `Status = Cleaned` with the reason. Don't record a consent opt-out for it: an opt-out with no purpose would be kept as an every-purpose opt-out of the address (which refuses new sign-ups) and would mark the address's other lists unsubscribed instead of cleaned.
- An address change is `Email` (old) plus `NewEmail`.
- Set `ProviderMemberId` to the same id your pushes return.
- A new address is created only as `Subscribed` or `Pending`; it runs the observers like any sign-up, so a list with a welcome coupon issues a code for it.

## 8. Reconciliation

`GetChangesAsync(list, sinceUtc, cursor)`: read the members changed since `sinceUtc` (Mailchimp: `GET /lists/{id}/members?since_last_changed=`), map them with the same mapper as the webhooks, and return `NextCursor` (an offset, a page token) until the last page. Throw when the connection is missing, so the reconciliation keeps its old cursor. The first run reads the last day only.

## 9. Known gaps to keep in mind

The Mailchimp module documents these; check whether your provider needs to do better:

- Moving an existing list to the provider pushes only later changes; nothing copies the existing members (an `IResumableStep` with paced upserts, or the service's batch API, would).
- Changes made while the provider's feature is off wait in the outbox and go out when the feature is back on.
- When the provider confirms an address and the welcome coupon is delivered by the provider, the member service queues an `Upsert` so the code reaches the profile field.

## 10. Tests

Copy the Mailchimp tests:

| Test | What it covers |
| --- | --- |
| `MailchimpPushPlannerTests` | The member-state mapping and the follow-up status, as pure functions. |
| `MailchimpMailingListProviderTests` | `PushAsync` and `GetChangesAsync` against a fake `HttpMessageHandler` (`MailchimpTestDoubles`): the calls made, idempotency, retryable and final failures, merge fields, paging. |
| `MailchimpWebhookParserTests`, `MailchimpWebhookSignatureTests` | Parsing every event, and signatures (valid, wrong, expired, malformed). |
| `MailchimpWebhookEndpointTests` | Size cap, URL secret, signature, audience check, inbox acceptance and answers. |
| `MailchimpWebhookInboxHandlerTests` | Each event reaching `ApplyProviderChangeAsync` with the right change. |
| `MailchimpCredentialsTests`, `MailchimpSubscriberHashTests` | Credentials and keys. |

Also run the architecture tests (module layering, `PrivateAssets="none"`, the settings group permission test, the public API baseline) and, end to end on a running site, sign up through the widget, confirm, check the member and the remote list, unsubscribe at the service and see the webhook and the reconciliation bring it back.

## 11. Docs

In the same change: a Technical Manual page (`docs/modules/mailing-lists-{provider}.md`: feature, setup, list settings, push mapping, webhooks, reconciliation, limitations), a User Manual page (`docs/user-manual/mailing-lists/{provider}.md`, no code or ids), both sidebars, a `docs/feature-reference.md` row and the settings group row, the changelog, and the module's `README.md`. Follow `src/CrestApps.Docs/README.md`.
