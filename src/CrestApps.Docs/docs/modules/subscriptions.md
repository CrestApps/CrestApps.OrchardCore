---
sidebar_label: Subscriptions
sidebar_position: 14
title: Subscriptions
description: Sell recurring subscriptions and onboard tenants in Orchard Core, with a multi-step checkout, pluggable payment methods, and a hardened Stripe integration.
---

| | |
| --- | --- |
| **Feature Name** | Subscriptions |
| **Feature ID** | `CrestApps.OrchardCore.Subscriptions` |
| **Category** | Subscriptions |
| **Dependencies** | `OrchardCore.Contents`, `OrchardCore.ContentTypes`, `OrchardCore.Title`, `CrestApps.OrchardCore.Users`, `CrestApps.OrchardCore.Products`, `CrestApps.OrchardCore.Checkout`, `CrestApps.OrchardCore.Receipts` |

The **Subscriptions** module lets you sell recurring plans built on ordinary Orchard Core content items. It adds a **`SubscriptionPart`** that turns a content type into a billable plan, a durable **subscription agreement** that outlives the purchase, and the lifecycle that keeps that agreement honest as payments succeed, fail, and stop.

Subscriptions owns no checkout of its own. Buying a plan runs through the [Checkout](checkout) framework exactly like any other purchase, so there is one path that takes money and one ledger that records it. That is what makes a subscription's revenue reconcilable against the payments the site actually collected.

## Features

The module ships several composable features:

| Feature | Feature ID | Description |
| --- | --- | --- |
| Subscriptions | `CrestApps.OrchardCore.Subscriptions` | Subscription plans, the durable agreement, its lifecycle, entitlements, and the admin and customer screens. Depends on the [Checkout](checkout) framework. |
| Subscriptions - Sites | `CrestApps.OrchardCore.Subscriptions.Tenants` | Sells whole Orchard Core sites, provisioning each from a durable job (default tenant only). |

Payment options are contributed by other modules rather than by dedicated Subscriptions sub-features:

- **Stripe** — enable the **Stripe** module (`CrestApps.OrchardCore.Stripe`). When it is enabled alongside Subscriptions, the Stripe payment method is wired into the subscription checkout automatically; there is no separate *Subscriptions - Stripe* feature to switch on.
- **Pay Later** — enable the standalone **[Pay Later](pay-later)** module (`CrestApps.OrchardCore.PayLater`). It contributes an offline pay-later option to the checkout framework for both subscriptions and one-time purchases.

## The Subscription part

Attach **`SubscriptionPart`** to any content type (it depends on [Products](products), so the type typically also carries `ProductPart`). The part defines the recurring billing terms:

| Property | Description |
| --- | --- |
| `InitialAmount` | An optional one-time initial charge applied at signup (for example a setup fee). |
| `InitialAmountDescription` | The line-item label shown for that initial amount. |
| `BillingDuration` | The number of `DurationType` units in each billing cycle (for example `1`). |
| `DurationType` | The unit for the cycle — `Year`, `Month`, `Week`, or `Day` (for example `1` + `Month` = monthly). |
| `BillingCycleLimit` | Optional cap on how many cycles are billed before payments stop. |
| `SubscriptionDayDelay` | Optional number of days to delay the start of the subscription. |
| `TrialDays` | Optional free trial, in days, before the first cycle is billed. Not the same as a delayed start — see [Free trials](#free-trials). |
| `Sort` | Sort order used when listing subscription options. |

Each content item of that type becomes a purchasable plan, editable and securable like any other Orchard Core content.

## Buying a plan

A plan is bought through the shared [Checkout](checkout), started from the **Sign up** link on any published plan (`Subscription/Signup/{contentItemId}`). The checkout resolves the plan, records what is being bought, and hands the customer to the ordinary checkout experience.

The plan itself contributes the charges: the recurring price and, when configured, a one-time setup fee. Those sit on a step that is never drawn — the customer already chose the plan, so there is nothing to ask them — but the charges are still billed. Other handlers contribute the steps that do ask for something:

| Step | Contributed when |
| --- | --- |
| Content | The plan's `SubscriptionPartSettings.ContentTypes` names content the subscriber must fill in. |
| Registration | The visitor is not signed in. It is hidden again the moment they are, including when they sign in on another tab midway through. |
| Site details | The **Subscriptions - Sites** feature is enabled and the plan provisions a site. |
| Payment | Always, and always last. |

The price comes from the plan through `IProductSnapshotResolver`, so a future pricing engine changes what is charged without changing the checkout. The invoice is built in the plan's own currency rather than a site-wide default, because an invoice built in one currency from prices set in another charges a number that belongs to a different currency. Amounts always come from the server-side invoice, never from anything the browser submitted.

### Buying without an account

When the visitor is not signed in, the account step collects their details and the account is created **before** any money moves, then signed in once the purchase completes. If the checkout fails, that account is deleted again — after verifying the password matches, so only the account this checkout created is ever removed. Creating it after payment would leave a paying customer with no account whenever account creation failed; leaving it behind on a failure would let a visitor accumulate accounts by abandoning checkouts.

Turn on **Allow Guest Signup** in the subscription settings to let somebody buy without creating an account at all.

## Receipts

Each confirmed payment offers a printable receipt through the reusable [Receipts](receipts) module, showing the configured issuer branding, billed-to details, plan, tax breakdown, total, and transaction reference. A receipt is only ever served to the customer who owns the payment.

## Admin management

Administrators get a **Subscriptions** admin area with two screens:

- **Agreements** — every durable subscription agreement, filterable by status. From an agreement's detail page an administrator can cancel it (immediately or at the close of the paid period), pause billing, and resume it. Every one of those actions goes through the lifecycle service rather than editing the record, so an administrator cannot race a gateway webhook or the nightly sweep, and each action is written to the agreement's own history.
- **My Plans** — the matching customer screen, where a subscriber sees what they subscribe to and cancels it themselves. Cancelling there defaults to ending at the close of the period they already paid for, because they bought that time.

Payments, refunds, and outstanding balances live in the [Transactions](transactions) module rather than here, because they are not specific to subscriptions.

A **Subscription Summary** widget (`SubscriptionSummaryPart`) surfaces headline numbers on a page. Every figure it shows is read from the durable agreement and the payment ledger, never from checkout sessions, so an abandoned checkout is never counted as a subscriber or as revenue.

## The subscription agreement

A completed checkout used to be the only record that somebody had subscribed. That made every ordinary question expensive: who is subscribed right now, whose payment failed last night, what renews next month, and what should happen when a customer cancels.

Every recurring obligation a checkout settles now also creates a durable **subscription agreement**. A checkout session records how something was bought once; the agreement records what the customer is entitled to from then on, and it outlives the checkout.

An agreement carries who owns it, what it bills and on what cycle, the gateway's own identifier for it, the period the customer has paid through, when the next cycle is due, the lines that make it up, and a full event history. It is created only from payment attempts the checkout actually confirmed, so an unpaid checkout never grants an active subscription, and it is looked up by obligation before being created, so a checkout that completes twice never produces two agreements.

The status is one of `Active`, `Trialing`, `PastDue`, `Canceled`, `Expired`, `Paused`, or `Incomplete`. Whether a subscriber currently has access is a separate question from the status, and the agreement answers it: a cancelled agreement is still current until the paid period runs out, and a past-due one is current through its grace window.

### Transitions

Subscription state is edited from four directions at once: a gateway webhook, a nightly sweep, an administrator, and the customer. Every transition therefore goes through one service, takes a lock on the single agreement it changes, re-reads it inside that lock, and is idempotent.

| Transition | What it does |
| --- | --- |
| Record renewal | Advances to the next period and schedules the next bill. Reporting the same cycle twice advances it once, so a webhook and the sweep cannot skip a payment between them. |
| Mark past due | Starts the dunning window. A second failure inside the same window does not restart the grace clock. |
| Cancel | Stops future billing. At period end it keeps the paid-through date; immediately it ends access now. |
| Resume | Returns a paused or past-due agreement to active. |
| Pause | Suspends billing without ending the agreement. |
| Expire | Ends the agreement when the grace window elapses or the agreed cycles are all billed. |
| Sync from provider | Adopts the gateway's period, pending cancellation, and status. It will not revive an agreement cancelled locally, because that would start billing a customer who already left. |

The dunning grace period is a site setting (**Dunning grace days**, 7 by default). A failed renewal is usually an expired card rather than a customer who left, so cutting access off the same night loses subscribers who would have paid, while never cutting it off gives the plan away.

A background sweep runs every thirty minutes. It expires agreements whose grace window has elapsed and closes those that have billed every cycle they were sold for. It never charges anyone: a gateway-backed agreement is billed by the gateway, and an offline one is invoiced by its own provider's renewal path.

## Renewals and cancellation

Who moves an agreement forward depends on who bills it:

- **A gateway** (Stripe) owns the schedule. Each cycle it bills arrives as a payment notification for a subscription cycle, and that notification records the renewal against the period the gateway named. The notification is delivered at least once and the renewal is idempotent, so a duplicate never advances twice.
- **An offline provider** (Pay Later) has no gateway to report anything. The lifecycle sweep advances any agreement whose provider has no gateway once its next billing date passes, and the Pay Later sweep records the debt for the new cycle. The two are independent: the debt exists whether or not the customer has paid the previous one, which is what lets an unpaid balance be chased.


Cancelling, suspending, and resuming all reach the gateway. That is worth stating plainly, because the
opposite failure is silent and expensive: an agreement marked cancelled here while the gateway keeps
charging the customer's card every cycle. Cancelling at period end asks the gateway to stop after the period
the customer has already paid for; cancelling immediately stops it now. Suspending an agreement suspends
collection at the gateway too, and resuming puts it back on the schedule.

The traffic only ever goes one way per change. A cancellation the gateway itself reported carries the
gateway as its source and is not sent back, so a subscription the gateway ended is not asked to end again.

:::note
A gateway that suspends collection still reports the agreement as **active** — pausing is not a status
there. A local suspension is therefore treated as a decision the gateway cannot express, and a later
notification saying "active" does not undo it. The same protection covers a local cancellation.
:::

A subscription does not end when checkout completes; it renews, can fall behind, and can be cancelled. The module tracks that afterlife from the payment gateway's notifications, so the site does not have to infer a subscription's fate from payments quietly stopping.

| Gateway notification | Effect on the recorded subscription |
| --- | --- |
| Cycle payment succeeded | The payment is recorded with **its own collection date**, and the subscription's expiration advances by exactly one billing cycle. |
| Cycle payment failed | The subscription moves to **Past due** and the date it first fell behind is recorded, so a dunning window can be measured from it. |
| Subscription updated | The status, paid-through date, and pending cancellation are adopted from the gateway — except where they would undo a local suspension or cancellation. |
| Subscription deleted | The subscription is marked **Canceled** with the cancellation date. |

Each recorded subscription carries a `SubscriptionLifecycleStatus` (`Active`, `Trialing`, `PastDue`, `Canceled`, `Expired`, `Paused`). Some deliberate rules:

- The expiration advances **from the previous expiration**, not from the moment the webhook was processed, so a renewal handled late never shortens the period the customer paid for.
- A cancellation scheduled for the end of the period keeps the paid-through date, so access is not revoked early.
- A status the adapter does not recognize leaves local state untouched rather than guessing, and a late-arriving payment failure never resurrects an already-cancelled subscription.
- Because payments now carry their own dates, revenue, tax, and product reports attribute a renewal to the month it was collected instead of the month the subscription started.

:::note
Grace-period enforcement, cancellation, and pausing are handled by the subscription agreement described above. What is still missing is a dunning email sequence and automatic revocation of roles granted at signup when an agreement ends.
:::

## Member-only access

A subscription that only records money is not much use to a site that wants to sell membership. **Entitlements** are what a subscription grants its owner while it is current.

Attach the **Subscription Entitlements** part to a plan and pick the roles a subscriber holds. The entitlement lives on the plan rather than in a site setting because different tiers grant different things, which a single site-wide list cannot express. What the plan grants is copied onto the subscription when it is created, so editing the plan afterwards does not silently change what an existing subscriber was sold.

Roles already gate content, features, and permissions across Orchard Core, so putting a subscriber in a role makes every one of those gates subscription-aware without any of them knowing subscriptions exist.

The role is added when the subscription becomes current and removed when it stops being current — which is later than when it stops billing. A subscriber who cancels mid-cycle keeps the role through the period they paid for, and one whose renewal failed keeps it through the grace window. Removing it when access genuinely ends matters as much as granting it: a subscriber who stops paying and keeps the role keeps everything they were paying for.

To ask the question from your own code, inject **`ISubscriptionAccessService`**:

| Member | Returns |
| --- | --- |
| `HasEntitlementAsync(userId, kind, value)` | Whether the user currently holds that entitlement. Pass no value to match any of the kind. |
| `GetEntitlementsAsync(userId)` | Everything the user currently holds. |
| `GetCurrentSubscriptionsAsync(userId)` | The user's subscriptions that are current right now. |

Ask it rather than reading a subscription's status: status and access are not the same thing, and a status check locks out exactly the paying customers you least want to lose.

To grant something other than a role, implement **`ISubscriptionEntitlementApplier`** for your own kind and register it. The subscription decides whether the entitlement is current; your applier decides what that means. An applier that throws is logged and does not roll the transition back — the agreement's state is the fact, and a side effect that failed is something to retry.

## Reacting to what happens

The reactions a site owner wants when a subscription changes — welcoming a new subscriber, warning one whose
card was declined, asking a leaver why, telling a channel — differ from site to site. Hard-coding any of them
would be wrong, and a setting for each would never end, so the module raises workflow events instead.

| Workflow event | Raised when |
| --- | --- |
| **Subscription Started** | A subscriber's first payment settles and the agreement is created. |
| **Subscription Renewed** | A cycle is paid, including a recovery from past due. |
| **Subscription Past Due** | A renewal payment fails and the dunning window starts. |
| **Subscription Canceled** | The customer, an administrator, or the gateway ends the agreement. |
| **Subscription Expired** | The grace window elapses, or the agreed cycles are all billed. |

Each event carries the subscription id, title, owner, current and previous status, amount, the date access
runs through, and the grace end date. Events are correlated by the subscription id, so one workflow can wait
for a later stage of the same agreement — for example expiry after a past-due warning.

Only a genuine status change raises an event. A sweep or a provider sync that only moves dates raises
nothing, so a customer is not told their payment failed every time something touches their record.

## Getting started quickly

The **Commerce starter** recipe (under **Configuration → Recipes**) enables the suite, creates a `Member`
role, defines a **Membership Plan** content type carrying a price and an entitlement, and publishes a `$10`
monthly plan that grants the `Member` role. It is a starting point to edit rather than a production
configuration: pick your own currency, price, and roles before selling anything.

## Free trials

A recurring plan can start with a free trial. Set **Free Trial Days** on the plan and the gateway is told about it, so the gateway holds the schedule and starts billing when the trial ends.

That is deliberate: if the site simply did not charge and promised itself to start billing later, a restart would break the promise. It is also different from delaying the start of the agreement. A trial establishes the agreement now, with a payment method attached, so a trial converts into a paying subscriber without asking them to come back and buy again.

A subscription in a trial is `Trialing`, is treated as current, and grants everything the plan entitles the subscriber to. Nothing is collected at checkout and no cycle counts as billed, so a plan sold as "three cycles with a two-week trial" still bills three cycles. The first real cycle is due the moment the trial ends: a gateway bills it and reports the payment; an offline agreement is advanced by the lifecycle sweep and the first debt is recorded for that date.

A delayed start (`SubscriptionDayDelay`) is handled the same way. To the money they are the same thing: a payment method is attached now and billing begins later.

## Selling sites

With **Subscriptions - Sites** enabled on the default tenant, a plan can sell a whole Orchard Core site. The customer names their site and its administrator during checkout, and gets a running site once they have paid.

The buying and the building are deliberately separate. Creating a tenant is slow, touches a database, and fails for reasons that have nothing to do with the buyer, so doing it inline in the request that completes the checkout means a slow recipe, a brief outage, or a deployment restart leaves somebody who paid with no site and nothing to retry.

Instead the checkout only records a **provisioning job**, and a sweep builds the site from it:

- Everything the customer can still fix is checked **before** payment. A name that is taken, a domain that belongs to another site, a URL prefix already in use: all are refused by the step editor while they can still change them.
- The administrator password is data-protected the moment it is captured, never rendered back into the form, and dropped once provisioning reaches a terminal outcome.
- Each attempt is claimed under a lock, so two nodes cannot build the same site twice, and a job stranded by a process that died is picked up again rather than left in limbo.
- A failure is recorded on the job with a growing back-off. After enough failures the job is **abandoned** rather than retried forever, because at that point the honest answer is that a person needs to look at it.

Customers watch progress under **My Sites**. Administrators see every job under **Subscriptions → Site provisioning**, including the abandoned ones, and can retry any of them once they have fixed the cause.

A site sold this way carries a `Tenant` entitlement naming it, so the site runs for as long as the subscription is current and is **disabled** when it is not. Disabled, never deleted: a disabled tenant stops serving immediately but keeps every byte of the customer's data, so somebody who pays a late invoice gets their site back exactly as it was.

## Payment safety and multi-instance operation

Subscriptions inherits the hardening built into the [Payments](payments#payment-resiliency) framework:

- Deterministic **idempotency keys** on Stripe operations prevent duplicate charges and subscriptions on retries.
- **Distributed locks** serialize checkout finalization and webhook handling across instances.
- Anonymous checkout endpoints are **rate limited** (JSON `429`) to resist card-testing abuse.
- Stripe **webhooks are verified and de-duplicated** so at-least-once delivery never double-processes an event.

:::tip Multi-instance deployments
Enable the **Redis** features so distributed locks and cached checkout state are shared across every node. See the [Orchard Core Redis documentation](https://docs.orchardcore.net/en/latest/reference/modules/Redis/).
:::

### Rate limiting the checkout

Beyond the always-on throttle above, the sensitive front-end routes advertise **rate-limit groups** so administrators can attach their own policies through the optional Orchard Core [Rate Limiting](https://docs.orchardcore.net/en/latest/reference/modules/RateLimits/) feature. The metadata is inert until that feature is enabled and a matching policy is created, so it is always safe to have declared.

| Group name | Routes it covers |
| --- | --- |
| `checkout` | Starting a subscription and every checkout step. |
| `checkout-payment` | The payment endpoints that begin a payment and report its status. |

Both groups are owned by the [Checkout](checkout) module, because that is where a subscription purchase actually runs. To enable it, turn on the **Rate Limiting** feature, then under **Configuration → Rate Limiting** create a policy targeting either group. Requests exceeding the configured limit receive an HTTP `429`.

## Taxation

When the [Taxation](taxation) feature is enabled, subscriptions consume it as the authoritative tax engine — the Subscriptions module never calculates tax itself, and it keeps working normally when Taxation is disabled.

- **Checkout.** Tax is determined on the amount due now and captured on the invoice as a tax amount, detailed tax lines, and an immutable snapshot. Exclusive tax is folded into the up-front charge so the payment provider collects the exact taxed total; tax-inclusive pricing is honored without adding tax on top.
- **Recurring billing.** Each renewal is re-taxed with the rules in effect at billing time and captures its own immutable snapshot, so a rate change applies to future cycles while historical transactions never change. Because renewals are provider-driven, the charged amount is treated as tax-inclusive; configure recurring provider prices as tax-inclusive for consistent collection.
- **Address changes.** The customer's tax-relevant location is re-resolved at billing time, so moving between jurisdictions affects future cycles only.
- **Classification.** A subscription's tax category and classification come from the **Taxation** part on the subscribed content; they are persisted on the checkout invoice so renewals reuse them.

Refunds derive their tax from the original transaction snapshot via the taxation framework's refund calculator, never from current rules.

## Reports

When the [Reports](reports) feature is enabled, the Subscriptions module contributes admin reports under the **Reports** area (permission: *Manage subscriptions*):

| Report | Shows |
| --- | --- |
| Subscription revenue | Revenue, payment count, average payment, and tax collected, split by currency and bucketed by month. |
| Subscriptions dashboard | Active, new in period, expiring within 30 days, past due, and total distinct subscribers. |
| Expiring subscriptions | Agreements whose paid period ends within the horizon, soonest first. |
| New subscriptions trend | New agreements per month over the period. |
| Tax collected | Tax collected in the period, with a monthly breakdown. |
| Product performance | Revenue and tax grouped by the plan that was bought. |

Every money figure is read from the **payment ledger** and every subscriber figure from the **durable agreement**. A checkout session records what somebody was asked to pay; the ledger records what a provider confirmed was taken. Reporting the first as revenue would overstate income by every abandoned and failed checkout, which is the difference between a report an owner can file a return from and one they cannot.

Revenue is reported **per currency** and never summed across them, because a single total mixing currencies is a number that means nothing — and silently adding them hides that the site took money in a currency the owner did not expect.

## Recipes and schema

Subscription plans are content items, so they are defined and imported through Orchard Core's built-in `ContentDefinition` and `Content` recipe steps — no subscription-specific recipe step is required.

When the **`CrestApps.OrchardCore.Recipes`** feature is enabled, JSON Schema is contributed for the module's content parts, giving editor validation and IntelliSense while authoring content-definition recipes:

- **`SubscriptionPart`** — its billing payload (`InitialAmount`, `BillingDuration`, `DurationType`, `BillingCycleLimit`, `SubscriptionDayDelay`, `Sort`, and the initial-amount description) and the `SubscriptionPartSettings.ContentTypes` option.
- **`SubscriptionSummaryPart`** — the dashboard-widget marker part.
- **`TenantOnboardingPart`** — its `RecipeName` and `FeatureProfile` payload, contributed only when the **Subscriptions - Sites** feature is also enabled.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.Subscriptions
```

Then, in the **Orchard Core Admin Dashboard** under **Tools → Features**, enable **Subscriptions** (which brings in the [Checkout](checkout) framework) and at least one payment module — the **Stripe** module and/or the **[Pay Later](pay-later)** module. Configure Stripe under **Settings → Stripe**.

## Related modules

- [Checkout](checkout) — the provider-agnostic checkout framework Subscriptions builds on.
- [Pay Later](pay-later) — adds an offline pay-later option to the checkout.
- [Products](products) — supplies the priced content items that subscription plans are built on.
- [Payments](payments) — the provider-agnostic payment framework and hardened Stripe provider.
- [Taxation](taxation) — determines, snapshots, and refunds tax for subscription transactions when enabled.
- [Reports](reports) — surfaces the subscription revenue, tax, and product-performance reports.
