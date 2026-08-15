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
| **Dependencies** | `OrchardCore.Contents`, `OrchardCore.ContentTypes`, `CrestApps.OrchardCore.Products` |

The **Subscriptions** module lets you sell recurring plans built on ordinary Orchard Core content items. It adds a **`SubscriptionPart`** that turns a content type into a billable plan, a multi-step checkout flow, a subscriber dashboard, and an admin console for managing subscriptions — all on top of the provider-agnostic [Payments](payments) framework so you can charge through Stripe today and add other gateways later.

## Features

The module ships several composable features:

| Feature | Feature ID | Description |
| --- | --- | --- |
| Subscriptions | `CrestApps.OrchardCore.Subscriptions` | Core subscription plans, checkout flow, subscriber dashboard, and admin management. |
| Subscriptions - Stripe | `CrestApps.OrchardCore.Subscriptions.Stripe` | Pay for subscriptions with [Stripe](payments#the-stripe-provider). |
| Subscriptions - Pay Later | `CrestApps.OrchardCore.Subscriptions.PayLater` | A manual/deferred checkout option that records the subscription without charging a card. |
| Subscriptions - reCaptcha | `CrestApps.OrchardCore.Subscriptions.ReCaptcha` | Adds Google reCaptcha protection to the subscription process. |
| Subscriptions - Tenant Onboarding | `CrestApps.OrchardCore.Subscriptions.TenantOnboarding` | Provisions a new Orchard Core tenant as part of a subscription (default tenant only). |

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

The **Payment** step renders the payment methods advertised in `PaymentMethodOptions`. Enabling **Subscriptions - Stripe** adds the *Stripe* method (with a real processor), and **Subscriptions - Pay Later** adds a *Pay Later* method (no processor). The site owner picks the default under the subscription settings; developers add more options (for example PayPal) by registering a payment method and a checkout display driver — see [Adding another payment provider](payments#adding-another-payment-provider).

## Subscriber dashboard

Subscribers get a self-service **dashboard** (`SubscriberDashboard`) where they can review their subscriptions and related information. Recorded payments are indexed (`SubscriptionTransactionIndex`) so a subscriber's transaction history is available for display.

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

## Installation

```bash
dotnet add package CrestApps.OrchardCore.Subscriptions
```

Then, in the **Orchard Core Admin Dashboard** under **Tools → Features**, enable **Subscriptions** and the payment feature you want (**Subscriptions - Stripe** and/or **Subscriptions - Pay Later**). Configure Stripe under **Settings → Stripe**.

## Related modules

- [Products](products) — supplies the priced content items that subscription plans are built on.
- [Payments](payments) — the provider-agnostic payment framework and hardened Stripe provider.
