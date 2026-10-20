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

Every gateway advertises itself by registering an **`ICheckoutPaymentProvider`**. There is no separate list of
methods to configure: the payment step is built from the providers actually registered on the tenant, so the
page can never offer a method the framework cannot execute — the failure mode that leaves a customer
submitting a checkout that waits forever for a payment nothing can collect.

| Member | Description |
| --- | --- |
| `PaymentMethod.Title` | The label shown to the customer. |
| `PaymentMethod.HasProcessor` | `true` when the method actually charges a card through a gateway; `false` for manual/deferred methods (for example *Pay Later*). |

The default method is the one the site owner picked in the checkout settings, falling back to a provider that has a real gateway behind it.

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
| `PaymentDisputeUpdatedAsync` | A dispute changes: its evidence goes under review, or it is won or lost (`IsClosed`). A default interface method, so existing implementers are unaffected. |
| `SubscriptionPaymentFailedAsync` | A **renewal** of an existing subscription fails, so the subscription can be moved to past due and dunning can start. This is deliberately separate from `PaymentFailedAsync`: a one-time payment failing means a purchase did not happen, while a renewal failing puts an already-paid subscription at risk, which is a different decision made by a different consumer. |
| `SubscriptionStatusChangedAsync` | The gateway reports a new lifecycle state for a hosted subscription, including a cancellation made outside the application. Carries the provider-neutral `RemoteSubscriptionStatus`, the paid-through date, and whether the cancellation takes effect at the end of the period. |

Each context carries normalized values — transaction id, amount, currency, gateway id, and **`GatewayMode`** (`Live` or `Testing`) — so downstream code never touches provider SDK types directly. Because the gateway stays authoritative, a refund, dispute, or subscription-state notification is reconciled against durable state; it never fabricates a result the provider did not confirm. A gateway status the adapter cannot map arrives as `RemoteSubscriptionStatus.Unknown` so a consumer leaves its state untouched rather than acting on a guess.

Every method takes a `CancellationToken`, which the Stripe webhook endpoint threads from the request through the dispatcher into each handler.

## The Stripe provider

<YouTube video="commerce" title="Narrated video of connecting Stripe and choosing currencies" start={100} end={131} />

The **Stripe** feature (`CrestApps.OrchardCore.Stripe`, category *Payment Providers*) implements the framework against Stripe. Configure it under **Settings → Payments → Stripe**.

### Connecting an account

Configure the connection at the top of the settings page. Choose the environment with **Enable Production** (off = test), then connect that environment.

1. Open your [Stripe API keys](https://dashboard.stripe.com/apikeys) (use the *Test mode* switch to pick test or live keys).
2. Paste the **Secret Key** and the **Publishable Key** into the settings page; the card form on the payment step needs the publishable key. **Save** the settings: the **Connect** button appears once a secret key is saved, and is hidden again while you edit a key, so a key you typed is never lost to connecting.
3. Click **Connect**. The app verifies the saved key, resolves the account, and automatically creates a webhook endpoint pointing at `/stripe/webhook` with a fresh signing secret — no dashboard setup is required.

Connecting also registers the site's domain with the account for wallet payments (see
[Apple Pay and Google Pay](#apple-pay-and-google-pay)); a `localhost` site is skipped, since Stripe cannot reach it.

The connection status is shown for the active environment, including the resolved Stripe account id. To unlink, click **Disconnect** and confirm; the app deletes the provisioned webhook and clears the stored credentials.

If the site is not publicly reachable (for example on `localhost`), Stripe cannot create the webhook. The key and account are still saved, and a warning explains how to receive events during development: forward them with the Stripe CLI and paste the printed signing secret into the optional **Webhook signing secret** field under *Advanced*. See [Local development](#local-development).

### Settings

| Setting | Description |
| --- | --- |
| `IsLive` | Switches between the live and test key sets. |
| `CheckoutMode` | Kept for compatibility. The settings page no longer edits it, and the payment step always collects the card on the site (see below). |
| `LivePublishableKey` / `LivePrivateSecret` / `LiveWebhookSecret` | Live-mode credentials. |
| `LiveAccountId` / `LiveWebhookId` | The resolved live account id and the id of the auto-provisioned live webhook endpoint. |
| `TestPublishableKey` / `TestPrivateSecret` / `TestWebhookSecret` | Test-mode credentials. |
| `TestAccountId` / `TestWebhookId` | The resolved test account id and the id of the auto-provisioned test webhook endpoint. |

Secrets are stored as protected site settings. The active key set is projected into `StripeOptions` based on `IsLive`. Clicking **Connect** verifies the secret key, stores it, records the resolved account id, and (when the site is publicly reachable) provisions the webhook and captures its signing secret. You can also fill the fields in and **Save** to store them without provisioning a webhook.

### Configuration from the application settings

Stripe can also be configured outside the database, from the tenant's configuration (`IShellConfiguration`): `appsettings.json`, environment variables, user secrets, or a secret store such as Azure Key Vault. This suits CI, staging and demo tenants that are created and thrown away by a script, tenants provisioned automatically, and sites that keep their secrets in a vault. The section is optional and is named `CrestApps_Stripe`:

| Key | Description |
| --- | --- |
| `IsLive` | `true` uses the live keys, `false` the test keys. Leave it out to let the settings page decide. |
| `Test:PublishableKey` / `Test:SecretKey` / `Test:WebhookSecret` | The test-mode publishable key (`pk_test_`), secret key (`sk_test_`) and webhook signing secret (`whsec_`). |
| `Live:PublishableKey` / `Live:SecretKey` / `Live:WebhookSecret` | The live-mode publishable key (`pk_live_`), secret key (`sk_live_`) and webhook signing secret (`whsec_`). |

In the startup project's `appsettings.json` the section sits under the `OrchardCore` key:

```json
{
  "OrchardCore": {
    "CrestApps_Stripe": {
      "IsLive": false,
      "Test": {
        "PublishableKey": "pk_test_...",
        "SecretKey": "sk_test_...",
        "WebhookSecret": "whsec_..."
      }
    }
  }
}
```

The same values as environment variables, which is all a throwaway tenant needs to take a test-mode payment:

```text
OrchardCore__CrestApps_Stripe__IsLive=false
OrchardCore__CrestApps_Stripe__Test__PublishableKey=pk_test_...
OrchardCore__CrestApps_Stripe__Test__SecretKey=sk_test_...
OrchardCore__CrestApps_Stripe__Test__WebhookSecret=whsec_...
```

To give one tenant its own keys, put the section under the tenant's name (`OrchardCore__Contoso__CrestApps_Stripe__Test__SecretKey=...`) or in `App_Data/Sites/{TenantName}/appsettings.json` without the `OrchardCore` wrapper. See [Per-tenant configuration](../configuration.md#per-tenant-configuration).

**Which value wins.** The application configuration wins over the settings page, one value at a time. `IsLive` from configuration, when it is set, chooses the mode; otherwise the stored **Enable Production** value does. For the active mode, each key that has a value in configuration is used, and each key that does not falls back to the value stored on the settings page. Configuration wins because it belongs to whoever deploys the tenant: keys kept in environment variables or a vault are the ones an operator rotates and audits, so an administrator editing the page must not be able to shadow them without anyone noticing. A tenant whose secrets come from configuration never reads the stored secrets, so it does not depend on the database holding readable ones.

**On the settings page**, each value that comes from configuration is shown read-only with the note *Configured from the application settings.* A configured publishable key and mode are shown; a configured secret is only reported, never shown. Saving the page never overwrites the stored value of a configured field, so removing the key from configuration later brings the stored value back. See [Connect Stripe](../user-manual/commerce/payment-providers.md#connect-stripe) in the User Manual.

**Connect and Disconnect** use the configured secret key when there is one and never copy it into the database; the verified account id and webhook id are still stored. When the webhook signing secret comes from configuration, **Connect** does not create a webhook, because a new endpoint comes with a signing secret of its own that the configured one would shadow, and every event would then fail verification; register the webhook (`POST /stripe/webhook`) in the Stripe dashboard with the signing secret you configured. **Disconnect** clears the stored values only; keys in configuration stay in use until you remove them there.

Configuration is read when the tenant starts, so restart the application (or reload the tenant) after you change it.

### Moving Stripe settings between environments

The Stripe settings have a recipe step and a deployment step of their own, named `StripeSettings`, instead of the standard site settings step, because they hold data-protected secrets.

The **Stripe Settings** deployment step (category *Payments*) exports the stored settings without any secret: `LivePrivateSecret`, `LiveWebhookSecret`, `TestPrivateSecret` and `TestWebhookSecret` are left out, since they are encrypted with the source tenant's data protection keys and a deployment plan is not a place to keep a credential. The account and webhook identifiers (`LiveAccountId`, `LiveWebhookId`, `TestAccountId`, `TestWebhookId`) are left out too: they describe the source site's connection to Stripe, and a destination that adopted them would delete the source's webhook at Stripe the next time it connected or disconnected. Values from the application configuration are never part of a plan.

The `StripeSettings` recipe step imports what it carries. A member the step carries replaces the stored one; a secret the step leaves out, or leaves empty, keeps the value the destination already stores, so replaying an exported plan never erases the destination's credentials. A hand-written recipe can supply secrets in clear text, and the step protects them with the same data protection purpose the settings page uses before it stores them. Every key is checked against its Stripe prefix for its mode (`pk_test_`, `sk_live_`, `whsec_` and so on), so a key of the wrong mode, or an encrypted value copied from another database, fails the step and nothing is stored. Account and webhook identifiers in the step are ignored.

```json
{
  "steps": [
    {
      "name": "StripeSettings",
      "Settings": {
        "IsLive": false,
        "TestPublishableKey": "pk_test_...",
        "TestPrivateSecret": "sk_test_...",
        "TestWebhookSecret": "whsec_..."
      }
    }
  ]
}
```

Keep a recipe that holds clear-text secrets out of source control, or leave the secrets out and supply them through the application configuration instead. The deployment step needs the **Deployment** feature and the recipe step the **Recipes** feature.

### Checkout modes

The checkout's payment step collects card details on your own page using Stripe Elements, with Payment and Setup Intents confirmed in the browser. The amount collected always comes from the server-side checkout session, never from values the browser supplies.

`StripeOptions.CheckoutMode` and its **Hosted Checkout (redirect)** value remain for compatibility, but nothing in the checkout reads them, so the settings page no longer shows the choice.

### Webhooks

Stripe delivers events to `POST /stripe/webhook`. Clicking **Connect** provisions this endpoint and stores its signing secret automatically; for local development you can set it by hand in the matching *WebhookSecret* setting. The endpoint is engineered for Stripe's at-least-once delivery guarantee:

- The request signature is verified against the configured webhook secret before anything is deserialized.
- Each event is processed under a per-event distributed lock and recorded in a processed-event index, so a replayed or duplicated delivery is acknowledged with `200` without being handled twice.
- Handler writes and the processed-event marker are committed together **inside** the lock, so a crash or a concurrent delivery cannot reopen the double-processing window.
- When a handler throws, pending changes are discarded and the endpoint returns `500` so Stripe retries; a lock contention returns `409`.

The dispatcher maps each supported Stripe event to a provider-neutral `IPaymentEvent` call: `invoice.payment_succeeded`, `invoice.payment_failed`, `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`, `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled`, `charge.refunded`, the per-refund events (`refund.created`, `refund.updated`, `refund.failed`, `charge.refund.updated`), and `charge.dispute.created`. A `charge.refunded` event raises one `PaymentRefundedAsync` per refund on the charge (falling back to a single aggregate notification from the charge's refunded total), so a refund issued from the Stripe dashboard is reconciled against the durable ledger and never dropped.

:::note Reconnect after upgrading
The set of events an auto-provisioned webhook subscribes to is fixed when the endpoint is created. After upgrading to a version that adds event types — such as the subscription lifecycle events above — click **Connect** again so the endpoint is re-provisioned with the new list. Without it, the new events are never delivered and renewals or cancellations go unrecorded.
:::

### Workflow events

When the **Workflows** feature (`OrchardCore.Workflows`) is enabled alongside Stripe, the module contributes workflow events so you can react to Stripe activity with custom workflows — for example, emailing a receipt on payment or alerting an administrator when Stripe stops responding. The events are an independent extension: with Workflows disabled the Stripe integration behaves exactly as before, and nothing is triggered.

Under the **Stripe** category you can start a workflow from any of these events:

| Event | Raised when |
| --- | --- |
| **Stripe Payment Received** | An invoice payment succeeds (including subscription cycles). |
| **Stripe Subscription Created** | A Stripe subscription is created. |
| **Stripe Payment Intent Succeeded** | A payment intent succeeds. |
| **Stripe Payment Failed** | A payment fails at the gateway. |
| **Stripe Payment Canceled** | A payment is canceled at the gateway. |
| **Stripe Payment Refunded** | A refund is observed at the gateway. |
| **Stripe Dispute Created** | A dispute or chargeback is opened. |
| **Stripe Request Failed** | A request to Stripe fails, typically due to an authentication or connectivity problem. |

The lifecycle events (everything except *Request Failed*) are raised from the provider-neutral payment events, so they carry the same normalized values — transaction id, amount, currency, and gateway mode — as workflow input. The **Stripe Request Failed** event is raised when an outgoing Stripe API call returns `401`/`403` or cannot connect, and also when key verification or webhook provisioning fails while connecting; it is throttled to avoid flooding when many calls fail together. Use it to notify an administrator that an account's credentials or connectivity need attention.

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
5. Copy the temporary signing secret the CLI prints (it starts with `whsec_`) into the **Test Webhook Signing Secret** field (under *Advanced*) on **Settings → Payments → Stripe**, with **Enable Production** left off, and **Save**.
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

Beyond the subscription-specific endpoints, Stripe also registers a **generic `ICheckoutPaymentProvider`** when the [Checkout](checkout) feature is enabled (through a `[RequireFeatures]` startup, so there is no separate integration feature to switch on). This lets *any* checkout — recurring subscriptions today, a one-time storefront tomorrow — collect a card payment through a Stripe PaymentIntent without taking a dependency on subscriptions.

- **`BeginAsync`** creates an *unconfirmed* PaymentIntent for the attempt's gross amount (base plus the tax the checkout determined) and returns its client secret, so the browser confirms it through Strong Customer Authentication with embedded Stripe Elements.
- **`VerifyAsync`** retrieves the PaymentIntent from Stripe's authoritative API and reports the net/tax split the durable ledger validates, so an obligation is never marked paid on a cached webhook.
- Every amount crosses the Stripe boundary through **`StripeCurrency`**, which honors zero-decimal (JPY) and three-decimal (KWD, rounded to a multiple of ten) currencies.

### Saved cards and charges without the customer

The provider also keeps a card for later and charges it without the customer present, which is what
[installment plans](installment-plans) are built on (`SupportsSavedPaymentMethods`). Both are asked
for through the provider-neutral `CheckoutPaymentDataKeys` on an ordinary checkout begin:

- **`savePaymentMethod`** — the browser tokenizes the card first; the PaymentIntent is created for a Stripe
  customer with that card and `setup_future_usage = off_session`, and the customer (or the administrator entering
  the card for them) confirms it as usual. Authenticating this payment is what lets later charges skip it.
  `ICheckoutSavedPaymentMethodProvider.GetSavedPaymentMethodAsync` then reads back the customer, the card, its
  brand, last four digits and expiry.
- **`offSession`** with **`savedCustomerReference`** and **`savedPaymentMethodReference`** — the PaymentIntent is
  created and confirmed server-side with `off_session = true`. A declined card is reported as a decline carrying
  Stripe's reason, and the engine records the attempt as failed; a bank that insists on authenticating the
  cardholder declines with `authentication_required`, and that payment has to be made by the customer. A network
  failure is not a decline, so it is retried without counting against the card. Only server code sets these keys:
  the down payment page strips them from anything the browser sends.

### Saved cards for the buyer's own purchases

A signed-in buyer paying for a one-time purchase can keep the card and pay with it later. **`IStripeWalletService`** keeps one Stripe customer per user (stored on the user, created with `wallet_user_id` in its metadata) and lists that customer's cards live from Stripe. The payment panel lists them and offers **Save this card for next time**; the begin call then carries one of two Stripe provider data keys:

- **`savedCardId`** (`StripeCheckoutPaymentProvider.SavedCardDataKey`) — the PaymentIntent is created on the buyer's customer with that card, after checking it is one of theirs; a guest's request, or a card that isn't theirs, is refused.
- **`rememberCard`** with **`paymentMethodId`** — the card the browser tokenized is attached to the buyer's customer with `setup_future_usage = on_session` (`CreateCheckoutPaymentIntentRequest.SaveForOnSessionUse`). A guest's request pays without keeping the card.

Either way the buyer confirms with Stripe.js as usual. `account/payment-methods` (`AccountPaymentMethods`) lists the cards in the storefront account and detaches one, again only after checking it is the buyer's. The personal data contributor redacts wallet customers like the ones the checkout creates.

### Disputes and fraud rating

The webhook translates `charge.dispute.created`, `charge.dispute.updated` and `charge.dispute.closed` into `PaymentDisputeCreatedAsync` and `PaymentDisputeUpdatedAsync`; the Orders feature records them on the order (see [Carts and Orders](carts-and-orders#disputes-and-fraud-rating)). Verifying a PaymentIntent expands its latest charge and reports Radar's `outcome.risk_level` and `risk_score` as `PaymentVerificationResult.RiskLevel` and `RiskScore`, which the reconciliation keeps on the attempt.

### Apple Pay and Google Pay

The card panel of the checkout also offers the wallets a device already holds, through Stripe's Express Checkout
Element: Apple Pay in Safari on an iPhone, iPad or Mac, and Google Pay in Chrome on Android and the desktop. A payer
on a phone confirms with a tap, Face ID or a fingerprint instead of typing a card number. The buttons appear above the
card form only when the device has a wallet set up and there is an amount to pay now; elsewhere the card form shows
alone.

- A wallet pays with a card, so everything the card form does still holds: the payment goes through the same
  checkout, the server decides the amount and verifies the result with Stripe, and a card kept for later charges
  (an installment plan, a subscription) is kept the same way. Only wallets that produce a card are offered; Link,
  PayPal, Amazon Pay and Klarna are turned off.
- Stripe offers a wallet only on a domain registered with the account. **Connect** registers the site's own domain
  for you; a site served from another domain too can add it under *Settings → Payment method domains* in the Stripe
  Dashboard. Wallets need HTTPS.
- Samsung Pay is not a separate button: on a Samsung phone the payer pays through Google Pay, which can hold the
  same cards.

Taking a card that is tapped against the administrator's own phone (Stripe's *Tap to Pay*) needs Stripe Terminal
inside a native iOS or Android app, and cannot be done from a web page.

### Stripe Terminal readers

With the [Point of Sale](point-of-sale) feature on, the Stripe module adds `StripeTerminalPaymentProvider` (key `stripe-terminal`, *Card on a Stripe reader*), which takes a register's card on a Stripe Terminal reader through Stripe's server-driven integration, and the `pos/terminal/readers` endpoints a till app lists and pairs readers with. A simulated reader (registration code `simulated-wpe`) and a test card presented on it work in test mode. See [Point of Sale](point-of-sale#stripe-terminal).

### Refunds

<YouTube video="commerce" title="Narrated video of the Payments list, a partial refund, and the Refunds list" start={500} end={534} />

The same provider implements **`ICheckoutPaymentRefundProvider`**, so a settled Stripe payment can be refunded through the checkout's durable refund ledger (`ICheckoutRefundService`) rather than by calling Stripe directly. Refunds are recorded before Stripe is contacted, carry the refund's idempotency key so a retry never double-refunds, and allocate tax from the original payment's immutable snapshot. See [Checkout → Refunds](checkout#refunds--the-durable-refund-ledger) for the full model. The refund itself runs through the `IStripeRefundService`, which converts the major-unit amount to the currency's minor units with `StripeCurrency`.

## Adding another payment provider

The checkout is extensible from both configuration and code. To surface a new gateway (for example PayPal) as a checkout option:

1. **Advertise the method.** Register an `ICheckoutPaymentProvider` under a unique provider key, declaring what it can settle through its `PaymentProviderCapabilities`:

   ```csharp
   services.AddScoped<ICheckoutPaymentProvider, MyGatewayCheckoutPaymentProvider>();
   ```

2. **Render its checkout UI.** Provide an `IDisplayDriver<SubscriptionFlowPaymentMethod>` (Subscriptions) or the equivalent display driver for your flow, so the method renders its own fields/redirect on the payment step when selected.

3. **Handle the money.** Add your endpoints/redirect to create the charge, and implement `IPaymentEvent` (or `PaymentEventBase`) to translate the provider's webhook into the normalized events the rest of the system already understands.

Because consumers depend only on `ICheckoutPaymentProvider` and `IPaymentEvent`, no changes to the Subscriptions or Products modules are required to add a provider.

## Taxes

Payment providers are **tax-agnostic**. They receive the final amount already determined by checkout — including any applicable tax — and never calculate, source, or interpret tax themselves. Tax determination is owned by the [Taxation](taxation) framework and captured on the transaction before payment; the provider simply charges the amount it is given. This keeps every provider (Stripe, PayPal, or a custom gateway) free of tax rules, jurisdictions, and exemptions, and keeps the Orchard transaction the single source of truth for tax.

## Installation

```bash
dotnet add package CrestApps.OrchardCore.Stripe
```

Then enable **Stripe** in the **Orchard Core Admin Dashboard** under **Tools → Features** and configure it under **Settings → Payments → Stripe**.

## Related modules

- [Subscriptions](subscriptions) — the primary consumer of the Payments framework.
- [Products](products) — supplies the priced content items being charged for.
- [Taxation](taxation) — determines the tax included in the amount the provider charges.
