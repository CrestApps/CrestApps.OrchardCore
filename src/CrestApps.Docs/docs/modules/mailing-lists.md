---
sidebar_label: Mailing Lists
title: Mailing Lists
description: "Mailing lists people sign up to: double opt-in by e-mail, the evidence of each person's consent, a sign-up widget, a one-time welcome coupon, unsubscribe links on the shared e-mail preferences page, and a provider seam that keeps a list at an outside service such as Mailchimp."
user_manual:
  - user-manual/mailing-lists/index
  - user-manual/mailing-lists/members
  - user-manual/mailing-lists/signup-form
  - user-manual/mailing-lists/welcome-coupon
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.MailingLists` (Mailing Lists) |
| **Core** | `CrestApps.OrchardCore.MailingLists.Core` |
| **Abstractions** | `CrestApps.OrchardCore.MailingLists.Abstractions` (namespace `CrestApps.OrchardCore.MailingLists`) |
| **Category** | Communication |
| **Providers** | Local (built in), [Mailchimp](mailing-lists-mailchimp.md) |

The Mailing Lists module keeps mailing lists and their members. A visitor, signed in or not, enters an e-mail address and joins one or more lists. Each list says whether the address must be confirmed first (double opt-in) or is subscribed at once (single opt-in), and can give each new address a one-time welcome coupon. Every e-mail the module sends carries an unsubscribe link to the site's [e-mail preferences page](consent.md).

The module manages lists and members. It does not write or send newsletters. A list kept at an outside provider is sent from the provider's own tools. A local list can be sent through the [outreach engine](outreach.md) (see [Sending to a list](#sending-to-a-list)).

The admin screens are described in the User Manual: [Mailing Lists](../user-manual/mailing-lists/index.md), [Members](../user-manual/mailing-lists/members.md), [Sign-up Form](../user-manual/mailing-lists/signup-form.md) and [Welcome Coupon](../user-manual/mailing-lists/welcome-coupon.md).

## Features

| Feature | ID | Depends on |
| --- | --- | --- |
| Mailing Lists | `CrestApps.OrchardCore.MailingLists` | E-mail Preferences (`CrestApps.OrchardCore.Consent`, see [E-mail Preferences and Consent](consent.md)), CrestApps Resources (`CrestApps.OrchardCore.Resources`), Orchard Core Email (`OrchardCore.Email`) |
| Mailing List Sign-up Widget | `CrestApps.OrchardCore.MailingLists.SignupWidget` | Mailing Lists, Orchard Core Widgets (`OrchardCore.Widgets`), Orchard Core Rate Limits (`OrchardCore.RateLimits`) |
| Mailing List Welcome Coupon | `CrestApps.OrchardCore.MailingLists.WelcomeCoupon` | Mailing Lists, Checkout (`CrestApps.OrchardCore.Checkout`) |
| Mailing Lists Mailchimp | `CrestApps.OrchardCore.MailingLists.Mailchimp` | Mailing Lists, Provider Webhook Inbox, Orchard Core Secrets. See [Mailchimp](mailing-lists-mailchimp.md) |

Mailing Lists works without commerce. Only the welcome coupon needs Checkout.

With other features on, the module adds more:

| Feature on | Adds |
| --- | --- |
| Timeline | A timeline entry for every change of a member, shown as **History** on the member's page |
| Orchard Core Workflows | Three events and two tasks (see [Workflows](#workflows)) |
| Orchard Core Recipes and Deployment | The `MailingLists` and `MailingListMembers` recipe and deployment steps |
| CrestApps Recipes | The schemas of those steps and of the workflow activities |
| Orchard Core Data Pipelines Core | The **Mailing lists** data source for the Report Builder (see [Reports](#reports)) |
| Content Transfer | The **Mailing list members** import target (see [Importing members](#importing-members)) |
| Marketing | Campaigns to a mailing list (see [Marketing campaigns](#marketing-campaigns)) |

## The records

The site always keeps every list and every member, whatever the list's provider is. Lists, members and the provider outbox are stored in the `MailingLists` YesSql collection.

### MailingList

`MailingList` is a `CatalogItem` managed by `IMailingListManager` (an `ICatalogManager<MailingList>`), so its handlers run for the editor and for recipes.

| Property | Default | What it is |
| --- | --- | --- |
| `Name` | | The technical name, unique in the tenant: letters, digits, dashes and underscores, starting with a letter (128 characters at most). Workflows and recipes find a list by it. |
| `DisplayText` | | The name people see on the sign-up form, in the e-mails and on the e-mail preferences page. |
| `Description` | | What the list sends and how often. |
| `ProviderName` | `Local` | The provider that keeps the list. |
| `Confirmation` | `Email` | `None` (single opt-in), `Email` (double opt-in by the site's e-mail) or `Provider` (the provider sends its own confirmation e-mail). |
| `SkipConfirmationForVerifiedUsers` | `true` | A signed-in user who signs up with the confirmed e-mail of their account is subscribed and verified at once. |
| `ConfirmationLinkLifetime` | 7 days | How long a confirmation link works, from one hour to 30 days. The editor sets whole days from 1 to 30. |
| `ConsentText` | | The text shown on the sign-up form. Each member keeps the text they agreed to. |
| `SendWelcomeEmail` | `true` | Whether a welcome e-mail is sent when a person is subscribed. A welcome coupon is always e-mailed, even when this is off. |
| `IsEnabled` | `true` | A list that is off is not offered in sign-up widgets, takes no sign-ups from the widget, the workflow task or another module (a `SubscribeAsync` that is not silent is refused), and can't be picked as a Marketing campaign's audience. An admin can still add and import members, and members can still unsubscribe. |
| `Properties` | | The settings other features keep on the list: `MailingListWelcomeCoupon` (welcome coupon), `MailchimpListSettings` (Mailchimp) and `MailingListReconciliationState` (the last reconciliation). |

Deleting a list keeps its members until they are erased, so a list deleted by mistake can be made again from a recipe with the same id without losing the evidence of consent.

### MailingListMember

`MailingListMember` is one address on one list. `MailingListMemberIndex` indexes the list, the normalized address, the status, the user, the source and the times. A pair of list and address exists once: every change of one member runs under a distributed lock on the list and the normalized address, and commits before the lock is released.

| Property | What it is |
| --- | --- |
| `ListId`, `Contact` | The list, and the person's e-mail (required) and name (optional) as they typed them. |
| `NormalizedEmail` | The address trimmed and in lower case (`EmailAddresses.Normalize`). |
| `Status` | `Pending`, `Subscribed`, `Unsubscribed` or `Cleaned` (mail to the address bounces). |
| `EmailVerifiedUtc` | When the person proved they own the address: a confirmation or claim link, a confirmed account e-mail, or the provider's confirmation. |
| `VerificationNonce` | The value the current verification link carries. A new value is made for each link, so older links stop working. |
| `Source`, `SourceId` | How the member was added (`MailingListMemberSources`: `Widget`, `Admin`, `Import`, `Api`, `Workflow`, `Provider`, or a module's own string) and through what, such as the widget's content item id. |
| `UserId` | The user who signed up while signed in. |
| `Consent` | The evidence of consent (`MailingListConsentEvidence`): the text agreed to, where (the page or the form), when, and the client address. |
| `SubscribedUtc`, `UnsubscribedUtc`, `UnsubscribeReason` | When the member last joined and left, and why. |
| `StatusChangedUtc` | When the status last changed. A provider change older than this is ignored. |
| `ProviderMemberId`, `SyncState`, `SyncError` | The member's id at the provider, whether the last change reached it (`Synced`, `Pending`, `Failed`), and the provider's last error, or why the change waits (the provider's feature is off, or it is not set up). |
| `CouponCodeId` | The welcome coupon code issued to the member. |

The status moves through a transition table (`MailingListMemberTransitions`):

| From | To |
| --- | --- |
| Pending | Subscribed, Unsubscribed |
| Subscribed | Unsubscribed, Cleaned |
| Unsubscribed | Pending (a new sign-up with confirmation), Subscribed (a list without confirmation) |
| Cleaned | Pending only, so an address that bounced must confirm again |

## Signing up

Every path goes through `IMailingListMemberService`, so the observers, consent, the provider outbox and the timeline always run. `SubscribeAsync(MailingListSubscribeRequest)` does this for one list:

1. Refuses a missing list, a list that is off (except for a silent request), and an address that is not valid (`EmailAddresses.IsValid`: syntax and 254 characters at most).
2. Asks `IConsentService` about the address on the `Email` channel with an **empty** purpose. A source that reports an opt-out of every purpose refuses the sign-up, and no e-mail is sent. See [Consent](#consent).
3. Chooses the next state:
   - A request with a `Status` (an admin addition) gets that state, without any e-mail.
   - A signed-in user whose confirmed account e-mail is the address, on a list with `SkipConfirmationForVerifiedUsers`: `Subscribed` and verified now.
   - `None`: `Subscribed`. But when the address has a `Marketing` opt-out on file, it is `Pending` and gets a confirmation link, so a stranger can't undo another person's opt-out.
   - `Provider`, when the provider has the `ConfirmsAddresses` capability: `Pending`, and the provider sends its own confirmation e-mail. A provider without that capability falls back to `Email`.
   - `Email`: `Pending`, with a new nonce and the confirmation e-mail.
4. Locks the list and address. An address already `Subscribed` stays as it is (`AlreadySubscribed`). A `Cleaned` address must confirm again, whatever the list says.
5. Caps the confirmation and claim e-mails: at most `ConfirmationCap` per list and address within `ConfirmationCapMinutes` (3 in 15 minutes by default), counted in the distributed cache under a hash of the list and the address's abuse key (plus tags and Gmail dots folded), so aliases share the cap. A sign-up past the cap is refused.
6. Saves the member with the evidence of consent, queues the change in the [provider outbox](#providers-and-the-provider-outbox), runs the observers, and commits. The e-mail leaves only after the commit.

The result is `Subscribed`, `ConfirmationSent`, `AlreadySubscribed` or `Refused` with a reason for the log. A caller shows the same message in every case, so a form can't tell who is on a list.

A request with `Silent = true` (admin additions) sends no e-mail and gives no welcome coupon. The recipe step writes members directly and sends nothing either.

## Verification links

A confirmation or claim link opens `mailing-lists/verify?token=…` (route `MailingListVerify`; the token rides in the query string because IIS refuses a URL path segment longer than 260 characters, and the older `mailing-lists/verify/{token}` form still works). The token is made with `SignedTokenService` for the purpose `CrestApps.MailingLists.Verify` and holds the member id, the nonce and the end of the list's link lifetime. The link is built on the site's base URL, or on the current request when the site has none.

Opening the link changes nothing: the themed page (`MailingListVerify` shape) shows what will be confirmed, with a **Confirm** button. Only the button's `POST` verifies the address, and no script submits it, so a mail scanner that opens every link never subscribes anyone. The signed token is the authorization; the endpoints are anonymous.

`VerifyAsync` refuses a link whose nonce changed (a newer link was sent), whose time is over, or whose member left the list: the page then offers **Send me a new link** (`POST mailing-lists/verify/resend?token=…`, route `MailingListVerifyResend`), which says the same whether or not a link left. Otherwise it sets `EmailVerifiedUtc`, moves `Pending` to `Subscribed`, records a `Marketing` opt-in for the address (see [Consent](#consent)), sends the welcome e-mail when the list asks for it or a coupon was issued, and shows the confirmation page with the welcome code when one was issued. A second click shows "already confirmed" and changes nothing.

## The e-mails

The module sends three [templated e-mail](templated-emails.md) kinds in the `MailingLists` group, through the shared durable templated e-mail outbox (`ITemplatedEmailOutbox`), which retries a send the e-mail service did not take. Each e-mail's `Source` is `MailingListMember:{memberId}`, so an erasure deletes the stored e-mails of the member.

| Kind | Name in the editor | Sent when |
| --- | --- | --- |
| `mailing-list-confirm` | Confirm a sign-up | A sign-up needs confirmation. It carries the welcome code when the code is issued at sign-up. |
| `mailing-list-welcome` | Welcome | A person is subscribed and the list sends a welcome e-mail, or a welcome coupon was issued. |
| `mailing-list-coupon-claim` | Claim the welcome coupon | A person is subscribed on a list without confirmation whose coupon waits for verification. Its link verifies the address and gives the code. |

The site rewrites them under **Settings > Mailing Lists** (**E-mail templates**, the shared `TemplatedEmailTemplatesEditor`), kept in `MailingListEmailSettings` and served by `MailingListEmailTemplateSource`. A template reads:

| Value | What it is |
| --- | --- |
| `{{ Content }}`, `{{ Subject }}`, `{{ SiteName }}` | The e-mail as the site built it, its subject, and the site's name |
| `{{ List.Name }}`, `{{ List.Description }}` | The list (`MailingListEmailList`) |
| `{{ Member.Name }}`, `{{ Member.Email }}` | The person (`MailingListEmailMember`) |
| `{{ Links.Verify }}`, `{{ Links.Unsubscribe }}` | The confirmation or claim link (empty in the welcome e-mail) and the unsubscribe link (`MailingListEmailLinks`) |
| `{{ Coupon.Code }}`, `{{ Coupon.Url }}`, `{{ Coupon.Description }}`, `{{ Coupon.Expires }}` | The welcome coupon, when the e-mail carries one (`MailingListEmailCoupon`): the code, the `discount/{code}` link that applies it at checkout, the discount in words, and when it ends, written in the site's time zone |
| `{{ VerifyUrl }}`, `{{ UnsubscribeUrl }}`, `{{ CouponCode }}`, `{{ CouponUrl }}` | The same links and code as plain values |

The unsubscribe link needs the site's base URL. Without it, the e-mails go without the unsubscribe line. Keep `{{ Links.Unsubscribe }}` in every template.

## Welcome coupon

The **Mailing List Welcome Coupon** feature adds a **Welcome coupon** card to the list editor (`MailingListWelcomeCouponDisplayDriver`), kept in the list's properties as `MailingListWelcomeCoupon`:

| Property | Default | What it is |
| --- | --- | --- |
| `IsEnabled` | `false` | Whether the list gives a welcome coupon. |
| `CouponId` | | The template coupon. It must be a code coupon, not an automatic one. A coupon that accepts only its generated codes (`OnlyGeneratedCodes`) is recommended, so its own code can't be shared. |
| `Prefix` | `WELCOME-` | The text each code starts with: capital letters, digits and dashes, 12 characters at most. |
| `ValidForDays` | 30 | How long a code works after it is issued, from 1 to 3,650 days. Empty follows the coupon's end. A code never works past the coupon's end. |
| `Timing` | `AfterVerification` | `AtSignup` or `AfterVerification`. |
| `Delivery` | `Email` | `Email` (the site e-mails the code) or `Provider` (the code is written to the member's profile at the provider, whose own automation sends it). A provider without the `DeliversWelcomeOffer` capability falls back to e-mail. |

`MailingListWelcomeCouponObserver` issues the code through Checkout's `ICouponIssuer.IssueAsync`, with the subscriber's address as the code's `CustomerEmail`, the end, the source `RecordRef("MailingListMember", memberId)`, and the idempotency key `mailing-list:{listId}:{HMAC-SHA256 of the address's abuse key}`. The HMAC key is a random 256-bit value made once per tenant (by the feature's migration) and kept in the module's keys document, never a Data Protection key, because those rotate. The key never uses the member id, so unsubscribing and signing up again, or an alias of the same mailbox, gets the code issued before and no second code. A repeated key is shown again only to the member it was issued to, once verified; it is never e-mailed twice.

What a person gets:

| List confirmation | Issue the code | What the person gets |
| --- | --- | --- |
| None | At sign-up | The welcome e-mail with the code, even when the list sends no welcome e-mail. |
| None | After verification | The claim e-mail (`mailing-list-coupon-claim`). Its link opens the verify page; after **Confirm**, the code is on the page and in the welcome e-mail. |
| Email | At sign-up | The confirmation e-mail already holds the code. After **Confirm**, the code is also on the page and in the welcome e-mail. |
| Email | After verification | After **Confirm**, the code on the page and in the welcome e-mail. |
| Provider | Either | The code is issued when the provider reports the confirmation of a person who signed up on the site. The site e-mails it in the welcome e-mail, unless the list's provider delivers it: then the member is queued for the provider again, so the code reaches the member's profile there. |

More rules:

- A signed-in user whose confirmed account e-mail is the address is verified at sign-up, so the code is issued then and sent in the welcome e-mail. The sign-up widget does not show the code.
- A code is shown on a web page only once the address is verified.
- A single opt-in list where the address has a `Marketing` opt-out on file works like `Email`, because that address confirms first.
- Members added by an admin (`Silent`), by an import or by the `MailingListMembers` recipe step never get a code, also when the provider confirms them later. Members the workflow task signs up do, as with the widget.
- A member the provider reports that the site did not have (a sign-up through the provider's own form, or one added by the provider's admin) is silent, like an import: the site sends it no welcome e-mail and gives it no welcome coupon, also when the provider confirms it later. `MailingListProviderChanges.IsSilent` decides it from the member's `Source` (`Provider`, `Admin` or `Import`). When such a person signs up on the site while they are not subscribed (pending, unsubscribed or cleaned), the sign-up becomes the member's source, and a later confirmation issues the code. A sign-up of an address that is already subscribed changes nothing, so its source stays.

Checkout's generated-codes CSV (**Download them (CSV)** on the coupon's page) lists each issued code with its e-mail, end and source.

## Consent

Mailing lists use the [consent purposes](consent.md#purposes) of the Consent building block:

- **Opt-outs of every purpose refuse a sign-up.** The sign-up asks the consent service with an empty purpose, which only opt-outs of every purpose answer, such as an e-mail opt-out the CRM keeps for an address no contact holds. A CRM contact's **Do not email** flag is kept on the contact, not on the address, so it does not refuse a sign-up with that address.
- **The person's own marketing opt-out does not refuse a new sign-up.** Someone who stopped all marketing last year can join again. On a double opt-in list the confirmation proves it is them; on a single opt-in list such an address gets a confirmation link anyway.
- **Confirming records a marketing opt-in.** When an address is verified, the module records `ConsentChange { OptedOut = false, Purpose = Marketing, Source = "mailing-lists" }` for the address. This clears an earlier `Marketing` opt-out of the address. With Customers on, its consent source also sets **Accepts marketing** on every customer record that holds the address, so those customers get Marketing campaigns too.
- **An opt-out recorded through the consent service unsubscribes every list.** `MailingListConsentSource` is a consent source that only reacts: it never reports an opt-out (list membership is not consent), and an opt-in changes no list. When an `Email` opt-out with the `Marketing` purpose or with no purpose is recorded through `IConsentService.RecordAsync` (the e-mail preferences page's **Stop all marketing e-mail**, a Marketing campaign's old unsubscribe link, a customer who unticks **Send me news and offers**, an admin who unticks **Accepts marketing**), it unsubscribes the address from every list, which also reaches each list's provider. It ignores the changes the module records itself (source `mailing-lists`). Ticking **Do not email** in a CRM contact's editor sets the flag on the contact directly, without the consent service, so it doesn't unsubscribe lists.

### Unsubscribe links

Every e-mail the module sends carries an `IUnsubscribeLinks` link with the scope `MailingList:{listId}` (`MailingListsConstants.UnsubscribeScopeKind`). `MailingListUnsubscribeScopeHandler` gives the page the list's name and unsubscribes the list. The page also offers **Stop all marketing e-mail**. See [E-mail Preferences and Consent](consent.md).

## Providers and the provider outbox

A list's provider decides where the list lives. The site keeps every member and the evidence of consent; an outside provider gets every member change through a durable outbox and sends its own changes back.

- **Local** (`LocalMailingListProvider`, shown as **This site**) keeps the list on the site. A push does nothing, and it has no capabilities.
- An outside provider is registered with `AddMailingListProvider<T>()`. [Mailchimp](mailing-lists-mailchimp.md) is the first.

`IMailingListProviderResolver.Resolve(list)` returns the list's provider, or Local when the named provider is not registered (its feature is off). It serves the callers that only read what the provider can do: the sign-up's choice of confirmation and the welcome coupon's delivery. Sign-ups keep working then, and the lists page shows **Provider not available**. The provider outbox never uses the fallback.

**The outbox.** Each member change on a list whose provider is not Local is queued as a `MailingListSyncItem` (list, member, `Upsert`, `Unsubscribe` or `Erase`) on the leased work queue, in the same unit of work as the change, so a crash can't lose it. This holds also while the list's provider is not registered. The member is `SyncState.Pending`. Only a list kept on the site (`MailingListExtensions.IsLocal`: the `Local` provider, or no provider named) is marked synced without a push. After the request commits, the change is sent at once; the **Send mailing list changes to providers** background task (every minute, up to 10 batches of `SyncBatchSize` a run, through the background work pacer) sends what is still due. Each change is sent in a scope of its own.

- A change whose provider is not registered (its feature is off) or not set up (`IsConfiguredAsync` is false) waits. It uses up no attempt and never ends **Not synced**: the outbox looks at it again every 5 minutes (`RetryAsync` with `countAttempt: false`), and sends it once the provider is back. The member stays `Pending`, with the reason in `SyncError`, shown as the **Waiting for the provider** badge. An `Erase` waits too, and keeps the address on the queued change until the provider takes it.
- A failure the provider says can pass (a time-out, a rate limit) is tried again with a back-off from 1 minute, doubling, to at most 6 hours between tries.
- After `SyncMaxAttempts` attempts (8 by default), or a refusal, the member is marked **Not synced** (`SyncState.Failed`) with the provider's error, shown on the member and counted on the list.
- A change for a list that is now kept on the site, or for a member that is gone, completes without a push.

**Retry sync.** The members list's **Retry sync** bulk action (offered on a list kept at an outside provider) queues the state of each checked **Not synced** member again through `IMailingListMemberService.RetrySyncAsync`: an unsubscribed member as `Unsubscribe`, any other member as `Upsert`. A member that is not **Not synced** is left as it is. An erasure that failed can't be retried there, because the member is gone.

A list whose confirmation is **The provider sends its own confirmation e-mail** falls back to the site's confirmation e-mail while its provider's feature is off. When the provider is back, the queued upsert of a person who has not confirmed yet adds them as pending there, so that person can get the provider's confirmation e-mail too.

**Reconciliation.** Providers drop or turn off webhooks that fail for long. The **Reconcile mailing lists with providers** background task (once a day, default schedule `40 3 * * *`) reads, for each list whose provider has the `ReportsChanges` capability and is set up, the changes since the last run (`GetChangesAsync`, up to 200 pages a list), and applies them through `ApplyProviderChangeAsync`. The run's start time is kept on the list (`MailingListReconciliationState`) as the next run's cursor; a failed run keeps the old one, so the next run reads the same changes again. The first run reads the changes of the last day.

**Provider changes.** `ApplyProviderChangeAsync(MailingListProviderChange)` applies a webhook or a reconciled change: a change older than the member's `StatusChangedUtc` is ignored, so a late webhook never subscribes someone again; a move the transition table refuses is not made (a cleaned address must confirm on the site); a new address is created only as `Subscribed` or `Pending`; `Verified` verifies a pending member and records the marketing opt-in; `NewEmail` changes the address when the list has no member with the new one. A change that creates a member the site did not have, or changes a member that never signed up on the site, is silent (see [Welcome coupon](#welcome-coupon)). Nothing is pushed back, because the provider already holds the change, unless an observer asks for it (`MailingListMemberContext.RequestsProviderPush`). The welcome coupon observer asks when the list's provider delivers the code it just issued: the member service then queues an `Upsert` after the code is stored, in the same unit of work as the member change, and the push writes the code to the member's profile (Mailchimp's `COUPON` merge field, read from the member's `MailingListMemberCoupon`). When the change issued a welcome code that the site delivers, the site sends the welcome e-mail with it.

## Retention and privacy

- **Unconfirmed sign-ups.** `PendingMembersRetentionPolicy` (`MailingListPendingMembers`) deletes members still `Pending` `PendingRetentionDays` days (30 by default) after their status last changed. It runs in the daily **Data retention** task.
- **Export.** `MailingListPersonalDataContributor` exports each membership found by the person's e-mail addresses or user id, with its status, source, times and the evidence of consent.
- **Erasure.** `EraseAsync(address)` deletes every membership of the address on every list, deletes the member's stored e-mails, and queues `Erase` to each list's provider. Erasing an address at Mailchimp is permanent, and the address can never be imported into the audience again. The erase confirmations say so: on a member's page when one of the address's lists is kept at an outside provider, and for the members list's row action and **Erase (privacy request)** bulk action when the list, or another list of the tenant, is. An erasure for a provider whose feature is off waits in the outbox until the provider is back. The address's [opt-outs](consent.md#privacy) are kept, and the welcome codes issued to it lose the address (see [Personal data](personal-data.md)).

## Admin screens and permissions

| Screen | Route | Permission |
| --- | --- | --- |
| **Mailing lists > Lists** | `Admin/mailing-lists` | View the members of mailing lists |
| A list's editor (**Add mailing list**, **Edit**) | `Admin/mailing-lists/create`, `Admin/mailing-lists/edit/{id}` | Manage mailing lists |
| A list's members | `Admin/mailing-lists/{listId}/members` | View the members of mailing lists; bulk actions need Manage the members of mailing lists |
| A member's page | `Admin/mailing-lists/{listId}/members/{id}` | View the members of mailing lists; **Send a new link**, **Unsubscribe** and **Erase** need Manage the members of mailing lists |
| **Add member** | `Admin/mailing-lists/{listId}/add-member` | Manage the members of mailing lists |
| **Settings > Mailing Lists** | `Admin/Settings/mailing-lists` | Manage the mailing lists settings |

The lists and members render through Orchard Core's `AdminList` (rows from display drivers with `SummaryAdmin` and `SummaryAdminGrid` locations). The members list searches by e-mail address in the action bar, and filters in the toolbar by status (all, Subscribed, Pending, Unsubscribed, Cleaned, Not synced) and by how members joined (all sources, Sign-up widget, Workflow, Added by an admin, Import, The provider). Its bulk actions are **Unsubscribe**, **Send a new confirmation link**, **Retry sync** (on a list kept at an outside provider) and **Erase (privacy request)**. The editor is `IDisplayManager<MailingList>`, so other features add their own cards.

| Permission key | Name | Implied by |
| --- | --- | --- |
| `ManageMailingLists` | Manage mailing lists | |
| `ManageMailingListMembers` | Manage the members of mailing lists | Manage mailing lists |
| `ViewMailingListMembers` | View the members of mailing lists | Manage the members of mailing lists |
| `ManageMailingListSettings` | Manage the mailing lists settings | |

The Administrator role holds all four. The `mailing-lists` settings group registers `ManageMailingListSettings` with `AddSiteSettingsPermission`.

**Add member** subscribes the address at once (`Source = Admin`, `Silent`), asks the admin to confirm that they hold the person's consent, and keeps the admin's note (or a default text) as the evidence. It sends no e-mail and gives no welcome coupon.

## Sign-up widget

The **Mailing List Sign-up Widget** feature creates the `MailingListSignup` widget type with `MailingListSignupPart` (**Mailing List Sign-up**): `ListIds` (one list is joined at once; several are offered as checkboxes), `Heading`, `Text`, `ButtonLabel` (default **Sign up**), `AskForName` and `SuccessMessage`. The form shows each list's consent text, and fills in a signed-in visitor's e-mail.

The form posts to `POST mailing-lists/subscribe/{widgetId}` (route `MailingListSubscribe`, `MailingListSignupController`):

- It answers only for a published widget the visitor may view (`ViewContent` on the widget).
- The post is at most 16 KB; a name is cut at 200 characters.
- A filled trap field (the shared `PublicFormTrap` shape, field `website`) is answered as if it worked.
- With Orchard Core's **ReCaptcha** configured, every visitor must pass the challenge (`IPublicFormChallenge`, shown by the `PublicFormChallenge` shape). The widget's editor warns while no challenge is set up.
- Orchard Core's Rate Limits holds each visitor to 10 sign-ups in 10 minutes (group `MailingListSignup`, per signed-in user, otherwise per client address; behind a proxy, set up Reverse Proxy so the address is the visitor's).
- The answer is the same whether the address was new, already on the list or refused. The widget's script reads a JSON answer (`succeeded`, `message`); a visitor without script goes back to the page with the outcome in the query string.
- The evidence of consent is the page the form was on and the client address.

## Workflows

With Orchard Core Workflows on, the category **Mailing Lists** adds:

| Activity | Kind | What it does |
| --- | --- | --- |
| **Mailing List Member Subscribed** (`MailingListMemberSubscribedEvent`) | Event | A member became subscribed. |
| **Mailing List Member Verified** (`MailingListMemberVerifiedEvent`) | Event | A member's address was verified. |
| **Mailing List Member Unsubscribed** (`MailingListMemberUnsubscribedEvent`) | Event | A member was unsubscribed or cleaned. |
| **Subscribe to Mailing List** (`SubscribeToMailingListTask`) | Task | Signs an address up as the list says (a confirmation e-mail on a double opt-in list). **Mailing list** (id or technical name), **E-mail** and **Name** take Liquid. Outcomes `Done` and `Failed`. |
| **Unsubscribe from Mailing List** (`UnsubscribeFromMailingListTask`) | Task | Unsubscribes an address from one list, or every list when **Mailing list** is empty. Outcomes `Done` and `Failed`. |

The events start after the request commits, with the input `ListId`, `ListName`, `MemberId`, `Email`, `Name`, `Status` and `Source`. With the Subscribe task, any Orchard Core Forms form can sign people up, for example with `{{ Request.Form.email }}`.

## Recipes and deployment

| Step | Matched by | What it does |
| --- | --- | --- |
| `MailingLists` | `ItemId` | Creates or updates lists through the list manager, so the list rules run. |
| `MailingListMembers` | `ItemId`, then list and address | Creates or updates members with their evidence of consent. It sends no e-mail, gives no welcome coupon and pushes nothing to a provider. |

```json
{
  "steps": [
    {
      "name": "MailingLists",
      "MailingLists": [
        {
          "ItemId": "4ml00000000000000000000001",
          "Name": "weekly-news",
          "DisplayText": "Weekly news",
          "Description": "Our news and offers, every Friday.",
          "ProviderName": "Local",
          "Confirmation": "Email",
          "SkipConfirmationForVerifiedUsers": true,
          "ConfirmationLinkLifetime": "7.00:00:00",
          "ConsentText": "Yes, send me the weekly news. I can unsubscribe at any time.",
          "SendWelcomeEmail": true,
          "IsEnabled": true,
          "Properties": {
            "MailingListWelcomeCoupon": {
              "IsEnabled": true,
              "CouponId": "4cp00000000000000000000001",
              "Prefix": "WELCOME-",
              "ValidForDays": 30
            }
          }
        }
      ]
    },
    {
      "name": "MailingListMembers",
      "Members": [
        {
          "ItemId": "4mm00000000000000000000001",
          "ListId": "4ml00000000000000000000001",
          "Contact": { "Email": "john.smith@contoso.com", "Name": "John Smith" },
          "Status": "Subscribed",
          "Source": "Import",
          "SubscribedUtc": "2026-11-02T09:30:00Z",
          "EmailVerifiedUtc": "2026-11-02T09:30:00Z",
          "Consent": {
            "Text": "Yes, send me the weekly news. I can unsubscribe at any time.",
            "Form": "/newsletter",
            "AgreedUtc": "2026-11-02T09:28:00Z"
          }
        }
      ]
    }
  ]
}
```

The deployment steps are **Mailing lists** (every list with its settings, without its members) and **Mailing list members** (category Mailing Lists). Members are personal data: the members step is part of a plan only when someone adds it. The e-mail templates are site settings (`MailingListEmailSettings`) and travel with the `Settings` step.

## Configuration

The module reads `OrchardCore:CrestApps:MailingLists` (`MailingListsOptions`) from the host's configuration, or `CrestApps:MailingLists` from a tenant's own `appsettings.json`.

| Setting | Default | What it does |
| --- | --- | --- |
| `SyncBatchSize` | 25 | How many member changes the provider outbox sends in one batch. |
| `SyncMaxAttempts` | 8 | How many times the provider outbox tries a change before it marks the member not synced. |
| `PendingRetentionDays` | 30 | How many days an unconfirmed sign-up is kept before it is deleted (1 at least). |
| `ConfirmationCap` | 3 | How many confirmation and claim e-mails one address may be sent for one list within `ConfirmationCapMinutes`. 0 turns the cap off. |
| `ConfirmationCapMinutes` | 15 | The window of `ConfirmationCap`, in minutes. |

```json
{
  "OrchardCore": {
    "CrestApps": {
      "MailingLists": {
        "SyncBatchSize": 25,
        "SyncMaxAttempts": 8,
        "PendingRetentionDays": 30,
        "ConfirmationCap": 3,
        "ConfirmationCapMinutes": 15
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__MailingLists__PendingRetentionDays=14
OrchardCore__Default__CrestApps__MailingLists__ConfirmationCap=5
```

A tenant's own `App_Data/Sites/{tenant}/appsettings.json` has no `OrchardCore` wrapper:

```json
{
  "CrestApps": {
    "MailingLists": {
      "PendingRetentionDays": 14
    }
  }
}
```

See also [Configuration](../configuration.md#mailing-lists).

## Telemetry

The meter `CrestApps.OrchardCore.MailingLists` (`MailingListsTelemetry`) counts, with no address, member id or list id on a metric:

| Instrument | Tags |
| --- | --- |
| `crestapps.mailing_lists.sign_ups` | `outcome` (`Subscribed`, `ConfirmationSent`, `AlreadySubscribed`, `Refused`), `source` |
| `crestapps.mailing_lists.verifications` | |
| `crestapps.mailing_lists.unsubscribes` | `source` |
| `crestapps.mailing_lists.sync_failures` | `provider`, `retry` |

## Sending to a list

The module registers the Outreach audience source `MailingList` (`MailingListAudienceSource`): the subscribed members of one list, a page at a time. A run names the list in its `MailingListId` property, or with `OwnerType = "MailingList"` and the list's id as `OwnerId`. Give such a run `ConsentChannel = Email` and `ConsentPurpose = Marketing`, so a person who stopped marketing is skipped. See [Outreach](outreach.md).

### Marketing campaigns

With [Marketing](marketing.md) on (it needs commerce), a campaign can go to a mailing list instead of a segment. Marketing's `MailingListsStartup` (`[RequireFeatures("CrestApps.OrchardCore.MailingLists")]`) replaces its `ICampaignMailingLists` default, and the campaign editor's **Audience** card then shows **Send to** (**The customers of a segment** or **The members of a mailing list**) and a **Mailing list** select with the enabled lists. The campaign keeps `Campaign.AudienceKind` (`Segment`, also when empty for campaigns made before, or `MailingList`) and `Campaign.MailingListId`.

- The campaign's outreach run uses the `MailingList` audience source with the list in the run's `MailingListId` property, `ConsentChannel = Email` and `ConsentPurpose = Marketing`. It goes to the list's subscribed members as the list is when each batch goes out, except addresses that stopped marketing e-mail.
- Each e-mail's unsubscribe link has the scope `MailingList:{listId}`, so the e-mail preferences page unsubscribes the person from that list, and also offers **Stop all marketing e-mail**.
- A member is not a customer. The Liquid has `{{ Recipient.Name }}` and `{{ Recipient.Email }}` for both audiences; `{{ Customer.FirstName }}` and `{{ Customer.Name }}` carry the name the member gave, which may be empty, and their orders are not known.
- Only an enabled list can be chosen. The `MarketingCampaigns` recipe step carries `AudienceKind` and `MailingListId`, and imports a campaign to a list only while Mailing Lists is on.

### Reports

With Orchard Core's Data Pipelines Core (`OrchardCore.DataPipelines.Core`) on, the module adds the data source **Mailing lists** (`MailingLists`, `MailingListsReportDataSource`) with the data set **Mailing list members** (`MailingListMembers`, `MailingListMembersReportDataSet`). The Report Builder, its CSV and Excel exports, and the **Read a data source** step of data pipelines read it. Reading it needs **View the members of mailing lists** (`ViewMailingListMembers`).

| Group | Fields |
| --- | --- |
| Member | `ItemId`, `ListId`, `List` (the list's name), `Email`, `Name`, `Status`, `Source`, `IsVerified`, `SyncState`, `HasCoupon` (got a welcome coupon), `UserId` |
| Dates | `SignedUpUtc` (the data set's main date), `SubscribedUtc`, `UnsubscribedUtc`, `EmailVerifiedUtc`, `StatusChangedUtc` |

The client address of the evidence of consent, the verification nonce and the provider's errors are never exposed.

### Importing members

With [Content Transfer](content-transfer.md) on, **Mailing list members** is an import target (`MailingListMembersImportTarget`, named `MailingLists.MailingListMember`) at `/Admin/import/contents/MailingLists.MailingListMember`, offered in the **Import** menu to people with **Manage the members of mailing lists**.

| Column | Also read from | What it holds |
| --- | --- | --- |
| `List` | `ListId`, `ListName`, `MailingList` | The list the address joins: its technical name or its id. |
| `Email` | `EmailAddress`, `E-mail` | The address. An address already on the list stays as it is. |
| `Name` | `FullName` | The person's name, optional. |
| `Status` | | `Subscribed` (the default when empty) or `Pending`. |

Each row goes through `SubscribeAsync` as a silent import (`Source = Import`), so it sends no e-mail and gives no welcome coupon. An import never subscribes an address that is `Unsubscribed` or `Cleaned` on the list, or that asked to stop all marketing e-mail (an `Email` opt-out with the `Marketing` purpose): its row is refused with the reason. A pending member gets a confirmation e-mail only when an admin sends one from the members page, and is deleted when it is not confirmed in time. To export members, use the Reports data set above.

## Extension points

| Contract | Register with | What it is for |
| --- | --- | --- |
| `IMailingListProvider` | `services.AddMailingListProvider<T>()` | A place a list lives (Mailchimp, Klaviyo, Brevo...). `Name`, `DisplayName`, `Capabilities`, `IsConfiguredAsync`, an idempotent `PushAsync(list, member, kind)` and `GetChangesAsync(list, sinceUtc, cursor)`. See the `crestapps-mailing-list-provider` skill in `.agents/skills`. |
| `IMailingListMemberObserver` (or `MailingListMemberObserverBase`) | `services.AddMailingListMemberObserver<T>()` | Reacts to `SignedUpAsync`, `SubscribedAsync`, `VerifiedAsync`, `UnsubscribedAsync` and `ErasedAsync`. Observers run inside the member service's unit of work, after the member is saved and before the commit, and must not commit. The context gives the member, the list, the previous status, the source and `Silent`; an observer can set `IssuedCoupon` (shown and e-mailed), `RequestsVerification` (sends a claim link instead of the plain welcome) and `RequestsProviderPush` (queues an upsert for the provider after a change the provider reported, such as a welcome code the provider sends). The welcome coupon, workflows and timeline are observers. |
| `IMailingListSettingsValidator` | `services.TryAddEnumerable(ServiceDescriptor.Scoped<IMailingListSettingsValidator, T>())` | Checks the settings your module keeps on a list (in its `Properties`), such as a provider's audience. The list's own handler calls every validator, so the editor and recipes check the same rules. Mailchimp checks its audience id and merge tag this way. |
| `IMailingListMemberService` | (provided) | Signs people up from your own module (a checkout box, a registration form), unsubscribes, erases, applies provider changes, and queues a not synced member again (`RetrySyncAsync`). |
| `IMailingListProviderResolver` | (provided) | Every provider, a provider by name, and a list's provider with the Local fallback (`Resolve`). `MailingList.IsLocal()` and `GetProviderName()` (`MailingListExtensions`) tell whether a list is kept on the site. |
| `IMailingListManager` | (provided) | Lists, and `FindByNameAsync` by technical name. |

`MailingListProviderCapabilities`:

| Capability | Used for |
| --- | --- |
| `ConfirmsAddresses` | The list's **The provider sends its own confirmation e-mail** option. Without it, such a list falls back to double opt-in by the site. |
| `DeliversWelcomeOffer` | **Delivered by** the provider. Without it, the site e-mails the code. |
| `ReportsChanges` | The daily reconciliation reads the provider's changes. |
| `SendsCampaigns` | Says the provider sends campaigns from its own tools. The module does not read it. |

## Related pages

- [E-mail Preferences and Consent](consent.md): unsubscribe links, purposes, address opt-outs.
- [Mailchimp](mailing-lists-mailchimp.md): the Mailchimp provider.
- [Checkout](checkout.md#discounts-and-coupons): coupons and generated codes.
- [Templated e-mails](templated-emails.md) and [Personal data](personal-data.md).
