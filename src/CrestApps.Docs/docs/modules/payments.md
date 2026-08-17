---
sidebar_label: Payments
sidebar_position: 13
title: Payments
description: A provider-agnostic payment framework for Orchard Core, with a hardened Stripe provider supporting on-site Payment Elements and Stripe-hosted Checkout.
---

| | |
| --- | --- |
| **Framework** | `CrestApps.OrchardCore.Payments.Abstractions` |
| **Provider Feature** | Stripe — `CrestApps.OrchardCore.Stripe` |
| **Category** | Payment Providers |

The **Payments** framework defines the provider-agnostic contracts that let any module accept money without binding to a specific gateway, and ships a production-hardened **Stripe** provider. Modules such as [Subscriptions](subscriptions) consume these contracts so a site owner can pick a payment method at checkout and a developer can add a new gateway (for example PayPal) that shows up as an additional checkout option.

## Concepts

### Payment methods and options

Every gateway advertises itself as a **`PaymentMethod`** registered into the shared **`PaymentMethodOptions`**:

| Member | Description |
| --- | --- |
| `PaymentMethodOptions.PaymentMethods` | The dictionary of available methods, keyed by a stable *processor key* (for example `Stripe`, `PayLater`). |
| `PaymentMethodOptions.DefaultPaymentMethod` | The method selected by default at checkout. |
| `PaymentMethod.Title` | The label shown to the customer. |
| `PaymentMethod.HasProcessor` | `true` when the method actually charges a card through a gateway; `false` for manual/deferred methods (for example *Pay Later*). |

The default method is resolved by an `IPostConfigureOptions<PaymentMethodOptions>` that honors the site owner's configured default and otherwise prefers a method that has a real processor.

### Payment events

Gateways translate their webhooks into a normalized event stream through **`IPaymentEvent`** (base class **`PaymentEventBase`**). Consumers implement it to react to payments regardless of provider:

| Method | Raised when |
| --- | --- |
| `PaymentIntentSucceededAsync` | A payment intent is confirmed. |
| `PaymentSucceededAsync` | A payment (initial, renewal, or update) succeeds. Carries a `PaymentReason`. |
| `CustomerSubscriptionCreatedAsync` | A recurring subscription is created at the gateway. |
| `PaymentFailedAsync` | A payment fails at the gateway. Carries the gateway failure code and reason. |
| `PaymentCanceledAsync` | A payment is canceled at the gateway. Carries the cancellation reason. |
| `PaymentRefundedAsync` | A refund is observed at the gateway — including one issued out-of-band from the provider dashboard — so the durable refund ledger can be reconciled. |
| `PaymentDisputeCreatedAsync` | A dispute or chargeback is opened against a settled payment. |

Each context carries normalized values — transaction id, amount, currency, gateway id, and **`GatewayMode`** (`Live` or `Testing`) — so downstream code never touches provider SDK types directly. Because the gateway stays authoritative, a refund or dispute notification is reconciled against durable state; it never fabricates a result the provider did not confirm.

## The Stripe provider

The **Stripe** feature (`CrestApps.OrchardCore.Stripe`, category *Payment Providers*) implements the framework against Stripe. Configure it under **Settings → Stripe**.

### Settings

| Setting | Description |
| --- | --- |
| `IsLive` | Switches between the live and test key sets. |
| `CheckoutMode` | The Stripe integration model to use (see below). |
| `LivePublishableKey` / `LivePrivateSecret` / `LiveWebhookSecret` | Live-mode credentials. |
| `TestPublishableKey` / `TestPrivateSecret` / `TestWebhookSecret` | Test-mode credentials. |

Secrets are stored as protected site settings. The active key set is projected into `StripeOptions` based on `IsLive`.

### Checkout modes

The `CheckoutMode` setting selects how card details are collected:

| Mode | Description |
| --- | --- |
| **Payment Elements (on-site)** | Collects payment on your own page using Stripe Elements with Payment/Setup Intents confirmed in the browser. This is the original integration. |
| **Hosted Checkout (redirect)** | Redirects the customer to a Stripe-hosted Checkout page created from a Checkout Session. This is the integration Stripe currently recommends. |

Both modes flow through the same server-side session so the collected amount is always derived from the server invoice, never from client-supplied values.

### Webhooks

Stripe delivers events to `POST /stripe/webhook`. Set the endpoint's signing secret in the matching *WebhookSecret* setting. The endpoint is engineered for Stripe's at-least-once delivery guarantee:

- The request signature is verified against the configured webhook secret before anything is deserialized.
- Each event is processed under a per-event distributed lock and recorded in a processed-event index, so a replayed or duplicated delivery is acknowledged with `200` without being handled twice.
- Handler writes and the processed-event marker are committed together **inside** the lock, so a crash or a concurrent delivery cannot reopen the double-processing window.
- When a handler throws, pending changes are discarded and the endpoint returns `500` so Stripe retries; a lock contention returns `409`.

The dispatcher maps each supported Stripe event to a provider-neutral `IPaymentEvent` call: `invoice.payment_succeeded`, `customer.subscription.created`, `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled`, `charge.refunded`, and `charge.dispute.created`. A `charge.refunded` event raises one `PaymentRefundedAsync` per refund on the charge (falling back to a single aggregate notification from the charge's refunded total), so a refund issued from the Stripe dashboard is reconciled against the durable ledger and never dropped.

### Local development

To exercise the webhook pipeline on your machine, forward Stripe events to your local server with the [Stripe CLI](https://docs.stripe.com/stripe-cli):

1. Enable and configure the **Stripe** feature as described under [The Stripe provider](#the-stripe-provider).
2. Install the Stripe CLI — see [Get started with the Stripe CLI](https://docs.stripe.com/stripe-cli#install).
3. Authenticate the CLI with your Stripe account:

   ```sh
   stripe login
   ```

   This opens a browser window to authorize the CLI.
4. Forward webhook events to your local endpoint, replacing `your-port` with the port your site runs on (for example `5000`):

   ```sh
   stripe listen --forward-to https://localhost:your-port/stripe/webhook --skip-verify
   ```

   `--skip-verify` is required when forwarding to an HTTPS address that uses the ASP.NET Core development certificate; alternatively, forward to the plain HTTP endpoint (`http://localhost:your-port/stripe/webhook`) and omit the flag.
5. Copy the temporary signing secret the CLI prints (it starts with `whsec_`) into the **Test Webhooks Secret** field under **Settings → Stripe**, with **Enable Production** left off.
6. Trigger a sample event to confirm the pipeline end to end:

   ```sh
   stripe trigger payment_intent.succeeded
   ```

   The `stripe listen` window should report a `200` response from `/stripe/webhook` for the forwarded event.

## Payment resiliency

The Stripe integration and the [Subscriptions](subscriptions) endpoints that drive it were hardened for multi-instance, production use:

- **Deterministic idempotency keys** are attached to every mutating Stripe API call, so a retried request resolves to the original result instead of creating a duplicate charge or subscription.
- **Distributed locking** (via `IDistributedLock`, backed by Redis in multi-instance deployments) serializes checkout finalization and webhook handling across all instances.
- **Currency-aware minor-unit conversion** ensures amounts are sent to Stripe in the correct smallest unit for each currency.
- **Rate limiting** protects the anonymous checkout endpoints against card-testing abuse, returning a JSON `429` when a caller exceeds the configured window.

:::tip Multi-instance deployments
For load-balanced or multi-instance hosting, enable the **Redis** features (`OrchardCore.Redis`, `OrchardCore.Redis.Lock`, `OrchardCore.Redis.Cache`, `OrchardCore.Redis.Bus`) so the distributed locks and cached payment state are shared across every node. See the [Orchard Core Redis documentation](https://docs.orchardcore.net/en/latest/reference/modules/Redis/).
:::

## Stripe as a generic checkout provider

Beyond the subscription-specific endpoints, Stripe also registers a **generic `ICheckoutPaymentProvider`** when the [Checkout](checkout) feature is enabled (through a `[RequireFeatures]` startup, so there is no separate integration feature to switch on). This lets *any* checkout — recurring subscriptions today, a one-time storefront tomorrow — collect a card payment through a Stripe PaymentIntent without taking a dependency on the subscription flow.

- **`BeginAsync`** creates an *unconfirmed* PaymentIntent for the attempt's gross amount (base plus the tax the checkout determined) and returns its client secret, so the browser confirms it through Strong Customer Authentication with embedded Stripe Elements.
- **`VerifyAsync`** retrieves the PaymentIntent from Stripe's authoritative API and reports the net/tax split the durable ledger validates, so an obligation is never marked paid on a cached webhook.
- Every amount crosses the Stripe boundary through **`StripeCurrency`**, which honors zero-decimal (JPY) and three-decimal (KWD, rounded to a multiple of ten) currencies.

### Refunds

The same provider implements **`ICheckoutPaymentRefundProvider`**, so a settled Stripe payment can be refunded through the checkout's durable refund ledger (`ICheckoutRefundService`) rather than by calling Stripe directly. Refunds are recorded before Stripe is contacted, carry the refund's idempotency key so a retry never double-refunds, and allocate tax from the original payment's immutable snapshot. See [Checkout → Refunds](checkout#refunds--the-durable-refund-ledger) for the full model. The refund itself runs through the `IStripeRefundService`, which converts the major-unit amount to the currency's minor units with `StripeCurrency`.

## Adding another payment provider

The checkout is extensible from both configuration and code. To surface a new gateway (for example PayPal) as a checkout option:

1. **Advertise the method.** Register a `PaymentMethod` in `PaymentMethodOptions` under a unique processor key:

   ```csharp
   services.Configure<PaymentMethodOptions>(options =>
   {
       options.PaymentMethods["PayPal"] = new PaymentMethod
       {
           Title = "PayPal",
           HasProcessor = true,
       };
   });
   ```

2. **Render its checkout UI.** Provide an `IDisplayDriver<SubscriptionFlowPaymentMethod>` (Subscriptions) or the equivalent display driver for your flow, so the method renders its own fields/redirect on the payment step when selected.

3. **Handle the money.** Add your endpoints/redirect to create the charge, and implement `IPaymentEvent` (or `PaymentEventBase`) to translate the provider's webhook into the normalized events the rest of the system already understands.

Because consumers depend only on `PaymentMethodOptions` and `IPaymentEvent`, no changes to the Subscriptions or Products modules are required to add a provider.

## Taxes

Payment providers are **tax-agnostic**. They receive the final amount already determined by checkout — including any applicable tax — and never calculate, source, or interpret tax themselves. Tax determination is owned by the [Taxation](taxation) framework and captured on the transaction before payment; the provider simply charges the amount it is given. This keeps every provider (Stripe, PayPal, or a custom gateway) free of tax rules, jurisdictions, and exemptions, and keeps the Orchard transaction the single source of truth for tax.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.Stripe
```

Then enable **Stripe** in the **Orchard Core Admin Dashboard** under **Tools → Features** and configure it under **Settings → Stripe**.

## Related modules

- [Subscriptions](subscriptions) — the primary consumer of the Payments framework.
- [Products](products) — supplies the priced content items being charged for.
- [Taxation](taxation) — determines the tax included in the amount the provider charges.
