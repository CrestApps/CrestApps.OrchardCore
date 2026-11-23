# Mailing lists plan (newsletter sign-up, e-mail verification, welcome coupon, Mailchimp)

Status: ML-D1 to ML-D6 chosen 2026-11-22 (the recommended options); ML-D7 to ML-D10 built as proposed. Built 2026-11-22 (ML0a to ML8); see "As built" at the end for where the build differs from this plan and what is left.

## Goal

Let a site collect newsletter subscribers and reward them:

- A visitor (signed in or not) enters an e-mail address in a sign-up widget and joins one or more mailing lists.
- The site owner decides, per list, whether the address must be verified first (double opt-in) or is subscribed at once (single opt-in).
- The site owner can attach a one-time welcome coupon to a list, and decides whether the coupon is sent at sign-up or only after the address is verified.
- Every e-mail carries a working unsubscribe link, and an unsubscribe anywhere (this module, a Marketing campaign, the CRM's **Do not email**, a provider) is honored everywhere.
- The list can live on the site (the **Local** provider) or at an outside e-mail marketing service. A **Mailchimp** module is the first outside provider; others (Klaviyo, Brevo, Constant Contact, SendGrid Marketing) plug in the same way.

The module manages lists and members. It does not write or send newsletters itself: bulk sending is Outreach (with Marketing campaigns as the authoring UI) for local lists, and the provider's own campaign tools for outside lists.

## Where things stand

| Piece | Where | What it means for this plan |
| --- | --- | --- |
| Newsletters, mailing lists, double opt-in | Nowhere. A repository search finds only two doc lines | New module |
| `commerce-parity-plan.md` P-D4 | "Email campaigns: build them, or integrate with Mailchimp or Klaviyo?" Default taken: **Integrate first** | Backs a provider seam with Mailchimp first |
| `commerce-parity-plan.md` (parity table) | "Email campaigns and newsletters: No: the omnichannel email channel is for conversations" | The messaging workspace is not the newsletter tool |
| **Subscriptions** module | Paid recurring plans sold through Checkout (`Subscription`, `ISubscriptionStore`, `account/subscriptions`, stereotype `Subscription`) | The name is taken. A newsletter "subscription" must not reuse it (ML-D1) |
| **Coupons** (in Checkout) | `Coupon`, `CouponCode` (generated codes), `ICouponStore.GenerateCodesAsync`, `ICouponRedemptionService`, the `discount/{code}` link, `Coupon.CustomerEmail` | Can issue a single-use code from a template, but a generated code can't be bound to one e-mail and has no expiry of its own (ML-D4) |
| **Referrals** (Customers) | `ReferralController` issues `REF-` codes with `GenerateCodesAsync(coupon, 1, "REF-", usesPerCode: 1)` from a coupon picked in settings | The closest precedent for "issue a coupon to a person" |
| **Consent** (`Consent.Abstractions`/`.Core`, no module, no feature) | `IConsentService` (`GetAsync`, `RecordAsync`), `IConsentSource`, channels Phone/SMS/Email, contact kinds Contact/Customer. Opt-out only: no purpose, no pending state | A non-customer address has no marketing consent store; there is no way to say "marketing only" (ML-D6) |
| **Marketing** (commerce) | Customer segments, Liquid e-mail campaigns sent through Outreach, `marketing/unsubscribe` signed with `SignedTokenService` (400-day tokens), records `IConsentService` with `ContactKind = Customer` | Sends to customers only, no sign-up form, no lists. Its unsubscribe page is the first unsubscribe page |
| **Outreach** | `IOutreachEngine` with audience sources, channel senders, renderers, `ConsentOutreachRecipientPolicy`, on the leased work queue. `EmailOutreachChannelSender` sends through Orchard Core `IEmailService` | The engine for local list sends. Needs a mailing-list audience source |
| Orchard Core `MailMessage` (4.0.0-preview) | Has `From`, `To`, `Cc`, `Bcc`, `ReplyTo`, `Subject`, `HtmlBody`, `TextBody`, `Attachments`; **no custom headers** | Local sends through `IEmailService` can't carry the RFC 8058 `List-Unsubscribe`/`List-Unsubscribe-Post` headers that large mailbox providers require from bulk senders |
| Templated e-mails | `ITemplatedEmailSender`, `AddTemplatedEmailKind`, `ITemplatedEmailTemplateSource`, `TemplatedEmailTemplatesEditor` (Tickets is the reference) | Confirmation and welcome e-mails use it. It never retries; Tickets keeps a private outbox (`TicketEmailOutbox`) |
| Tickets web form | `TicketFormPart` widget, `TicketFormController` (`[AllowAnonymous]`, `ViewContent` on the widget, `RateLimitGroup`), trap field, `ITicketFormChallenge` with a ReCaptcha implementation, `TicketFormGuard` (an in-memory per-node counter) | The pattern for the sign-up widget. Its challenge and trap are private to Tickets |
| Messaging workspace | Agent inbox; SMS is the only channel on `main`. E-mail arrives with PR 817 (open) | Not used for newsletters. See "PR 817 review" |
| Provider patterns | Geo (named providers picked in settings, credentials through `ProtectedSettingExtensions`), PhoneNumbers.Verifications (keyed providers), Plaid (Orchard Core Secrets, provider inbox webhooks) | Mailchimp follows Geo for the seam and Plaid for credentials and webhooks |
| Provider webhook inbox | `IProviderWebhookInbox`, `IProviderWebhookInboxHandler`, `AfterResponseWork`; Plaid's endpoint is the template | Mailchimp webhooks go through it |

## Decisions (ML-D1 to ML-D6 chosen 2026-11-22)

| # | Proposal (recommended) | Alternatives | Why |
| --- | --- | --- | --- |
| ML-D1 | **Name the building block `MailingLists`** (`CrestApps.OrchardCore.MailingLists`, records `MailingList` and `MailingListMember`, provider module `CrestApps.OrchardCore.MailingLists.Mailchimp`). User-facing text says "mailing list" and "newsletter sign-up" | `Newsletters` | The module owns lists and members, not newsletters (content is written in Marketing or at the provider). "Subscription" is taken by paid plans, report subscriptions, webhook subscriptions and calendar subscriptions |
| ML-D2 | **The site always keeps a member ledger; the provider is the list of record for sending.** Every sign-up, verification, unsubscribe and coupon is recorded locally first. Each list names a provider (Local by default). An outside provider receives every change through a durable outbox and sends its changes back through webhooks and a daily reconciliation | Provider only (no local record when Mailchimp is on) | "One coupon per address", consent checks, privacy export and erasure, reports and a provider outage all need local state. A provider-only design would lose sign-ups while the provider is down and could not stop coupon farming |
| ML-D3 | **Double opt-in by e-mail is the default for a new list**; single opt-in and "the provider confirms" are options. A signed-in user whose account e-mail is confirmed skips the confirmation | Single opt-in by default | Double opt-in is expected in Germany and helps prove consent under GDPR and Canada's CASL, keeps typos and fake addresses off the list, and is the only real check that the person owns the address |
| ML-D4 | **Extend Checkout's coupons (reuse ladder rung 3):** a generated `CouponCode` gains `CustomerEmail`, `ExpiresUtc`, `Source` (`RecordRef`) and `IdempotencyKey`, and a new `ICouponIssuer` issues one code from a template coupon, idempotently | (A) Use `GenerateCodesAsync` as Referrals do: a bearer code, the template's dates. (B) Clone a full `Coupon` per subscriber | (A) can't bind the code to the subscriber or give it "valid 30 days from sign-up". (B) fills the Coupons list with thousands of coupons and copies editor rules. (C) is small, and Referrals, win-back and abandoned-cart offers reuse it |
| ML-D5 | **The coupon is issued once per list and normalized address, ever**, keyed on the list id plus a keyed hash of the normalized address (never the member id). It is always delivered by e-mail. The owner picks **when**: at sign-up, or after verification. It is shown on the web page only after verification | Issue per member record; show on the page at sign-up | Keying on the member id lets unsubscribe and subscribe again farm coupons. A coupon shown on the page at sign-up, unverified, can be farmed with made-up addresses; an e-mailed coupon at least proves the address receives mail |
| ML-D6 | **Add a purpose to Consent** (rung 3): `ConsentQuery.Purpose` and `ConsentChange.Purpose`, an open string with `ConsentPurposes.Marketing`; empty means every purpose (today's behavior). A "stop all marketing" is stored with the Marketing purpose; a confirmed sign-up records a Marketing opt-in | Use a new `ConsentContactKinds` value as a stand-in for "marketing" | An unsubscribe from a newsletter must stop marketing, not the support conversations that **Do not email** stops. A CRM **Do not email** (every purpose) must still stop newsletters. Contact kinds name who the address belongs to, not why we write |
| ML-D7 | **One unsubscribe and e-mail preferences page for the solution**, in a new small `CrestApps.OrchardCore.Consent` module (feature enabled by dependency only): signed links with a scope (a list, a campaign, "all marketing"), a themed confirmation page, and the RFC 8058 one-click `POST`. Marketing moves onto it in the same plan | A private unsubscribe page in MailingLists | Marketing has one page, PR 817 adds a second, this would be the third. The second copy is the trigger to extract (AGENTS.md reuse ladder, rung 4) |
| ML-D8 | **Provider credentials in Orchard Core Secrets** (a `TextSecret` picked by name, as Plaid and Orchard Core's SMTP settings do) | Data-protected site settings through `ProtectedSettingExtensions` (Geo, Telnyx) | The catalog says a credential of an outside service goes in Orchard Core Secrets. A Mailchimp API key has full account access and never expires |
| ML-D9 | **Local lists are sent through Marketing campaigns**: Marketing gains "mailing list" as a campaign audience through a `[RequireFeatures]` bridge startup in Marketing; MailingLists registers the Outreach audience source | A campaign editor inside MailingLists | Marketing campaigns are the solution's one campaign editor; a second would duplicate it. Sites without commerce send through their provider until campaign authoring moves out of commerce (open question 1) |
| ML-D10 | **Verification links are safe from link scanners:** `GET` shows a themed page with a **Confirm** button; only the button's `POST` changes state. No script submits it | Confirm on `GET`; a page that submits itself by script | Corporate mail scanners open every link in a message, and some run the page's scripts. Anything that confirms without a person's click subscribes people who never clicked and issues their coupon |

## Design

### Prerequisite building blocks (slice ML0)

Each item is the second or third copy of a concept, so it is extracted first and its current owner is moved onto it in the same slice.

| # | Building block | Where | Migrates |
| --- | --- | --- | --- |
| ML0a | **E-mail address helpers:** `EmailAddresses.Normalize(string)` (trim, lower-case), `EmailAddresses.IsValid(string)` (syntax and the 254-character limit), `EmailAddresses.AbuseKey(string)` (normalized, with plus tags and Gmail dots folded; used only to detect repeats, never to send) | `CrestApps.OrchardCore.Abstractions` (`CrestApps.OrchardCore.Emails.EmailAddresses`) | `ContactOptOutResolver`'s e-mail branch, coupon e-mail matching in `CouponScopeEvaluator`. `Inventory`'s stock alerts upper-case addresses in their index; leave that index, use the helper for new comparisons |
| ML0b | **Consent purpose** (ML-D6): `Purpose` on `ConsentQuery` and `ConsentChange`, `ConsentPurposes.Marketing`. Rule: an opt-out with a purpose blocks queries for that purpose; an opt-out with an empty purpose blocks every query; a query with an empty purpose asks only "is everything blocked?". `ContactPreferencesConsentSource` keeps answering and recording the empty purpose (a **Do not email** blocks marketing too). `CustomerMarketingConsentSource` answers and records the Marketing purpose, and **also finds the customer by normalized e-mail** when no `ContactId` is given (today it reads the id only, so an address-only "stop all marketing" would never reach `AcceptsMarketing`). **Address-level opt-outs move to Consent:** a store keyed by channel, address and purpose (`ConsentAddressOptOut`) with its source, registered by the Consent module feature (ML0c), so a person who is neither a customer nor a CRM contact can still say "stop all marketing" | `Consent.Abstractions`, `Consent.Core`, the Consent module, the three sources | Marketing's unsubscribe records the Marketing purpose. `CustomerAccountPages` and `CustomersAdminController` set `AcceptsMarketing` directly today; move them to `IConsentService.RecordAsync` so the change reaches every source. Omnichannel's `ContactAddressOptOut` (every purpose) moves onto the Consent store with a data migration, so the solution keeps one address opt-out store |
| ML0c | **Unsubscribe and preferences** (ML-D7): `IUnsubscribeLinks.CreateUrlAsync(UnsubscribeScope scope, string address)` and `TryRead(token)` over `SignedTokenService` (a long lifetime, as Marketing's 400 days) and `SiteUrlGenerator`; `UnsubscribeScope { Kind, Id }`; `IUnsubscribeScopeHandler` per kind (`AddUnsubscribeScope<T>()`: title, unsubscribe); the page `email-preferences/{token}` (themed shape, lists the scope and "Stop all marketing e-mail", which records the Marketing purpose) and the one-click `POST` | `Consent.Abstractions`, `Consent.Core`, new module `CrestApps.OrchardCore.Consent` | Marketing's `UnsubscribeController`: its `marketing/unsubscribe` route keeps working for e-mails already sent, and new campaigns use the shared links |
| ML0d | **Public form protection:** the trap field (`PublicFormTrap` shape and check) and `IPublicFormChallenge` (`IsConfigured`, `VerifyAsync`), with the ReCaptcha challenge registered under `[RequireFeatures(OrchardCoreFeatureIds.ReCaptcha)]` | Contract in `CrestApps.OrchardCore.Abstractions`, implementation in `CrestApps.OrchardCore.Core`, shape in Resources | Tickets' `ITicketFormChallenge`, `ReCaptchaTicketFormChallenge` and the trap field. Tickets' in-memory `TicketFormGuard` counter is per node; its limit moves to the Orchard Core Rate Limits group Tickets already declares |
| ML0e | **Durable templated e-mail:** an outbox on `ILeasedWorkQueue<T>` for `TemplatedEmail` (bounded retries with backoff, a "not delivered" mark, telemetry), so a mail provider blip never loses a confirmation | `CrestApps.OrchardCore.Core` (`Core.Emails`) | Tickets' `TicketEmailOutbox` (the audit marks it as the candidate to extract when a second module needs retries; it expected Commerce to be that module) |
| ML0f | **Coupon issuance** (ML-D4): `CouponCode` gains `CustomerEmail`, `ExpiresUtc`, `Source`, `IdempotencyKey`, `IssuedUtc`, and `CouponCodeIndex` gains an `IdempotencyKey` column (an additive migration) for the lookup; `CouponStore.ForGeneratedCode` lays the code's e-mail over the template's and ends the code at the earlier of the template's end and its own expiry; `ICouponIssuer.IssueAsync(CouponIssueRequest)` takes the template coupon id, prefix, recipient e-mail, `ValidFor`, source and a stable idempotency key, runs under a distributed lock on the key, and returns the existing code for a repeated key; the generated-codes admin list shows the e-mail, expiry and source; recipe and deployment steps carry the new fields | `Checkout.Abstractions`, `Checkout.Core`, Checkout module | Nothing to migrate. Referrals can move onto it later for idempotent `REF-` codes |

### Abstractions (`CrestApps.OrchardCore.MailingLists.Abstractions`)

Namespace `CrestApps.OrchardCore.MailingLists`. References only other `.Abstractions` projects (platform Abstractions, Consent, Records).

- **`MailingListsConstants`:** feature ids (`MailingLists`, `SignupWidget`, `WelcomeCoupon`, `Mailchimp` lives in its own module's constants), permission names, the settings group id, the YesSql collection `MailingLists`, the templated e-mail group `MailingLists` and kinds, the unsubscribe scope kind `MailingList`, the record kind `MailingListMember`, rate limit group names.
- **`MailingList : CatalogItem`:** `Name`, `DisplayText`, `Description`, `ProviderName` (default `Local`), `Confirmation` (`None`, `Email`, `Provider`), `SkipConfirmationForVerifiedUsers` (default true), `ConfirmationLinkLifetime` (default 7 days), `ConsentText` (shown on the form and kept on each member as evidence), `SendWelcomeEmail`, `ShowOnPreferencesPage`, `Properties` (provider settings and the welcome coupon live here, each edited by its own driver).
- **`MailingListMember : CatalogItem`:** `ListId`, `Contact` (`ContactInfo`: e-mail required, name optional), `NormalizedEmail`, `Status` (`Pending`, `Subscribed`, `Unsubscribed`, `Cleaned`), `EmailVerifiedUtc`, `VerificationNonce` (rotated on every resend, so older links stop working), `Source` (`Widget`, `Admin`, `Import`, `Api`, `Workflow`, `Checkout`, `Registration`, `Provider`) and `SourceId`, `UserId` (when signed in; names come from `IDisplayNameProvider`), `Consent` (`ConsentEvidence`: text, form, time, client address), `SubscribedUtc`, `UnsubscribedUtc`, `StatusChangedUtc` (orders provider changes), `ProviderMemberId`, `CouponCodeId`, `Properties`.
- **`MailingListMemberStatus`** moves through a `TransitionTable<MailingListMemberStatus>`: Pending → Subscribed, Unsubscribed; Subscribed → Unsubscribed, Cleaned; Unsubscribed → Pending (sign-up again with confirmation) or Subscribed (single opt-in lists); Cleaned → Pending only.
- **`IMailingListManager : ICatalogManager<MailingList>`**.
- **`IMailingListMemberService`:**
  - `SubscribeAsync(MailingListSubscribeRequest)` → `MailingListSubscribeResult` (`ConfirmationSent`, `Subscribed`, `Refused` with a reason). The caller always shows the same message whether or not the address was already on the list, so the form can't be used to find out who is subscribed.
  - `VerifyAsync(string token)` → result with the member and, when issued, the coupon to show.
  - `ResendConfirmationAsync(memberId)`.
  - `UnsubscribeAsync(listId or all, address, source, reason)`.
  - `ApplyProviderChangeAsync(MailingListProviderChange)` (list, address, new status, occurred time, provider member id, profile values). An older change than `StatusChangedUtc` is ignored, so a late webhook never subscribes someone again.
  - `EraseAsync(address)` for privacy requests.
- **`IMailingListMemberObserver`** (`AddMailingListMemberObserver<T>()`): `SubscribedAsync`, `VerifiedAsync`, `UnsubscribedAsync`, `CleanedAsync`, `ErasedAsync`. The welcome coupon, workflows and provider sync are observers.
- **`IMailingListProvider`** (`AddMailingListProvider<T>(name)`):
  - `Name`, `DisplayName`, `Capabilities` (`ConfirmsAddresses`: sends its own double opt-in; `DeliversWelcomeOffer`: can put the coupon code in a profile field its automations send; `ReportsChanges`: webhooks or a changes feed; `SendsCampaigns`).
  - `IsConfiguredAsync()`.
  - `PushAsync(MailingList, MailingListMember, MailingListPushKind kind)` where kind is `Upsert`, `Unsubscribe` or `Erase`. Must be idempotent: the outbox may call it twice.
  - `GetChangesAsync(MailingList, DateTime sinceUtc, string cursor)` for reconciliation (optional; `ReportsChanges`).
- **`IMailingListProviderResolver`:** the list's provider, or Local when the named one is missing or not configured (the list then shows a warning instead of failing sign-ups).

### Core (`CrestApps.OrchardCore.MailingLists.Core`)

- `MailingListStore` and `MailingListMemberStore` on `DocumentCatalog`, with `AuditedCatalogEntryHandlerBase` stamping.
- Indexes: `MailingListMemberIndex` (`ListId`, `NormalizedEmail`, `Status`, `UserId`, `SubscribedUtc`, `Source`), `ExclusiveToDocumentType()` and `SingleRecordPerDocument()`. A unique pair of list and address is enforced under a distributed lock on `list + normalized address` (`DistributedLockExtensions`).
- `DefaultMailingListMemberService`: the flows below, the transition table, observers, the Timeline entry for every change (record kind `MailingListMember`, `AddRecordSource<MailingListMemberRecordSource>()`), telemetry (`CrestAppsOrchardCoreTelemetry.CreateMeter("CrestApps.OrchardCore.MailingLists")`: sign-ups, verifications, unsubscribes, sync failures; no addresses on metrics).
- `LocalMailingListProvider`: `PushAsync` does nothing; members are already on the site. No capabilities.
- **Provider outbox:** `MailingListSyncItem : ILeasedWorkItem` (list, member, push kind, attempts), enqueued in the same unit of work as the member change (so a crash can't lose it), processed by one background task with `BackgroundWorkPacer` and `BackgroundTaskTelemetry`. Failures retry with backoff; after the last attempt the member shows "not synced" in the admin and on the list.
- **Reconciliation task:** daily per list whose provider `ReportsChanges`, reading changes since the last run through `ApplyProviderChangeAsync`; its cursor is kept on the list.
- **Retention:** `PendingMembersRetentionPolicy : DocumentRetentionPolicyBase` deletes members still `Pending` after 30 days (configurable).
- `MailingListConsentSource : IConsentSource` (e-mail channel) only reacts; list membership is not consent, so `HasOptedOutAsync` always answers "not opted out" (the stored opt-outs are Consent's, ML0b). `RecordAsync` with an opt-out for the Marketing or the empty purpose unsubscribes the address from every list, so a Marketing "stop all", a CRM **Do not email** or a PR 817 unsubscribe reach the provider too. An opt-in changes no list: joining a list needs the person's own sign-up. **Loop guard:** when the preferences page's "stop all" unsubscribes every list, the module records the Marketing opt-out with `Source = MailingListsConstants.ConsentSourceName`, and the source ignores changes with its own source name.
- `MailingListUnsubscribeScopeHandler : IUnsubscribeScopeHandler` for scope kind `MailingList`.
- `MailingListAudienceSource : IOutreachAudienceSource` (`MailingList`): subscribed members of one list, cursor paging on the index.
- `MailingListEmailTemplateSource : ITemplatedEmailTemplateSource` over `MailingListEmailSettings` (site settings), as Tickets does.
- `MailingListPersonalDataContributor : IPersonalDataContributor`: export members with their consent evidence; erase through `EraseAsync`, which deletes the members, queues `Erase` to the provider, and keeps only the coupon idempotency key (a keyed hash, not the address).

### The flows

**Sign-up** (`SubscribeAsync`), for each chosen list:

1. Validate the address (`EmailAddresses.IsValid`) and refuse more than three confirmation e-mails per address in 15 minutes (a counter in `IDistributedCache`, as Users' `EmailSignInCodes` does; Orchard Core Rate Limits handles the per-visitor limit).
2. Check consent in two steps:
   - **Hard blocks refuse the sign-up, even the confirmation e-mail:** `IConsentService.GetAsync` for the e-mail channel with an **empty** purpose, which only every-purpose opt-outs answer (a CRM **Do not email**, and the bounce and complaint suppression once it is a consent source).
   - **The person's own marketing opt-out does not block a new sign-up.** Someone who clicked "stop all marketing" last year can join again; on a double opt-in list the confirmation proves it is them. On a single opt-in list, an address with a Marketing opt-out is sent the confirmation e-mail anyway, so a stranger can't undo another person's opt-out.
3. Lock on list and address. Create or update the member with the consent evidence. An address already `Subscribed` stays as it is.
4. Choose the next state:
   - Confirmation `None`, or the signed-in user's confirmed account e-mail is the address: `Subscribed` (and `EmailVerifiedUtc` set in the second case).
   - Confirmation `Email`: `Pending`, a new nonce, and the **confirm** e-mail with a link `mailing-lists/verify/{token}` (`SignedTokenService`, purpose `CrestApps.MailingLists.Verify`, value member id and nonce, the list's lifetime).
   - Confirmation `Provider`: `Pending`, pushed with the provider's own double opt-in; the provider's webhook later reports the confirmation.
5. Run the observers (the coupon may be issued now, see below), then commit. E-mails go through the durable outbox (ML0e).

**Verification** (`VerifyAsync`), on the `POST` from the verify page's button: read the token; refuse a changed nonce or an expired token with a "send me a new link" form; set `EmailVerifiedUtc`; move `Pending` to `Subscribed`; record the opt-in (`ConsentChange { OptedOut = false, Purpose = Marketing, Source = MailingListsConstants.ConsentSourceName }`), which clears an earlier Marketing opt-out of the address (whether it also sets a customer's `AcceptsMarketing` is open question 5); send the **welcome** e-mail when the list asks for it; show the confirmation page (with the coupon when one was issued). A second click shows "already confirmed" and changes nothing.

**Unsubscribe:** every e-mail the module sends carries `IUnsubscribeLinks` for scope `MailingList:{id}`; the preferences page unsubscribes the list, or every list and all marketing. Admins can unsubscribe members in bulk. Each path ends in `UnsubscribeAsync`, which pushes `Unsubscribe` to the provider.

**Welcome coupon** (feature **Mailing List Welcome Coupon**, `[RequireFeatures(CheckoutConstants.Features.Area)]`, references `Checkout.Abstractions` only):

| List confirmation | Coupon "when" | What the person gets |
| --- | --- | --- |
| None | At sign-up | The welcome e-mail with the code |
| None | After verification | The welcome e-mail with a "claim your code" link; the link verifies the address, then shows and e-mails the code |
| E-mail | At sign-up | The confirm e-mail already holds the code ("confirm to stay on the list") |
| E-mail | After verification | The code on the confirmation page and in the welcome e-mail |
| Provider | After verification | Issued when the provider reports the confirmation; delivered by site e-mail, or written to the provider's profile field (Mailchimp merge field) when the list chooses "the provider sends it" |

- The driver on `MailingList` (in `Properties`): the template coupon (a picker of non-automatic coupons; `OnlyGeneratedCodes` recommended), the code prefix (default `WELCOME-`), "valid for" days, "when", "delivered by", and "only for people who have not bought before" (open question 4).
- The observer calls `ICouponIssuer.IssueAsync` with the idempotency key `mailing-list:{listId}:{HMAC-SHA256 of the abuse key}`. The HMAC key is a random 256-bit value made once per tenant and kept in the module's settings document; never a Data Protection key, because those rotate and a rotation would let the same address get a second coupon. The request also carries the subscriber's address as `CustomerEmail` and source `RecordRef("MailingListMember", memberId)`. A repeated key returns the code issued before; the e-mail is not sent twice.
- The e-mail's Liquid values: `{{ Coupon.Code }}`, `{{ Coupon.Url }}` (the `discount/{code}` link, which applies the code at checkout), `{{ Coupon.Description }}` (the template's discount in words) and `{{ Coupon.Expires }}`, converted with `ILocalClock` to the site's time zone (the subscriber has no account time zone).

### Module (`CrestApps.OrchardCore.MailingLists`)

Features:

| Feature | Id | Depends on |
| --- | --- | --- |
| Mailing Lists | `CrestApps.OrchardCore.MailingLists` | `CrestApps.OrchardCore.Consent` (ML0c), Resources, Orchard Core Email |
| Mailing List Sign-up Widget | `CrestApps.OrchardCore.MailingLists.SignupWidget` | Mailing Lists, Widgets, Orchard Core Rate Limits |
| Mailing List Welcome Coupon | `CrestApps.OrchardCore.MailingLists.WelcomeCoupon` | Mailing Lists, Checkout |

Contents:

- **Admin, lists:** `CatalogAdminController<MailingList>` with `<admin-list name="MailingLists">` (rows from a `DisplayDriver<MailingList>` with `SummaryAdmin` and `SummaryAdminGrid` locations: provider badge, confirmation badge, member counts, "not synced" warning). The editor is `IDisplayManager<MailingList>`: General card (name, description, consent text), Confirmation card, Provider card (the provider picker; the provider module adds its own card), Coupon card (from the coupon feature). Buttons in `action-bar-bottom`. Top-level menu **Mailing lists** with an icon.
- **Admin, members:** `<admin-list name="MailingListMembers">` per list: search by address in the action bar, status and source filters in `ToolbarActions` (bootstrap-select), bulk actions (unsubscribe, resend confirmation, erase), a member page with the consent evidence and the Timeline. "Add member" asks the admin to confirm they hold the person's consent and never issues a coupon.
- **Import and export:** import through ContentTransfer (`AddContentTransferImportTarget<MailingListMember>()`), status from a column, never sending e-mails or coupons. Export through Reports (a `MailingListMembers` `IndexedReportDataSet`, CSV and Excel from the Reports export formats); no CSV writer in this module.
- **Settings** (group `mailing-lists`, permission `ManageMailingListSettings`): the e-mail templates (`TemplatedEmailTemplatesEditor` shape) for the kinds `mailing-list-confirm`, `mailing-list-welcome` and `mailing-list-coupon-claim`; provider connection cards are added here by provider modules.
- **Public endpoints**, each `AllowAnonymous` with the check that fits it:
  - `POST mailing-lists/subscribe/{widgetId}` (`MailingListSignupController`): `ViewContent` on the widget (the Tickets web form convention), `[RateLimitGroup(MailingListsConstants.RateLimitGroups.Signup)]`, antiforgery, the trap field and the challenge (ML0d). Answers JSON for the widget's script and a redirect with a notice without script.
  - `GET`/`POST mailing-lists/verify/{token}` (ML-D10): the signed token is the authorization, as for Marketing's unsubscribe.
  - The unsubscribe page belongs to the Consent module (ML0c).
- **Sign-up widget:** `MailingListSignupPart` on a `MailingListSignup` widget type created by a migration. Settings: the lists to offer (one, or several with checkboxes), heading, text, button label, ask for a name, success message. The form shows the list's consent text and a link to the privacy page.
- **Workflows** (`[RequireFeatures("OrchardCore.Workflows")]` startup): events Member subscribed, Member verified, Member unsubscribed; tasks Subscribe to a mailing list, Unsubscribe from a mailing list. With these, any Orchard Core Forms form can sign people up.
- **Recipes and deployment:** `MailingLists` step (`NamedCatalogRecipeStep`, `NamedCatalogDeploymentSource`), `MailingListMembers` step (marked as holding personal data, off by default in deployment plans), the settings; schemas registered under `[RequireFeatures("CrestApps.OrchardCore.Recipes")]`.
- `ConfigurationSchema.json` for `MailingListsOptions` (sync batch size and attempts, pending retention days, per-address confirmation cap).
- Permissions: `ManageMailingLists`, `ViewMailingListMembers`, `ManageMailingListMembers`, `ManageMailingListSettings`.

### Mailchimp module (`CrestApps.OrchardCore.MailingLists.Mailchimp`)

Projects: `MailingLists.Mailchimp.Core` (API client, provider, webhook parser) and the module. Feature `CrestApps.OrchardCore.MailingLists.Mailchimp` depends on Mailing Lists, the provider inbox (`WebhooksConstants.Feature.ProviderInbox`) and Orchard Core Secrets.

- **Connection** (card on the `mailing-lists` settings page): the API key as a Secret name (ML-D8), "Test connection", and the server prefix. The API is `https://api.mailchimp.com/3.0/` with a bearer token per the current docs; whether calls must go to the key's data-center host is **to confirm**. A named `HttpClient` with `AddStandardResilienceHandler` (429 honored); at most a few concurrent calls per tenant, because Mailchimp allows 10 simultaneous connections per Mailchimp user across every client.
- **List card** (`DisplayDriver<MailingList>`, shown when the list's provider is Mailchimp): the audience (from `GET /lists`), tags to add (for example `website-signup`), interest groups (optional), the name merge fields, the coupon merge field (default `COUPON`, with a "create it" button: `POST /lists/{id}/merge-fields`), and "Mailchimp sends the confirmation e-mail".
- **Push** (`PushAsync`):
  - `Upsert`: `PUT /lists/{list}/members/{subscriber_hash}` (the hash is the MD5 of the lower-case address) with `status_if_new` and `status` mapped from the member, merge fields, and then `POST .../tags`. Naturally idempotent.
  - "Mailchimp sends the confirmation": `status_if_new = pending`. That `pending` makes Mailchimp send its own confirmation e-mail is **to confirm** before the option ships.
  - Re-subscribing an address Mailchimp holds as `unsubscribed`: whether the API accepts `subscribed` or needs `pending` is **to confirm**; the push uses `pending` when unsure.
  - `Unsubscribe`: `status = unsubscribed`. `Erase`: `POST .../actions/delete-permanent` (the docs say the address can never be imported again; the admin is told so before erasing).
- **Webhooks:** "Register webhook" on the list card calls `POST /lists/{id}/webhooks` for `subscribe`, `unsubscribe`, `profile`, `upemail` and `cleaned`, sources `user` and `admin` set explicitly and `api` off (so our own pushes don't echo back), with a URL `mailing-lists/mailchimp/webhooks/{listId}/{secret}`. Mailchimp's payload is form-encoded with `type`, `fired_at` and `data[...]` and carries no event id. The endpoint (Plaid's as the template): size cap, then the optional `X-Mailchimp-Signature` (`t=...,v1=...`, HMAC-SHA256 of `{t}.{body}`, rejected after 5 minutes) when a signing secret is saved (it is returned only once, at creation), otherwise the URL secret compared in constant time; `IProviderWebhookInbox.AcceptAsync` with `DeliveryId` = SHA-256 of the body; 200 on Accepted or Duplicate, 503 on Busy so Mailchimp retries; dispatch with `AfterResponseWork`. The handler (`WebhookHandlerReplaySafety.NaturallyIdempotent`) maps each event to `ApplyProviderChangeAsync`: `subscribe` (a confirmed double opt-in: Subscribed and verified, which may issue the coupon), `unsubscribe`, `cleaned` (the member on that list becomes Cleaned; no consent opt-out is recorded, because an empty-purpose opt-out would also set **Do not email** on CRM contacts and mark the address's other lists Unsubscribed instead of Cleaned; once bounces have a shared suppression source, see PR 817 finding 4, a `cleaned` event adds the address there), `profile`, `upemail` (address change).
- **Reconciliation:** `GET /lists/{id}/members?since_last_changed=...` paged, through the same change path, because Mailchimp drops or disables webhooks that fail for long.
- **Moving a list to Mailchimp:** an `IResumableStep` pushes existing members (unsubscribed ones as unsubscribed, never as subscribed). The batch endpoint is **to confirm**; until then, paced single upserts.
- **Welcome coupon by Mailchimp:** with "the provider sends it", the code is written to the coupon merge field and the site owner's Mailchimp welcome automation sends it.

### Sending to local lists (Marketing bridge)

- Marketing gets a `[RequireFeatures(MailingListsConstants.Features.MailingLists)]` startup that adds "mailing list" as a campaign audience next to segments (`Campaign.AudienceKind`, `AudienceId`; segments stay the default). The run uses `AudienceSource = "MailingList"`, the e-mail sender, `ConsentChannel = Email` and the Marketing purpose.
- Campaign unsubscribe links come from `IUnsubscribeLinks` with scope `MailingList:{id}`.
- Limit: `EmailOutreachChannelSender` goes through Orchard Core `IEmailService`, which can't send `List-Unsubscribe` headers. Small local sends are fine (the link is in the body); high-volume senders should use their provider. Open question 2 covers the fix.

## PR 817 review (findings for discussion, nothing changed)

[CrestApps/CrestApps.OrchardCore#817](https://github.com/CrestApps/CrestApps.OrchardCore/pull/817) ("Add an email channel to the messaging workspace, and AI email automation") is open, about 21,500 added lines. It makes e-mail a conversation channel of the workspace: addresses, entry-point routing, sending through Orchard Core's e-mail service or the address's own SMTP server (`IEmailTransport`), inbound from provider webhooks and IMAP through the provider inbox, bounces and complaints, a deliverability governor (limits, warm-up, health), a suppression list, one-click unsubscribe on broadcast and campaign e-mail, and AI email automation on a channel-neutral engine.

This plan does not depend on it. What it gives mailing lists once merged: an SMTP transport that can send `List-Unsubscribe` headers, bounce and complaint events, and the suppression list. Findings that touch this plan, backed by the PR's code:

1. **Its unsubscribe bypasses Consent.** `EmailOptOutService.OptOutAsync` sets `OmnichannelContactPart.DoNotEmail` on each matching contact directly and records nothing when no contact holds the address. Audit defect 8 was the same gap for SMS STOP from an unknown number, fixed by recording through `IConsentService`. Recording through `IConsentService.RecordAsync(ConsentChannels.Email, address, ...)` would store an address opt-out for strangers and let the mailing-list consent source unsubscribe the address too.
2. **A second unsubscribe page and token.** `EmailUnsubscribeLinks` creates its own `IDataProtector` instead of `SignedTokenService`, and `EmailUnsubscribeEndpoint` writes the page as an HTML string outside the site's theme. Marketing already has an unsubscribe page; ML0c proposes the shared one both could use.
3. **Unsubscribe scope.** A broadcast or campaign unsubscribe sets **Do not email**, which also stops one-to-one conversations. With a Consent purpose (ML0b) a campaign unsubscribe could stop marketing only.
4. **The suppression list is private.** `IEmailSuppressionList` lives in `Messaging.Email.Core` and is read only by its own screener. A read-only `IConsentSource` over it (as `DoNotCallListConsentSource` is for phone) would make Outreach, Marketing and mailing lists skip hard-bounced and complaining addresses.
5. **Secrets.** `EmailSecretProtector` is another data-protection wrapper beside `ProtectedSettingExtensions` (and Orchard Core Secrets, which Orchard Core's SMTP settings support).
6. **E-mail normalization.** `OmnichannelEmailAddress.Normalize` sits in `Omnichannel.Core`; Consent and coupons need the same rule. ML0a proposes the shared helper.
7. **Newsletter fit.** `MessagingOutreachChannelSender` creates a conversation per recipient, which suits broadcasts to contacts but not a newsletter to thousands of subscribers. The parity plan already says the e-mail channel is for conversations.

These are findings to discuss with the PR author; nothing has been posted on the PR or changed in it.

## Slices

| Slice | Contents | Size |
| --- | --- | --- |
| ML0a | E-mail address helpers and their first callers | S |
| ML0b | Consent purpose, the three sources (Customers by e-mail), the Consent address opt-out store with Omnichannel's `ContactAddressOptOut` migrated onto it, Customers pages through `IConsentService` | L |
| ML0c | Consent module: unsubscribe links, scopes, themed preferences page, one-click `POST`; Marketing moved onto it | M |
| ML0d | Public form trap and challenge out of Tickets; Tickets' limit on Rate Limits | S |
| ML0e | Durable templated e-mail outbox out of Tickets | M |
| ML0f | Coupon issuance: `CouponCode` fields, `ICouponIssuer`, admin list, recipe and deployment, tests for idempotency, expiry and the e-mail binding under concurrency | M |
| ML1 | MailingLists Abstractions, Core and module: records, stores, indexes, transitions, member service, Local provider, admin lists and editors, permissions, Timeline, recipes and deployment, personal data, retention | L |
| ML2 | Sign-up widget, verify endpoints and pages, confirm and welcome e-mails, settings page, consent source, unsubscribe scope handler | L |
| ML3 | Welcome coupon feature (driver, observer, claim link, e-mail values) | M |
| ML4 | Provider seam: picker, outbox, `ApplyProviderChangeAsync`, reconciliation task, "not synced" state | M |
| ML5 | Mailchimp module (connection, list card, push, webhooks, reconciliation, move-to-Mailchimp step, personal data) | L |
| ML6 | Marketing bridge and the Outreach audience source | M |
| ML7 | Workflows, the members data set, a growth report under **Reports** (sign-ups, verification rate, unsubscribes and coupon redemptions by local day) | M |
| ML8 | Docs (module page, feature reference, changelog), the building-blocks catalog rows (Mailing lists; Consent purpose and unsubscribe; coupon issuance; form protection; durable e-mail), `src/Targets`, a `crestapps-mailing-list-provider` skill for provider authors | S |

ML0b needs the Consent module from ML0c (its address store is registered by that feature); the other ML0 slices are independent of each other and can run in parallel. ML1 needs ML0a and ML0b; ML2 needs ML0c, ML0d and ML0e; ML3 needs ML0f; ML5 needs ML4.

## Testing

- Unit tests: the transition table, sign-up with each confirmation mode, the token (expired, changed nonce, second click), coupon idempotency (unsubscribe and sign up again, two requests at once), the consent source in both directions, `ApplyProviderChangeAsync` ordering, the Mailchimp hash and status mapping, webhook signature and URL-secret checks against a fake `HttpMessageHandler`.
- Feature activation tests: Mailing Lists alone (no commerce), with the coupon feature, with Mailchimp.
- Architecture tests stay green: module layering, index declarations, project references (`PrivateAssets="none"`), admin list and action-bar lint tests.
- End to end with Playwright on a running site: sign up through the widget, confirm from the e-mail (pickup directory), check the member, the issued `CouponCode` (e-mail, expiry, source) and the redemption at checkout in the database; unsubscribe from the e-mail link. Mailchimp against a test audience: push, confirmation, unsubscribe in Mailchimp reaching the site through the webhook, and reconciliation after the webhook is turned off.

## Out of scope

- A newsletter or campaign editor inside MailingLists (Marketing and the provider own sending).
- Third-party address checks (MX lookups, disposable-domain lists, paid verification APIs). Double opt-in is the verification; an `IEmailAddressVerifier` hook can come later, as Addresses defers `IAddressVerifier`.
- Providers other than Mailchimp (the seam and the skill make them separate modules later).
- SMS lists (Mailchimp's SMS events are ignored).

## Open questions

1. **Campaign authoring without commerce.** Marketing depends on Commerce, Customers and Orders, so a site without a store can't send to a local list from the site. Move campaigns into a non-commerce building block later, or accept "use your provider" for those sites?
2. **One-click unsubscribe headers for local sends.** Ask Orchard Core to add headers to `MailMessage`, send local bulk mail through PR 817's SMTP transport once it lands, or accept body links only?
3. **Sign-up at checkout and at registration.** A "Send me news and offers" checkbox at checkout (a `[RequireFeatures]` checkout handler) and on Orchard Core's registration form. Same release or later?
4. **New customers only.** Should the coupon skip addresses that already have orders? That needs an eligibility hook the Orders feature answers (`IMailingListOfferEligibility`).
5. **Signed-in customers.** When a signed-in customer subscribes, should the site also record a Marketing opt-in for the customer (sets `AcceptsMarketing`)?
6. **Mailchimp facts to confirm** before ML5 ships: the data-center host, `pending` sending Mailchimp's confirmation, re-subscribing an unsubscribed member through the API, and the batch endpoint.

## As built (2026-11-22)

Everything in "Slices" is built, with these differences:

- **ML0b, address opt-outs.** The Consent store (`ConsentAddressOptOut`, `AddressOptOutConsentSource`) keeps opt-outs **with a purpose** only. Omnichannel's every-purpose `ContactAddressOptOut` is not moved onto it yet (it sits in the dialer and SMS compliance path); the catalog row marks the gap.
- **CRM Do not email.** A contact's **Do not email** flag blocks a sign-up only when the question names the contact. An address-only question (a sign-up, a mailing-list send) does not find contacts by e-mail, because the contact index has no normalized e-mail column on `main` (PR 817 adds one). The contact editor also sets the flag without `IConsentService`, so lists don't hear of it. Follow-up.
- **Customers.** `CustomerMarketingConsentSource` answers address-only Marketing questions only when they name the Customer kind (otherwise every past guest, whose `AcceptsMarketing` is false, would be blocked). Recording by address reaches every customer with that address, so confirming a sign-up sets **Accepts marketing** on those customers (open question 5 answered yes by the build).
- **`ICouponIssuer` commits** the tenant session inside its lock, so concurrent requests with one key get one code. `MailingList` and `MailingListMember` carry a `[JsonIgnore]` YesSql `Id`, so a record saved again after that commit updates its document (found in the smoke test: without it the member was stored twice).
- **Recipes.** The shared `CatalogRecipeStep` base gives a new entry a new `ItemId`; the `MailingLists` step keeps the recipe's id instead, so widgets, members and campaigns that name a list still find it. Other catalog steps keep the base behavior.
- **One-click unsubscribe.** The RFC 8058 `POST` works on the preferences page, but local sends carry no `List-Unsubscribe` header (Orchard Core's `MailMessage` has no headers).
- **Mailchimp.** Not built: moving an existing list to Mailchimp (a resumable step) and interest groups. Still to confirm against a real account: the data-center host, `pending` sending Mailchimp's confirmation, re-subscribing an unsubscribed address, `fired_at`'s time zone, the webhook GET check, and whether a `subscribe` webhook fires for API-created members while `sources.api` is off.
- **Shared pieces extracted beyond the plan:** `TextSecretSelectList` (Core) for the Orchard Core Secrets picker of Plaid and Mailchimp; the personal-data contributor of issued coupon codes (`CouponCodeIndex.NormalizedEmail`).
- **Referrals** still issue bearer `REF-` codes with `GenerateCodesAsync`.
- **Tokens in the query string.** The confirmation and e-mail preferences links carry their signed token as `?token=…`. IIS and HTTP.sys refuse a URL path segment longer than 260 characters with `400`, and a signed token is longer. The old `/{token}` routes still answer for hosts that accept them.
- **Add member** lives at `mailing-lists/{listId}/add-member`; `members/add` was caught by the member details route `members/{id}`.
- **Provider settings rules.** Mailchimp's checks of a list (audience id, merge tag) run through `IMailingListSettingsValidator`, which `MailingListHandler` calls, so recipes check them too. The provider select keeps the list's provider when an unknown name is posted; a recipe may name a provider whose feature is not on yet.

Verified: 2,075 unit, architecture and public API tests; the view lint tests; a full web build with no warnings; an end-to-end run on a fresh local site (sign-up widget → confirmation e-mail → Confirm button → welcome code shown and e-mailed, bound to the address with its own expiry → unsubscribe and "stop all marketing" → signing up again returns the same code); and an upgrade of copies of existing local tenants (the new coupon-code columns added, and a feature's new dependency loaded with its tables); and a live run under IIS Express on a local copy of a store tenant (18 cases: double and single opt-in, an invalid address, the trap field, aliases and capitals, unsubscribe, stop all marketing and signing up again, a signed-in user, a resent link, RFC 8058 one-click, the rate limit, a newsletter campaign to a list, the code at checkout bound to its address, and admin Add member). Not verified: Mailchimp against a real account, and the full docs site build (`npm run build` fails on this machine for a local tool permission reason; `npm run check:manuals` passes).
