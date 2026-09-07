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
| **Dependencies** | `OrchardCore.Contents`, `OrchardCore.ContentTypes`, `OrchardCore.Title`, `CrestApps.OrchardCore.Products`, `CrestApps.OrchardCore.Checkout`, `CrestApps.OrchardCore.Wizard` |

The **Subscriptions** module lets you sell recurring plans built on ordinary Orchard Core content items. It adds a **`SubscriptionPart`** that turns a content type into a billable plan, a multi-step checkout flow hosted by the shared [Wizard](wizard) feature, a subscriber dashboard, and an admin console for managing subscriptions — all on top of the provider-agnostic [Payments](payments) framework so you can charge through Stripe today and add other gateways later.

## Features

The module ships several composable features:

| Feature | Feature ID | Description |
| --- | --- | --- |
| Subscriptions | `CrestApps.OrchardCore.Subscriptions` | Core subscription plans, checkout flow, subscriber dashboard, and admin management. Depends on the [Checkout](checkout) and [Wizard](wizard) frameworks. |
| Subscriptions - reCaptcha | `CrestApps.OrchardCore.Subscriptions.ReCaptcha` | Adds Google reCaptcha protection to the subscription process. |
| Subscriptions - Tenant Onboarding | `CrestApps.OrchardCore.Subscriptions.TenantOnboarding` | Provisions a new Orchard Core tenant as part of a subscription (default tenant only). |

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
| `Sort` | Sort order used when listing subscription options. |

Each content item of that type becomes a purchasable plan, editable and securable like any other Orchard Core content.

## The checkout flow

Subscribing runs through an extensible, server-driven **subscription flow** composed of ordered steps. The built-in steps are:

1. **Content** — collects any content the plan requires.
2. **User Registration** — registers or signs in the subscriber when the plan requires an account.
3. **Tenant Onboarding** — provisions a dedicated tenant (only with the *Tenant Onboarding* feature).
4. **Payment** — selects a payment method and collects payment.

Each step is still authored as a display driver against the `SubscriptionFlow`, and the server still persists subscription-specific state in `SubscriptionSession` through `ISubscriptionSessionStore`. The public host, navigation, completion locking, and resume behavior now come from the shared `CrestApps.OrchardCore.Wizard` feature, so subscriptions reuses the common wizard controller and engine instead of duplicating that infrastructure. Amounts are always derived from the server-side invoice, never from client-submitted values.

The invoice currency comes from the subscribed product itself: the flow resolves the product through `IProductSnapshotResolver` and bills in the product-owned currency. A product that declares no currency at all (neither its own nor its content type's default) is not sellable, so the flow fails closed rather than billing it in a guessed currency; the site subscription currency applies only when the flow content item is not a product. Prices are never converted between currencies — when Stripe price synchronization is asked for a currency that differs from the product's own currency, that price is skipped and a warning is logged rather than silently relabeled.

The site-level subscription currency picker and the product editors now read from the shared **Commerce → Currencies** catalog, so editors select friendly managed values such as **US Dollar (USD)** instead of typing currency codes free-form.

### Payment methods at checkout

The **Payment** step renders the payment methods advertised in `PaymentMethodOptions`. Enabling the **Stripe** module adds the *Stripe* method (with a real processor), and enabling the **Pay Later** module adds a *Pay Later* method (no processor). The site owner picks the default under the subscription settings; developers add more options (for example PayPal) by registering a payment method and a checkout display driver — see [Adding another payment provider](payments#adding-another-payment-provider).

The checkout UI presents those methods as selection cards and keeps the processor-specific input inside a dedicated panel, which makes the difference between online payment and offline pay-later flows clearer at checkout.

### Stripe checkout modes

When the **Stripe** module is enabled alongside Subscriptions, Stripe contributes two ways to collect payment, selectable from the Stripe settings page under **Checkout Mode** (see [The Stripe provider](payments#the-stripe-provider)):

- **Payment Elements (on-site)** — collects card data on your own site. Supports products that mix multiple billing intervals and up-front one-time fees.
- **Hosted Checkout (redirect)** — redirects the customer to a Stripe-hosted [Checkout Session](https://docs.stripe.com/payments/checkout), minimizing your PCI scope.

Hosted Checkout redirects the browser to Stripe and, on return, the `Subscription/CheckoutReturn` action retrieves the session from Stripe, confirms it is complete and paid, records the Stripe subscription against the local session, and finalizes the flow through the **same completion pipeline** used by Payment Elements. Because a single Checkout Session maps to a single Stripe subscription, Hosted Checkout only supports products that have a **single billing interval** and **no separate up-front one-time fee**. A product that does not meet these constraints automatically falls back to the Payment Elements experience, so switching modes never changes how a completed subscription is recorded.

## Subscriber dashboard

Subscribers get a self-service **dashboard** (`SubscriberDashboard`) where they can review their subscriptions and related information. Recorded payments are indexed (`SubscriptionTransactionIndex`) so a subscriber's transaction history is available for display. Each payment in the **Payments** list offers a **Print** action that opens a printable receipt, rendered through the reusable [Receipts](receipts) module — showing the configured issuer branding, billed-to details, service plan, tax breakdown, total, and transaction reference. A receipt is only served to the subscriber that owns the transaction.

The **Payments** and **Subscriptions** lists are each paged and sorted latest-first — payments by their payment date and subscriptions by their start date. Both lists carry their own independent pager (the payments pager uses the `invoicesPage` query key and the subscriptions pager uses `subscriptionsPage`), so paging one list never disturbs the other. The page size follows the site-wide pager option.

## Admin management

Administrators get a dedicated **Subscriptions** admin area (registered through `SubscriptionsAdminMenu`) to:

- Browse and filter subscription sessions through an extensible, queryable admin list (`ISubscriptionsAdminListQueryService` + `ISubscriptionAdminListFilterProvider`).
- Manage service plans and, when Stripe is enabled, synchronize plan prices with Stripe.
- Control access through the module's permission provider.

A **Subscription Summary** widget (`SubscriptionSummaryPart`) is also available for surfacing subscription information on the site.

Alongside the session list there is an **Agreements** screen backed by the durable subscription record described below. From an agreement's detail page an administrator can cancel it (immediately or at the close of the paid period), pause billing, and resume it. Every one of those actions goes through the lifecycle service rather than editing the record, so an administrator acting in the admin cannot race a gateway webhook or the nightly sweep, and each action is written to the agreement's own history.

Subscribers get a matching **My Plans** screen where they can see what they subscribe to and cancel it themselves. Cancelling from there defaults to ending at the close of the period they already paid for, because they bought that time.

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

A subscription does not end when checkout completes; it renews, can fall behind, and can be cancelled. The module tracks that afterlife from the payment gateway's notifications, so the site does not have to infer a subscription's fate from payments quietly stopping.

| Gateway notification | Effect on the recorded subscription |
| --- | --- |
| Cycle payment succeeded | The payment is recorded with **its own collection date**, and the subscription's expiration advances by exactly one billing cycle. |
| Cycle payment failed | The subscription moves to **Past due** and the date it first fell behind is recorded, so a dunning window can be measured from it. |
| Subscription updated | The status, paid-through date, and pending cancellation are adopted from the gateway. |
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

A recurring plan can start with a free trial. Set the trial days on the plan and the gateway is told about it, so the gateway holds the schedule and starts billing when the trial ends.

That is deliberate: if the site simply did not charge and promised itself to start billing later, a restart would break the promise. It is also different from delaying the start of the agreement. A trial establishes the agreement now, with a payment method attached, so a trial converts into a paying subscriber without asking them to come back and buy again.

A subscription in a trial is `Trialing`, is treated as current, and grants everything the plan entitles the subscriber to. Its first cycle settles for nothing collected, which is exactly right: nothing was.

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

## Tenant onboarding

With **Subscriptions - Tenant Onboarding** enabled (on the default tenant), a subscription can provision a brand-new Orchard Core tenant as part of checkout — for example to sell isolated SaaS workspaces. The provisioning is resilient to failures, and two workflow events let you react to the outcome:

| Workflow event | Raised when |
| --- | --- |
| `SubscribedTenantSetupSucceededEvent` | A subscribed tenant is provisioned successfully. |
| `SubscribedTenantFailedSetupEvent` | Provisioning a subscribed tenant fails. |

Use these events in Orchard Core workflows to send notifications, seed data, or trigger compensating actions.

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
| `subscription-checkout` | The signup form submission and every checkout flow step. |
| `subscription-payment` | The anonymous payment endpoints (payment/setup intents, checkout session, subscription creation, and pay-later confirmation). |

To enable it, turn on the **Rate Limiting** feature, then under **Configuration → Rate Limiting** create a policy targeting the `subscription-checkout` and/or `subscription-payment` group. Requests exceeding the configured limit receive an HTTP `429`.

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
| Subscription revenue | Total revenue, transaction count, average value, tax collected, and revenue by month. |
| Subscriptions dashboard | Active subscriptions, new in period, expiring within 30 days, and total subscribers. |
| Expiring subscriptions | Subscriptions expiring within the horizon, ordered by expiry. |
| New subscriptions trend | New subscriptions per month over the period. |
| Tax collected | Tax collected in the period, with a monthly breakdown. |
| Product performance | Revenue and tax grouped by product. |

## Recipes and schema

Subscription plans are content items, so they are defined and imported through Orchard Core's built-in `ContentDefinition` and `Content` recipe steps — no subscription-specific recipe step is required.

When the **`CrestApps.OrchardCore.Recipes`** feature is enabled, JSON Schema is contributed for the module's content parts, giving editor validation and IntelliSense while authoring content-definition recipes:

- **`SubscriptionPart`** — its billing payload (`InitialAmount`, `BillingDuration`, `DurationType`, `BillingCycleLimit`, `SubscriptionDayDelay`, `Sort`, and the initial-amount description) and the `SubscriptionPartSettings.ContentTypes` option.
- **`SubscriptionSummaryPart`** — the dashboard-widget marker part.
- **`TenantOnboardingPart`** — its `RecipeName` and `FeatureProfile` payload, contributed only when the **Tenant Onboarding** feature is also enabled.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.Subscriptions
```

Then, in the **Orchard Core Admin Dashboard** under **Tools → Features**, enable **Subscriptions** (which brings in the [Checkout](checkout) framework) and the payment module you want — the **Stripe** module and/or the **[Pay Later](pay-later)** module. Configure Stripe under **Settings → Stripe**.

## Related modules

- [Checkout](checkout) — the provider-agnostic checkout framework Subscriptions builds on.
- [Pay Later](pay-later) — adds an offline pay-later option to the checkout.
- [Products](products) — supplies the priced content items that subscription plans are built on.
- [Payments](payments) — the provider-agnostic payment framework and hardened Stripe provider.
- [Taxation](taxation) — determines, snapshots, and refunds tax for subscription transactions when enabled.
- [Reports](reports) — surfaces the subscription revenue, tax, and product-performance reports.
