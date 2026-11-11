# Workstream DRY: reuse, building blocks and layering

Part of the [November 2026 review plan](README.md). Paths are relative to `src/`. `M/X` is `Modules/CrestApps.OrchardCore.X`, `C/X` is `Core/CrestApps.OrchardCore.X`, and `A/X` is `Abstractions/CrestApps.OrchardCore.X`.

This workstream continues [`reusability-audit-2026-11.md`](../reusability-audit-2026-11.md) (2026-11-03). It does three things: says what that audit left open (section 1), adds the duplication it did not cover (section 2), and records layering and coupling as they are now (section 3). It follows the reuse ladder in [`building-blocks.md`](../building-blocks.md): use, extend, add the missing extension point, extract when a second module needs the concept, and only then keep it private.

Reviewed on `main` at `a12102c7a`, read-only. "Verified" means the code was opened; "search pass" means a count from a search that the implementer confirms before changing anything. Every extraction migrates all the listed copies in the same pull request series, updates the catalog in `building-blocks.md`, and adds a guard so the copy cannot come back.

## 1. Open items from the 2026-11-03 audit

Status against today's code. Sizes: S under a day, M a few days, L a week or more.

| Item (audit reference) | Status | Evidence | Remaining work | Size |
| --- | --- | --- | --- | --- |
| Contact Center `SkillProficiencies` mirror (defect 6, phase 5 item 1) | Open, and larger | Routing reads it at `C/ContactCenter.Core/Services/SkillMatching.cs:61`; property at `Models/AgentProfile.cs:208`; 26 uses in 14 files; `M/ContactCenter/SharedSkills/*` is 901 lines in 16 files (was 768) | `SkillMatching` reads `ISkillAssignmentService`; delete the sync, backfill and mapper; migrate entitlement recipes | M |
| Financial number generator's own store (P6) | Open | `C/Transactions.Core/Services/SequentialFinancialDocumentNumberGenerator.cs:55-63` keeps a `FinancialDocumentSequences` document under its own lock (`:27`) | Reimplement on `INumberSequence`; one data migration seeds each series from the stored last number | S |
| `ProviderCommand` on the leased queue (P5) | Open | `C/ContactCenter.Core/Models/ProviderCommand.cs:38,49,105` re-declares the fence, lease and next attempt; `ProviderCommandStore.cs:44-68` | Implement `ILeasedWorkItem`; needs live-call testing | M |
| Recording ingest (P5, D17) | Open | Telnyx: store 171 lines and service 511; Asterisk: store 138 and service 346; no `IRecordingIngestService` | `IRecordingIngestService` with a fetcher per carrier in `Telephony.Core`, on the leased queue | L |
| Messaging outbound state (P5) | Open | `C/Omnichannel.Messaging.Core/Models/OutboundDeliveryState.cs:12`, `Services/MessagingOutbox.cs:26` | Move to `ILeasedWorkQueue` | M |
| Setup guide (P8) | Not started | No `ISetupGuideStep`. `IStoreSetupStep` in `A/Abstractions/Commerce/StoreSetup.cs` has 9 implementations; `M/Workforce/Controllers/WorkforceSetupController.cs`; `M/Dispatch/Controllers/FieldServiceSetupController.cs:72` builds a `WorkforceSetupViewModel` | DRY-9 below | M |
| Search service forks (P9) | Open | 14 service files, 2,584 lines, in the six `AI.*.AzureAI` and `AI.*.Elasticsearch` modules | Blocked on client-factory overloads in CrestApps.Core; raise the request there | L |
| Connections for FTP and SFTP (P10) | Partly | Editors share `C/AI.FileTransfer.Core/Drivers/FtpConnectionDisplayDriverBase.cs`; the four modules never reference `DataConnection` | Store a connection id; add FTP and SFTP `IDataConnector` kinds | M |
| Connections for carriers (P10) | Open | `TelnyxSettings.cs:18` and `TelnyxSmsSettings.cs:20` each hold an `ApiKey`; `M/Shipping/Models/EasyPostSettings.cs:14`; `GooglePlacesSettings` and `GoogleMapsSettings` both exist | One credential per carrier or provider | M |
| Ad hoc purges (P13) | Open, 8 | `ShiftReminderLogStore.cs:26`, `WebhookDeliveryStore.cs:73`, `AIWebhookStores.cs:287`, `FieldStores.cs:51,137`, `WorkerLocationService.cs:174`, `IntervalStatisticsRoller.cs:388`, `AgentTimeSummaryRoller.cs:251`, `ProviderWebhookInbox.cs:466`. 24 retention policies exist, 20 in Contact Center | One `DocumentRetentionPolicyBase` subclass each | S each |
| Contacts abstraction (D3) | Not started | No `Omnichannel.Abstractions`; `IOmnichannelContactTypeProvider` and `OmnichannelContactPart` sit in `C/Omnichannel.Core`; WorkOrders (3 files), Assets (3) and PhoneNumbers.Verifications (4) import them | Create `Omnichannel.Abstractions` (also LAY-1) | M |
| Call-control results (D17) | Not started | `TelephonyResult`, `ContactCenterVoiceProviderResult` and `IVoiceAgentMediaProvider` (`Task<bool>`) are still separate | One result family | M |
| Contact Center distribution contracts (D18) | Partly | Only `IAgentProfileReader` exists. `ContactCenter.Core` is imported by 8 files in Messaging.Core, 19 in the Messaging module, 9 in Telnyx.Core, 3 in the Telnyx module, 2 in `M/Omnichannel.Sms` | Add queue, entry-point and agent-load readers; drop the references | L |
| Commerce types in platform libraries (D20) | Open | `A/Abstractions/Commerce/StoreSetup.cs`, `CustomerAdminPageAttribute.cs`, `CommerceConstants.cs`; `A/Reports.Abstractions/DataSources/CommerceReportDataSets.cs` | Move to `Commerce.Abstractions` | S |
| `Omnichannel.Core` references `AI.Core` | Open | csproj line 28; four files use it (`*AIConversationStarting*`, `*ConversationContextFactory*`) | Move the four to an AI integration library | S |
| Customer linked to Account; Vendor as an Account | Open | `C/Customers.Core/Models/Customer.cs:18,39,44` (`Kind`, `CompanyName`, `TaxId`, no account id); `M/Marketplace/Models/MarketplaceModels.cs:10` | As planned, with a data migration. SEC-C4 (reverse charge) touches the same fields: do them together | L |
| Messaging routing on the Contact Center pipeline | Open | `IMessagingRoutingStrategy` (2 implementations) and `DistributedCacheMessagingPresenceTracker.cs:14` beside 7 `IActivityRoutingStrategy` | As planned; live testing | L |
| Telnyx split | Open | `C/Telnyx.Core/Services`: 13 `TelnyxOutboundBridgeOrchestrator*` partial files, `TelnyxIvrProvider`, `TelnyxQueueTreatmentProvider`, `ContactCenterTelnyxInboundCallRouter` | As planned | L |
| Subscriptions through Orders | Open | `C/Subscriptions.Core/SubscriptionCheckout.cs:17`; `GatewayBillingSubscriptionLifecycleHandler.cs:162` | As planned | L |
| `PartyRef` adoption (D1) | Open | Used only in Parties, Assets and WorkOrders. `Transaction.cs:43` and `Subscription.cs:39` use `CustomerOwnerKind`; `Calendar.cs:40` `CalendarOwnerKind`; `StockLocation.cs:30` a string kind | Adopt per module | M each |
| `NamedAddress` adoption (D4) | Open | Used only in Addresses and Commerce. `StockLocation`, `InventoryLocation`, `PickupLocation` (`ShippingSettings.cs:57`), `Register` (`PosModels.cs:9`) and `WorkforceLocation` keep their own types | Adopt and migrate | M |
| `ITimeZoneResolver` (D7) | Not started | `TeamTimeZoneResolver.cs`, `WorkerTimeZones.cs`, `TimeTrackerTimeZone.cs`, `ReportTimeZone.cs` | Build the resolver chain | M |
| Scheduling and Bookings hours on Business Hours (D6) | Open | `SchedulingWorkingHours.cs:6`, `BookingPart.cs:145`, WorkOrders `WeeklyHours` (`WorkOrderSettingsRecords.cs:116`) | `IHoursScheduleSource` adapters, then migrate | M |
| Feature ids as literals | Open; the audit's count is wrong | The ratchet lists 57 literals in 22 files, not 47 (`tests/CrestApps.OrchardCore.Tests/Architecture/FeatureIdLiteralRatchetTests.cs:24-45`); 30 are in `M/Recipes` | Module-owned recipe schemas (LAY-4) | M |
| Paging in memory (defect 23) | Open, wider than recorded | About 25 controllers (search pass). `CouponsAdminController.cs:115,161` loads all coupons | Page in SQL where a list can grow (CQ-4) | S each |
| Contact Center data governance (defect 19) | Open | `ContactCenterDataGovernanceCatalog.cs`; no `IPersonalDataContributor` in Contact Center | Add contributors | M |
| API idempotency outside the filter | Open | Hand-rolled at `M/Dispatch/Api/FieldServiceApiEndpoints.cs:175` and `M/Workforce/Api/WorkforceApiEndpoints.cs:523` | DRY-8 | S |
| Tenant provisioning beside the broker | Open | `C/Subscriptions.Core/Services/TenantProvisioningService.cs:50-52` injects `IShellHost`, `IShellSettingsManager` and `ISetupService` | Provision through `ITenantHierarchyBroker`; do it with SEC-C2 | M |
| `IRefundAllocator`, line-level refunds (D11) | Not started | No such type | As planned | M |
| Slot filters | Open | `IBookingSlotFilter` and `IAppointmentSlotFilter` both exist | Bookings time slots on Scheduling | M |
| `Order` carries other modules' fields | Open | `C/Orders.Core/Models/Order.cs:96,102,107,134,139` | Parts or `Properties` owned by each extender | M |
| Legacy AI webhook stores | Open | `C/AI.Core/Webhooks/AIWebhookStores.cs` (304 lines) is kept only for `AIWebhookImporter` | Delete after one release with the importer | S |

Correct the audit document while here: 57 feature-id literals (not 47), 21 files over 1,000 lines (not 18), and about 25 in-memory pagers (not "a few small settings lists").

## 2. New building blocks and extractions

Ordered by reuse value. Each names its owner, contracts, consumers, migration order, data migration and guard.

### DRY-1. Rate limits and attempt limits (M, low risk)

The full design and work items are in [`02-payment-rate-limits.md`](02-payment-rate-limits.md); this entry records why it is a platform building block and not a Checkout feature.

- `C/Checkout.Core/Services/PaymentAttemptLimiter.cs:40-103` (verified) serves 11 scopes in Checkout, Orders and Customers. Seven are not payments: `coupon-code`, `gift-card-code`, `track-order`, `find-order`, `find-order-email`, `order-create-account`, `referral-link`. All share one limit.
- `M/Users/Services/EmailSignInCodes.cs:88-113` (verified) is the same algorithm without the lock, which is the race in SEC-P1.
- The kiosk PIN lockout is a third variant (`DefaultTimeClockService.cs:386-419`, search pass).
- Three modules register an Orchard rate-limit group with identical blocks: `M/Addresses/Startup.cs:58`, `M/AI.WebMcp/Startup.cs:83`, `M/NLWeb/Startup.cs:118`. Carts, Checkout, Orders, Subscriptions and Wizard tag groups and register nothing.

Owner: platform infrastructure, with no new module. The contract (`IAttemptLimiter`) goes in `A/Abstractions/Throttling`; the implementation, `ClientNetworkKey` and the `RateLimitsOptions` helpers go in `C/Core/Throttling`, beside `SignedTokenService` and `EgressNetwork`. The request layer stays an extension of `OrchardCore.RateLimits`: a "Payment Rate Limits" feature in Checkout that depends on it, and `[RequireFeatures]` startups in the other modules. Consumers and order: Checkout, Orders and Customers (RL-2 to RL-5); `EmailSignInCodes` (SEC-P1, after its local fix); the kiosk PIN lockout; the three identical group registrations move to `AddClientGroupRateLimit`; Scheduling's public booking page (SEC-P9); the AI completion API quota (SEC-A5). No data migration.

Not consumers: `ProviderWebhookIngressLimiter` (a concurrency gate and a token bucket per provider in memory: a different job, which stays), the AI chat rate-limit tiers (enforced in the CrestApps.Core package), `DialerAttemptLimit` (a dialing rule), and `AgentCheckoutService`'s cap on open checkouts (a quota).

### DRY-2. Secret tokens, signed links and one-time codes (M, medium risk)

**Evidence.**

- Seven "random token, store its SHA-256" helpers in four encodings (verified): `PortalSecrets.cs:13-23`, `DelegatedAccessTokens.cs:18-29`, `C/ContactCenter.Core/Services/SecureCaptureAccessToken.cs:20-39`, `AgentPlatformApiKeys.cs:20-27`, `AIApiKeys.cs:71,98,223`; and `PunchoutService`, `GuestSessionTokenManager` (search pass).
- `SecureCaptureAccessToken.cs:20` builds its token from two `IdGenerator.GenerateId()` values instead of `RandomNumberGenerator`. In practice these are random GUIDs, but an id generator makes no promise about unpredictability; a token that opens a card-capture page should not depend on it.
- Tokens stored and looked up in plain text (verified): `Calendar.FeedToken` (`CalendarStore.cs:48`), `WorkerPortalSettings.FeedToken` (`C/Workforce.Core/Services/DefaultWorkerFeedService.cs:68`, compared with `string.Equals`). Also `Appointment.ManageToken`, `CalendarAttendee.ResponseToken`, `ProductFeedSettings.Key` (search pass). Anyone who can read the database or a backup can open every feed.
- Four classes build their own time-limited protector instead of `SignedTokenService` (verified): `C/Dispatch.Core/Services/VisitCustomerServices.cs:52`, `MessagingAttachmentLinks.cs:47`, `DelegatedEntryController.cs:60`, `TimeClockKioskController.cs:204`. `CallRecordingLinks` and the Telephony OAuth state (which has no expiry) do the same with a plain protector (search pass).
- One-time codes: `EmailSignInCodes` issues, hashes, counts and verifies by itself; phone verification and two-factor paths have their own.

**Design, in `C/Core/Security` (platform library, no feature needed):**

```csharp
public static class SecretTokens
{
    public static string Create(int bytes = 32);   // RandomNumberGenerator, base64url
    public static string Hash(string token);       // SHA-256, lower-case hex, for new stores
    public static bool FixedTimeEquals(string left, string right);
}
```

**Migration order.** (1) `SecretTokens.Create` replaces token *generation* in the seven helpers. Each store keeps its own stored hash format and comparison, so no stored value changes and nothing has to accept several encodings. (2) The six hand-rolled signed links move to `SignedTokenService` with a distinct purpose each; old links stay valid until they expire because the protector purpose strings are kept. (3) Plain-text tokens: add a hash column beside each, backfill it from the stored value in a data migration, look up by hash, then drop the plain column one release later. Existing feed and manage links keep working because the token itself does not change.

**Deferred.** A shared `IOneTimeCodeService` (issue, hash, count, verify) has one consumer today (`EmailSignInCodes`, fixed in place by SEC-P1). Extract it when a second consumer moves (phone verification), not before.

**Guard.** `DuplicatePatternRatchetTests`: no `SHA256.HashData(` next to `RandomNumberGenerator` outside `C/Core/Security`, and no `CreateProtector(...).ToTimeLimitedDataProtector()` outside `SignedTokenService`, with today's sites as a shrink-only list.

### DRY-3. Durable jobs, backfills and backoff (L in total; do the low-risk ones first)

**Evidence.** `ILeasedWorkQueue` has three item types (`ContactCenterOutboxMessage`, `ProviderWebhookInboxMessage`, `OutreachBatch`). Hand-written due-list queues with attempt counts, beyond those in section 1: `WebhookDeliveryStore.cs:25` (the platform's own outbound webhooks), `TenantProvisioningJobStore.cs:53`, `CallbackRequestStore.cs:28`, `BookingStores.cs:300`, `DefaultGeocodeQueue` (verified in part). Nine `*Backfill` classes walk a document-id cursor by hand, and one keeps its cursor in a field, so it restarts from zero after a restart (`QueueItemDialerProfileIndexBackfill`, search pass). `BackgroundWorkPacer` is used by 2 of 101 background tasks. About 12 hand-rolled backoff formulas exist, two of them copies of `LeasedWorkBackoff.Exponential` (search pass).

**Design.** No new module. Add to `C/YesSql.Core`:

```csharp
public interface IDocumentBackfill
{
    string Name { get; }
    Task<long> RunPageAsync(long afterDocumentId, int pageSize, CancellationToken cancellationToken); // returns the last id handled, or -1 when done
}
```

with one runner background task that keeps each backfill's cursor in a document, paces with `BackgroundWorkPacer`, and disables itself when every registered backfill is done (this is also CQ-5). Use `LeasedWorkBackoff` for every retry delay that is not an HTTP retry; HTTP retries use .NET resilience pipelines (DRY-7).

**Order.** Webhook deliveries; geocode queue; tenant provisioning jobs; booking balances; the nine backfills; then the live-call items in section 1 (`ProviderCommand`, recording ingest, Messaging outbound), which need live-call testing. Data migration: additive index columns only (lease owner, fence, next attempt).

**Guard.** A ratchet on classes named `*Backfill` that do not implement `IDocumentBackfill`, and on `Math.Pow(2,` in service code outside `LeasedWorkBackoff`.

### DRY-4. Choosing a provider by name (M, low to medium risk)

**Evidence (5 of 15 resolvers read).** Fifteen resolvers pick "the provider" with seven different default rules: `DefaultCarrierProviderResolver.cs:17-28` (a site setting and an enabled check), `CheckoutPaymentProviderResolver.cs:27` (case-sensitive), `DefaultGeoService.cs:177` (a private generic `Find<T>`), `MarketplaceService.cs:385` (the named one, or the only one), `DispatchOptimizationService.cs:122` (the last one registered). Payment keys are also matched inline in five more places with mixed case rules, so a key can resolve in one place and not in another (search pass). CQ-7 counts 64 `injected.FirstOrDefault()` picks.

**Design.** The defect is the inconsistent matching rule, not the absence of a framework. Add one small helper to `A/Abstractions/Providers`:

```csharp
public static class ProviderLookup
{
    // Always case-insensitive. Returns null when the name is empty or unknown.
    public static T Find<T>(IEnumerable<T> providers, string name, Func<T, string> nameOf);

    // The named provider; otherwise the only one registered; otherwise null.
    public static T FindOrSingle<T>(IEnumerable<T> providers, string name, Func<T, string> nameOf);
}
```

Each resolver keeps its own default rule (a site setting, an enabled check), states it in its XML documentation, and calls the helper for the match. No "first registered" or "last registered" default remains: a resolver with several providers and no configured name returns null and the caller reports "no provider chosen". A generic options-driven selector was considered and rejected as more machinery than the defect needs.

**Consumers and order.** The 15 resolvers and 10 inline lookups. Start with Geo, Shipping and Marketplace; then payment keys (one comparison rule everywhere: check stored provider keys for case differences first, and normalize them in a data migration if any exist); Telephony and Contact Center voice last, with live-call testing.

**Not consumers.** The tax provider chain (a chain of responsibility) and `SmsProviderRouter` (it routes by the sending number).

**Guard.** Widen `DependencyInjectionArchitectureTests`' `.FirstOrDefault()` rule to all of `src/`.

### DRY-5. Notifying a person or a party (design note first; build later, L)

**Status after review: do not build this yet.** It is a new feature (consent, templates, moving Workforce's inbox onto Orchard Notifications), not a small extraction, and nothing in the security work needs it. Write the design below up as a note under `docs/engineering/`, agree it with the owner, and start when a second module is about to add its own notifier. Until then, the ratchet at the end of this item stops new direct senders. The evidence and the proposed shape are kept here for that note.

**Evidence (contracts verified).** Three services each "send this to that person over their channel": `IWorkforceNotificationChannel` (`A/Workforce.Abstractions/Services/IWorkforceNotificationChannel.cs`: inbox, web push, e-mail), `IVisitNoticeSender` (`A/Dispatch.Abstractions/Services/DispatchSeams.cs:119`: SMS and e-mail), and `ICustomerOwnerNotifier` (`A/Customers.Abstractions`). Orchard's `INotificationService` is used by four projects. 12 call sites send through `IEmailService` directly, and Reports builds the same attachment mail twice (`EmailReportTask.cs:193`, `ReportSubscriptionService.cs:245`) (search pass). `ShiftReminderLog` and `CalendarReminderLog` are nearly identical (search pass).

**Design.** A new building block `CrestApps.OrchardCore.PartyNotifications` (`A/PartyNotifications.Abstractions`, `C/PartyNotifications.Core`, feature `CrestApps.OrchardCore.PartyNotifications`):

```csharp
public interface IPartyNotifier
{
    Task<PartyNoticeResult> NotifyAsync(PartyRef party, PartyNotice notice, CancellationToken cancellationToken = default);
}
// PartyNotice: Kind (an open string a module registers), Subject, Model (named values for the template),
// Url, and an optional ContactInfo for a guest who has no record.

public interface IPartyNoticeChannel   // e-mail, SMS, web push
{
    string Name { get; }
    Task<bool> CanReachAsync(PartyContact contact, PartyNotice notice, CancellationToken cancellationToken);
    Task SendAsync(PartyContact contact, RenderedNotice notice, CancellationToken cancellationToken);
}
```

Rules: a party of kind User goes through Orchard Notifications, so the user's own notification preferences apply. Customers, contacts and guests go through channels, after an `IConsentService` check per channel and address. Templates are one Liquid template per notice kind with a module default and a site override: lift this from `ICommerceEmailSender` (audit D14), which then becomes a thin layer over the notifier. `IPartyDirectory` (Parties) resolves the party to its contact details.

**Order.** `ICustomerOwnerNotifier` becomes the first implementation; Dispatch visit notices; Workforce (its inbox and channel preferences need a data migration to Orchard Notifications and consent); Reports mails; AI alerts. The 12 direct `IEmailService` call sites move as their modules are touched.

**Guard.** A ratchet on `IEmailService` and `ISmsService` injection outside the channel implementations and Orchard-facing modules, with today's sites as a shrink-only list.

### DRY-6. Record events, notes and timelines (M, low risk)

**Evidence (types verified).** Eight types repeat "when, kind, message, actor" under drifting names: `SubscriptionEvent` (`SubscriptionLine.cs:33`), `InstallmentPlanEvent.cs:6`, `TransactionEvent.cs:8`, `VisitStatusChange` (`ServiceVisit.cs:60`), `TimeEntryChange` (`TimeTrackerModels.cs:105`), `MessagingConversationEvent.cs:20`, `OrderNote` (`Order.cs:401`), `WorkOrderNote` (`WorkOrder.cs:220`). Three projects write to Orchard's Audit Trail (`HierarchyAuditLog.cs`, `TimeTrackerAuditTrail.cs`, `RecordingMediaDeletionAuditTrailHandler.cs`). Three near-identical timeline cards exist in Transactions and Subscriptions views (search pass).

**Design.** Share the display, not the stored types. In `C/Core`: a view model `TimelineEntry { CreatedUtc, Kind, Message, ActorId, IsInternal, Tone }` and one `RecordTimeline` shape that renders a list of them in time order, with the actor's display name through `IDisplayNameProvider` and times through `ILocalClock`. Each module projects its own stored events and notes to `TimelineEntry`. The stored types stay as they are: making eight stored types derive from a base would mean remapping JSON property names and adds a polymorphism risk for little gain. New event or note types added from now on use the same property names (`CreatedUtc`, `Kind`, `Message`, `ActorId`); say so in the catalog.

An optional Audit Trail bridge (a `[RequireFeatures]` startup that writes selected event kinds to Orchard's Audit Trail) is a separate, later item.

**Order.** The shape; Transactions and Subscriptions timelines (the three near-identical cards); Orders and WorkOrders notes; Dispatch, TimeTracker, Messaging. **Guard:** the catalog row "Record timelines" in `building-blocks.md`.

### DRY-7. HTTP clients, egress and inbound signatures (M)

**Evidence.**

- About two dozen `AddHttpClient` registrations (23 to 26 depending on how they are counted), 8 with a resilience handler.
- **A likely defect (verified in code by three readers, not run):** `M/Telnyx/Startup.cs:36` adds the resilience handler to the client named `TelnyxConstants.ProviderTechnicalName`, but `:63` registers `AddHttpClient<TelnyxApiClient>(...)`, whose client name is the type name. The typed client that "every Telnyx call now goes through" therefore does not get the handler, although the comment at `:60-62` says it does. Fix: register the typed client on the named client (`AddHttpClient<TelnyxApiClient>(TelnyxConstants.ProviderTechnicalName, ...)`) and add a test that resolves the client's handler chain. Keep `DisableForUnsafeHttpMethods`. `TelnyxApiClient` has its own retry policy inside it, so joining the two would retry a GET at both layers: remove the transport-level retry for this client (keep its timeout and circuit breaker) and let the client's own policy decide.
- Two private-address classifiers besides `EgressNetwork`: `C/TenantHierarchy.Core/Guards/EgressAddressClassifier.cs:16` (stricter) and `M/Calendars/External/ICalendarUrlProvider.cs:101` (weaker, SEC-P5).
- Outbound clients that take a user- or administrator-supplied host without the guard: Workforce web push (SEC-P4), Calendars external (SEC-P5), Messaging inbound media (SEC-P10f), OSRM (SEC-P10b), and the AI ones in the AI section of `01-security-findings.md`.
- `IWebhookSignatureValidator` is registered (`ProviderWebhookInboxServiceCollectionExtensions.cs:33`) with two implementations, and nothing resolves it (verified). EasyPost, Twilio and the ACP timestamped HMAC each verify by hand (search pass).

**Change.** (1) Merge the stricter TenantHierarchy rules into `EgressNetwork` and delete both other classifiers. (2) Add `AddEgressGuardedHttpClient` overloads that also attach the standard resilience handler, and a `ReadBoundedAsync(HttpContent, long maxBytes)` helper in `C/Core/Http` (lifted from `MessagingInboundMediaIngestor`). (3) Move every outbound client whose host comes from stored configuration or request data onto it. (4) Either use `IWebhookSignatureValidator` from the EasyPost, Twilio and ACP endpoints or delete the contract; using it is right, because the inbox already depends on it by name (catalog row "Provider webhooks").

**Guard.** A new `T/Architecture/OutboundHttpArchitectureTests.cs`: every `AddHttpClient` is either egress-guarded or on an allowlist of fixed-host provider clients (Stripe, Telnyx, TaxJar, EasyPost); extend `ProviderHttpRetryArchitectureTests` to typed clients.

### DRY-8. One API toolkit for minimal APIs (S to M)

`IdempotencyEndpointFilter` and `ApiProblems` live in `C/Commerce.Api.Core`, so Dispatch and Workforce hand-roll both (`M/Dispatch/Api/FieldServiceApiEndpoints.cs:175`, `M/Workforce/Api/WorkforceApiEndpoints.cs:523`), and the AI endpoints return three error shapes (CQ-11). Move the filter, `IdempotencyPolicy`, `IdempotentOperation`, `ApiProblems`, and the "Api scheme, permission, problem details" group builder that `MapCommerceApi` wraps into `C/Core` (folder `Api`, namespace `CrestApps.OrchardCore.Api`). No new project. `Commerce.Api.Core` keeps commerce routes and type-forwards the moved types. Apply SEC-C17l (body fingerprint, protected stored answers, scoped anonymous keys) while moving the filter. Consumers: Commerce, Dispatch field service, Workforce, Calendars, AI endpoints. CQ-11's "one API error contract" row is this item.

### DRY-9. Setup guide (M, low priority; audit P8)

Nothing earlier in this plan depends on it: the "Protect payments" step in RL-6 uses the existing `IStoreSetupStep`. One contract, `ISetupGuideStep { string Guide; string Title; int Order; Task<SetupStepState> GetStateAsync(); string ActionUrl }`, in a small `CrestApps.OrchardCore.SetupGuide` building block (Abstractions, and a module with one controller and one shape). Commerce (`IStoreSetupStep`, 9 implementations), Workforce and Dispatch become providers; `FieldServiceSetupController` stops borrowing `WorkforceSetupViewModel`. The payment-protection step in RL-6 and RL-7 is a provider too. `IStoreSetupStep` stays for one release as an adapter.

### DRY-10. Admin view helpers (M, low risk)

**Evidence (counts verified).** `LocalAsync(DateTime utc)` is copied as a Razor `@functions` block in 59 views across 24 modules. Status-to-badge switches are inline: 215 views contain literal badge classes and about 18 repeat a switch (search pass). The "Add type" modal is copied in 12 index views (`M/AI.WebCrawlers/Views/WebCrawlers/Index.cshtml:53` is typical; four copies share the id `available-rule-types-title`), while Contact Center has a private abstraction in `_CatalogList.cshtml`. KPI tile markup repeats in about 9 views although Reports has a metric partial. File-size formatting has 7 copies; `_GeoMap` and `_DispatchMap` are identical (search pass). The solution has exactly one tag helper (in Seo).

**Design, in `C/Core`:** an `ILocalClock` extension `ToLocalDateTimeAsync(utc)` and the `<local-time>` tag helper (UX-4); a `<status-badge>` tag helper with `StatusBadgeOptions.Map<TEnum>(value, tone)` registered by each module (UX-6); shapes `TypePickerModal` and `MetricTile`; `FileSizeFormat`. The map partial moves to `Geo.Core`.

### DRY-11. Money display and the store currency (M)

`CheckoutSettings.Currency` is read directly 23 times in 12 projects although `IShopperCurrency` exists, and that contract lives in `Products.Core`. Three settings default to "USD" independently (`CheckoutSettings.cs:19`, `SubscriptionSettings.cs:15`, `WorkOrderType.cs:84`). Three display styles: `CurrencyScale.Format` (60 uses), `ProductPriceFormatter` (132), and fixed two-decimal strings (33). `ToStoreCurrencyAsync` is copied in `B2BApprovalService.cs:200` and `CustomerPurchasesService.cs:173` (counts verified).

Add `IStoreCurrency` (the store's currency, from the store profile) and `IMoneyFormatter` (audit D10) to `A/Payments.Abstractions`, with the implementation in `C/Commerce.Core`. `ProductPriceFormatter` becomes a thin caller; the 33 fixed-format strings and the 23 direct reads move over. **Guard:** a ratchet on `.ToString("F2")`, `"N2"` and `"0.00"` in commerce views and services.

### DRY-12. Smaller extractions (S each)

| Id | Evidence | Change |
| --- | --- | --- |
| a. Reading protected settings | 44 to 51 files outside `C/Core` call `CreateProtector(` (two counts differ; recount when starting); 17 private `Unprotect*` helpers (6 in Asterisk, 3 in phone verification, 2 in DncRegistry); one Asterisk copy returns the ciphertext when unprotecting fails (search pass) | `UnprotectSetting` from `ProtectedSettingExtensions` everywhere; a failure returns null and logs once. Ratchet `CreateProtector(` outside `C/Core` |
| b. API keys | `AIApiKeyAuthenticationHandler` and `AgentPlatformAuthenticator` read `X-Api-Key` or a bearer value and hash the same way (search pass) | Share `SecretTokens` (DRY-2) and one `ApiKeyHeaderReader` |
| c. Upload validation | 11 upload paths with private size constants; only Downloads and AI.Documents run Orchard's scan pipeline; the same 20 MB audio allowlist is copied in `MyVoicemailGreetingController.cs:32-46` and `VoiceMediaItemDisplayDriver.cs:25-39` (search pass) | `UploadPolicy { MaxBytes, AllowedExtensions, AllowedContentTypes, ScanForMalware }` and `IUploadValidator` in `C/FileStorage.Core`, running Orchard's file scan when that feature is on; each path names a policy |
| d. Party pickers | Asset owners (`AssetOwnerEndpoints.cs:25`) and work-order customers (`WorkOrderCustomers.cs:209`) are nearly identical; `messaging-contact-picker.js` reimplements `item-selector.js`; Scheduling hard-codes the Calendars user-search URL (`SchedulingResourceFields.Edit.cshtml:10`, verified) | One Parties search endpoint over `IPartySource`, behind a permission, used by `item-selector.js` |
| e. Workforce decisions | Nine `DecidedBy` properties; seven records repeat status, decided-by and decided-at | One `Decision` value object inside Workforce. A cross-module approval service is rejected: the side effects differ |
| f. Dashboard widget cache | Four copies of the same key and clamp logic: `SalesSummaryWidget.cs:49`, `SubscriptionSummaryPartDisplayDriver.cs:62`, `ReportWidgetService.cs:83`, `ReportDesignWidgetService.cs:109` (verified) | `IWidgetCache.GetOrCreateAsync(key, minutes, factory)` in `C/Reports.Core` (the Reports widgets are the owner) |
| g. Distance math | Haversine copied at `DefaultTimeClockService.cs:667` and `M/Classifieds/Services/GeoDistance.cs:20`; `GeoMath` sits in `Geo.Core` (verified) | Move `GeoMath` to `Geo.Abstractions`; delete the copies |
| h. ContentTransfer CSV | `CsvContentTransferFileFormatProvider.cs:171,230,339-366` hand-rolls a parser and a writer with no formula guard (verified); the audit's defect 12 fix missed it | `CsvReader` and `CsvWriter` |
| i. Reminder logs | `ShiftReminderLog` and `CalendarReminderLog` are nearly identical (search pass) | Shift reminders on calendar reminders, as the audit planned for defect 20, or one log type in Calendars.Abstractions |

Rejected after checking: a shared debounce beyond UX-8 (one named definition), chart code (only Reports), Telnyx Ed25519 and Stripe SDK signatures (provider-specific), download and cookie protectors, ICS and GDPR exports, a shared list-options base (the 18 classes share only `Search`), and a base class for the 95 permission providers (boilerplate, low value).

## 3. Layering and coupling today

### LAY-1. Building blocks with no Abstractions project (M each)

Consumers reference the implementation library to reach a contract:

| Block | Interfaces in `.Core` | Projects that reference it |
| --- | --- | --- |
| Products | 18 | 24 |
| Orders | 11 | 18 |
| Omnichannel | 45 | 12 |
| Carts, Inventory, Shipping | not counted | not counted |

`Customer` is still in `Customers.Core` (9 referencing projects). Create `Products.Abstractions`, `Orders.Abstractions` and `Omnichannel.Abstractions` (this is also audit D3), move the contracts and the records other modules read, and leave type-forwards for one release. Then Carts, Inventory and Shipping. **Guard:** extend `LayeringRatchetTests` with "a project outside domain X references `X.Core` only through an allowlist", shrink-only.

### LAY-2. `.Core` references that run the wrong way (M)

37 references go from one domain's `.Core` to another's. The ones to reverse:

- `Workforce.Core` → `Reports.Core`
- `Checkout.Core` → `Transactions.Core`, and `Subscriptions.Core` → `Checkout.Core`
- `Orders.Core` → `Customers.Core`, while the Customers module references `Orders.Core` and `Checkout.Core`
- `Omnichannel.Core` → `AI.Core`
- `Omnichannel.Messaging.Core` and `Telnyx.Core` → `ContactCenter.Core`

Each is fixed by LAY-1 (contracts in Abstractions) plus a `[RequireFeatures]` integration in the consumer.

### LAY-3. `Reports.Core` is the designer (M)

48 of its 56 files are the report designer, and `IndexedReportDataSet` sits beside them, so 27 projects reference the designer engine to register one data set. Move `IndexedReportDataSet` and the data-set base classes to `A/Reports.Abstractions` or a slim `C/Reports.DataSets.Core`; the designer becomes `C/Reports.Designer.Core`, referenced only by the Reports module. This was phase 3 item 4 of the audit.

### LAY-4. Building blocks that still name their consumers (M)

- `A/Accounts.Abstractions/AccountPageTabs.cs:41-90` hard-codes Opportunities, Leads, Orders, Invoices and Service. Tabs become registrations by the modules that add them (`AddAccountPageTab`).
- `C/Recipes.Core/Schemas/Steps` holds about 25 domain schemas (Contact Center 8, Omnichannel 9, Customers, Stock, Stripe, Telephony and others), and `M/Recipes` has 154 `[RequireFeatures]` startups and 30 of the 57 feature-id literals. Each module owns its recipe-step schemas; `M/Recipes` keeps only the platform's.
- Fixed since the audit: time-punch sources are an open provider.

### LAY-5. Reusable pieces locked inside a consumer (S each)

| Piece | Where it is | Where it belongs |
| --- | --- | --- |
| `IPaymentAttemptLimiter` | Checkout | `A/Abstractions/Throttling` and `C/Core/Throttling` (DRY-1) |
| `IdempotencyEndpointFilter`, `ApiProblems` | `Commerce.Api.Core` | `C/Core/Api` (DRY-8) |
| `IPaymentIssueRecorder` | Contract in `A/Transactions.Abstractions`, registered in `C/Transactions.Core/TransactionsServiceCollectionExtensions.cs:40`; the catalog says Checkout owns it | Correct the catalog row to Transactions, and give Checkout the logging default (CQ-6) |
| `GeoMath` | `Geo.Core` | `Geo.Abstractions` (DRY-12g) |
| `IShopperCurrency`, `ProductPriceFormatter` | `Products.Core` | `Payments.Abstractions` and `Commerce.Core` (DRY-11) |
| The type-picker modal | Contact Center `_CatalogList.cshtml` | `C/Core` shape (DRY-10) |
| The metric tile | Reports | `C/Core` shape (DRY-10) |
| `telephonyClient.escapeHtml` | Telephony script | Resources (UX-8) |

### LAY-6. Service locator and optional dependencies that hide a disabled feature (M)

The counts and the fix pattern are in CQ-7. The sites where a missing feature silently changes behavior:

- Transactions: `DefaultTransactionCommandService.cs:313,363-365,465-467`; `TransactionCustomerPageService.cs:91-92`; `TransactionSettlementCheckoutHandler.cs:508` resolves `IPaymentIssueRecorder` optionally.
- Orders, for `ICustomerService`: `CartCheckoutService.cs:82`, `DraftOrderService.cs:191`, `QuoteService.cs:144`.
- Optional constructor parameters that turn a behavior off: `DefaultCheckoutEngine.cs:90` (`IPaymentIssueRecorder`), `ReportSubscriptionService.cs:52` (`IEmailService`, although the feature already depends on Email: `M/Reports/Manifest.cs:35`), `M/Transactions/Controllers/AdminController.cs:82`, `TelnyxOutboundBridgeOrchestrator.cs:47,52,53`, `TelnyxTelephonyProvider.cs:73`, `AIDataSourceConnectionPicker.cs:39` and the two AI data-source handlers (`IDataConnectionResolver`), `DefaultSubjectActionExecutor.cs:44`.

Fix each with a null-object default registered by the owner, or a `[RequireFeatures]` handler in the consumer. Where the feature is already a manifest dependency (Reports and Email), make the parameter required.

### LAY-7. Manifests (S to M)

- No manifest depends on `OrchardCore.RateLimits`, although five modules tag groups (RL-2 fixes Checkout).
- `M/Omnichannel.Sms/Manifest.cs` hard-depends on AI, AI chat, Managements and the provider inbox: split the AI automation into its own feature so SMS can be enabled without AI.
- Contact Center has 18 features over a `ContactCenter.Core` of 845 files, 503 in a flat `Services/` folder. Split by feature after D18 (section 1).
- Module-to-module project references rose from 75 to 90 in 58 modules (AI is referenced 17 times, Resources 7, Webhooks 7). The layering ratchet confirms none uses another module's types; keep it that way, and prefer a feature-id constant over a new project reference when only the dependency is needed.

## 4. Order of work

1. **Guards and small moves first** (S): DRY-12a, g, h; LAY-5 rows that are one move; the audit corrections. These stop new copies immediately.
2. **Building blocks that follow the security fixes** (M): DRY-1 (the attempt limiter and its consumers), DRY-2 steps 1 to 3 (token generation, signed links, hashed tokens), DRY-7 (egress classifiers, HTTP clients, signature validators). The security findings carry local fixes and do not wait for these.
3. **Platform DRY** (M): DRY-4, DRY-6, DRY-8, DRY-10, DRY-11, DRY-3 (low-risk half), DRY-9.
4. **Re-layering** (M): LAY-1 to LAY-4, with LAY-6 alongside.
5. **Larger extractions** (L): the rest of DRY-3, the section 1 items marked L, and DRY-5 once its design note is agreed. The live-call ones (ProviderCommand, recording ingest, Messaging routing, the Telnyx split) are scheduled with live-call testing.
