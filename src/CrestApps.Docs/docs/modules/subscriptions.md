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
| **Dependencies** | `OrchardCore.Contents`, `OrchardCore.ContentTypes`, `OrchardCore.Title`, `CrestApps.OrchardCore.Products`, `CrestApps.OrchardCore.Checkout` |

The **Subscriptions** module lets you sell recurring plans built on ordinary Orchard Core content items. It adds a **`SubscriptionPart`** that turns a content type into a billable plan, a multi-step checkout flow, a subscriber dashboard, and an admin console for managing subscriptions — all on top of the provider-agnostic [Payments](payments) framework so you can charge through Stripe today and add other gateways later.

## Features

The module ships several composable features:

| Feature | Feature ID | Description |
| --- | --- | --- |
| Subscriptions | `CrestApps.OrchardCore.Subscriptions` | Core subscription plans, checkout flow, subscriber dashboard, and admin management. Depends on the [Checkout](checkout) framework. |
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

Each step is a display driver against the `SubscriptionFlow`, and the server tracks progress in a `SubscriptionSession` persisted through `ISubscriptionSessionStore`. Amounts are always derived from the server-side invoice, never from client-submitted values.

### Payment methods at checkout

The **Payment** step renders the payment methods advertised in `PaymentMethodOptions`. Enabling the **Stripe** module adds the *Stripe* method (with a real processor), and enabling the **Pay Later** module adds a *Pay Later* method (no processor). The site owner picks the default under the subscription settings; developers add more options (for example PayPal) by registering a payment method and a checkout display driver — see [Adding another payment provider](payments#adding-another-payment-provider).

### Stripe checkout modes

When the **Stripe** module is enabled alongside Subscriptions, Stripe contributes two ways to collect payment, selectable from the Stripe settings page under **Checkout Mode** (see [The Stripe provider](payments#the-stripe-provider)):

- **Payment Elements (on-site)** — collects card data on your own site. Supports products that mix multiple billing intervals and up-front one-time fees.
- **Hosted Checkout (redirect)** — redirects the customer to a Stripe-hosted [Checkout Session](https://docs.stripe.com/payments/checkout), minimizing your PCI scope.

Hosted Checkout redirects the browser to Stripe and, on return, the `Subscription/CheckoutReturn` action retrieves the session from Stripe, confirms it is complete and paid, records the Stripe subscription against the local session, and finalizes the flow through the **same completion pipeline** used by Payment Elements. Because a single Checkout Session maps to a single Stripe subscription, Hosted Checkout only supports products that have a **single billing interval** and **no separate up-front one-time fee**. A product that does not meet these constraints automatically falls back to the Payment Elements experience, so switching modes never changes how a completed subscription is recorded.

## Subscriber dashboard

Subscribers get a self-service **dashboard** (`SubscriberDashboard`) where they can review their subscriptions and related information. Recorded payments are indexed (`SubscriptionTransactionIndex`) so a subscriber's transaction history is available for display. Each payment in the **Payments** list offers a **Print** action that opens a printable receipt — showing the site name, billed-to details, service plan, tax breakdown, total, and transaction reference. A receipt is only served to the subscriber that owns the transaction.

## Admin management

Administrators get a dedicated **Subscriptions** admin area (registered through `SubscriptionsAdminMenu`) to:

- Browse and filter subscription sessions through an extensible, queryable admin list (`ISubscriptionsAdminListQueryService` + `ISubscriptionAdminListFilterProvider`).
- Manage service plans and, when Stripe is enabled, synchronize plan prices with Stripe.
- Control access through the module's permission provider.

A **Subscription Summary** widget (`SubscriptionSummaryPart`) is also available for surfacing subscription information on the site.

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
