---
sidebar_label: Checkout
sidebar_position: 12
title: Checkout
description: A provider-agnostic checkout and payment framework for Orchard Core, reusable by subscriptions and one-time goods purchases, with a durable payment ledger that prevents orphaned records across distributed nodes.
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Checkout` |
| **Abstractions** | `CrestApps.OrchardCore.Checkout.Abstractions` |
| **Core** | `CrestApps.OrchardCore.Checkout.Core` |
| **Category** | Commerce |

The **Checkout** feature provides a provider-agnostic checkout and payment framework. It is the reusable foundation that any purchase flow builds on — recurring [Subscriptions](subscriptions) as well as one-time goods purchases — so the wizard, the invoice, taxation, and the money-handling safety guarantees are written once and shared.

It deliberately does **not** implement a storefront. It defines the contracts and the durable, distributed-safe machinery for collecting money; a consuming module (such as Subscriptions) contributes the domain-specific steps and decides what a completed checkout means.

## Why a dedicated framework

Handling money is sensitive: payments are settled by outside vendors, so the site must never record a payment as *paid* when the gateway actually failed, and must never lose a real charge because a cache entry expired or a node crashed mid-checkout. The Checkout framework centralizes the patterns that make this safe:

- A **durable payment ledger** persisted in the tenant database — never only in a distributed cache.
- **Verification against the provider's authoritative API** before a checkout is ever marked complete.
- **Currency-correct money handling** for every ISO-4217 currency, including zero-decimal (JPY) and three-decimal (KWD) currencies.
- **Distributed coordination** through `IDistributedCache` and `IDistributedLock` so the same guarantees hold when Orchard Core runs on multiple nodes.

## Concepts

### Checkout session

A **`CheckoutSession`** is the provider-agnostic unit of work for any purchase. It references *what* is being bought through a neutral `ReferenceType` / `ReferenceId` / `ReferenceVersionId` triple, so it is not tied to content items. Its property bag carries the `CheckoutInvoice`, provider metadata, and the confirmed `PaymentsMetadata`.

Sessions are persisted by **`ICheckoutSessionStore`**, which enforces ownership: an anonymous session is bound to its originating IP address and user agent so it cannot be resumed by a different visitor.

#### The canonical order reference

The reference triple is intentionally generic so any consumer can drive a checkout, but ecommerce orders use one stable relationship, published as the well-known constants in **`CheckoutReferenceTypes`**:

- `ReferenceType` is `CheckoutReferenceTypes.Order` (`"Order"`);
- `ReferenceId` is the owning order's stable item id;
- `ReferenceVersionId` identifies the draft or quote version only when the order requires versioning, and is otherwise left empty.

The order owns the reverse link by storing its checkout session id, and `ICheckoutSessionStore.GetByReferenceAsync(referenceType, referenceId, referenceVersionId)` resolves the most recent session for a reference when only the order is known. Payment attempts and refunds remain owned by Checkout and correlate through the session and provider transaction references. Because payment stays authoritative in the durable attempt ledger, **an order must never be marked paid from a session status alone**.

### Checkout flow and steps

A **`CheckoutFlow`** provides step navigation (first/next/previous/current) over the session's ordered **`CheckoutFlowStep`** list. Features contribute steps and their **billing items** while a session is being activated by implementing **`ICheckoutHandler`** (or deriving from `CheckoutHandlerBase`). The handler lifecycle mirrors the wizard: `Activating` → `Activated` → `Initializing`/`Initialized` → `Loading`/`Loaded` → `Completing` → `Completed`, with `Failed` on error.

### Checkout invoice

A single **`CheckoutInvoice`** is built for the whole checkout so the customer is charged exactly once regardless of how many steps contributed billing items. It records the one-time amount due now, the first recurring amount charged now, the recurring subtotals grouped by billing interval, and the tax determined for the amount due now (with an immutable `TaxSnapshot`).

### Payment providers

A gateway implements the first-class **`ICheckoutPaymentProvider`** contract:

| Member | Responsibility |
| --- | --- |
| `Key` / `DisplayName` | Stable identity and the label shown to the customer. |
| `Capabilities` | Declares what the provider can do (one-time, recurring, hosted redirect, embedded elements, dynamic tax collection, refunds). |
| `BeginAsync` | Begins a payment for a durable attempt; returns the provider's authoritative reference so it can be persisted immediately. |
| `VerifyAsync` | Queries the provider's authoritative API and reports what really happened. This is the source of truth at completion. |
| `CancelAsync` | Cancels or compensates a remote resource for an abandoned or rolled-back attempt. |

Capabilities let the framework select a suitable provider and enforce constraints — for example, refusing to add a separate up-front fee to a provider-hosted page that cannot represent one.

### Built-in payment providers

- **Pay Later** — provided by the standalone **[Pay Later](pay-later)** module (`CrestApps.OrchardCore.PayLater`). It records an offline commitment instead of moving money through a gateway. Because it never contacts a processor, its verification reports that it is *not* the authoritative source of a charged amount, so the checkout records the commitment on the strength of a recorded transaction id alone — without an amount cross-check — while still flowing through the exact same durable ledger and reconciliation as a real gateway. This keeps the safety guarantees intact and never fabricates a *paid* record a processor could contradict.
- **Stripe** — provided by the **[Stripe](payments#stripe-as-a-generic-checkout-provider)** module. When both the Stripe and Checkout features are enabled, Stripe registers a generic `ICheckoutPaymentProvider` (and `ICheckoutPaymentRefundProvider`) so *any* checkout — subscriptions today, a storefront tomorrow — can collect and refund a card payment through a Stripe PaymentIntent without depending on the subscription-specific endpoints. It verifies against Stripe's authoritative API and converts every amount through `StripeCurrency`.

## The durable payment ledger

The heart of the safety model is the **`PaymentAttempt`** — a durable, per-obligation record of a single interaction with a provider, stored through **`IPaymentAttemptStore`** in the tenant database.

The rules that prevent orphaned records:

1. **Persist before you call.** An attempt is written (`Created`) *before* the provider is ever contacted, so a crash or node failure can never strand an untracked charge.
2. **Record the reference immediately.** The provider's authoritative reference (for example a PaymentIntent or remote subscription id) is stored on the attempt as soon as it is returned, so the remote resource is never lost even if a later step fails.
3. **Per obligation.** A checkout that spans several billing intervals plus a one-time amount gets one attempt *per obligation*, so a partial failure is always attributable and compensatable.
4. **Idempotent.** Each attempt carries an idempotency key, so a retried attempt resumes rather than double-charges.

### Reconciliation — never mark paid on a guess

**`ICheckoutReconciliationService`** is what actually completes a checkout safely. It:

- loads every attempt for the session,
- verifies each non-terminal attempt against the owning provider's authoritative API,
- records a confirmed payment **only** when the provider says it succeeded, and
- reports whether **every** expected obligation is settled.

A cached webhook notification is only a hint; it never completes a checkout on its own. If the provider cannot yet confirm success, the obligation stays *outstanding* and the checkout is **not** marked paid — a later reconciliation (or webhook) settles it. This is the guarantee that our side never shows *paid* while the payment failed at the provider.

### Refunds — the durable refund ledger

Refunds move money too, so they get the same safety model as payments rather than a fire-and-forget call to a gateway.

A **`PaymentRefund`** is a durable, per-refund record persisted through **`IPaymentRefundStore`** in the tenant database. **`ICheckoutRefundService`** is the single authoritative entry point for issuing one — callers never talk to a gateway directly. For each request it:

1. **Resolves the settled payment** from the durable attempt ledger and computes the remaining refundable amount, so a payment can never be refunded for more than it was charged, even across several partial refunds.
2. **Derives the refunded tax from the original payment's immutable `TaxSnapshot`** through the Taxation framework's `ITaxRefundCalculator`, never by recalculating with today's rules — a full refund reuses the captured amounts and a partial refund allocates them proportionally. When Taxation is disabled the gross is still refunded.
3. **Persists the refund as `Requested` before calling the provider**, so a crash can never strand a real refund.
4. **Serializes concurrent refunds of the same payment with an `IDistributedLock`**, so two nodes can never read each other's partial state and over-refund.
5. **Reconciles the ledger against what the provider confirms**, storing the provider's authoritative refund reference and updating the status; a retried refund reuses the refund's idempotency key so the gateway never double-refunds.

A gateway opts in to executable refunds by *also* implementing the additive **`ICheckoutPaymentRefundProvider`** contract. It is intentionally separate from `ICheckoutPaymentProvider` so a provider that cannot refund (for example an offline Pay Later commitment) is never forced to change, and so `Capabilities.SupportsRefunds` becomes a real, executable promise. When the owning provider has no executable refund operation, the refund is recorded as `PendingManualReview` for an operator to settle rather than being silently dropped.

#### Reconciling a refund observed at the gateway

Refunds also flow *inbound*: a gateway may report a refund the application never requested — most commonly one issued directly from the provider dashboard. **`ICheckoutRefundReconciliationService`** is the single authoritative path for applying such a notification. A provider adapter maps its webhook into a provider-neutral `ReconcileRemoteRefundContext` (the Stripe module does this for `charge.refunded`), and the reconciliation service, under the same per-payment distributed lock the refund service uses:

1. **Correlates the remote refund to a local `PaymentRefund`** by the provider's refund reference first, then the idempotency key, then a still-open local request for the same transaction whose amount matches at currency minor-unit precision.
2. **Adopts the gateway's authoritative reference and status** onto the correlated record — the gateway is the source of truth, so a pending local record advances to the confirmed terminal state the gateway reports.
3. **Quarantines an unmatched remote refund** as `PendingManualReview` with the failure code `remote_refund_without_local_request`, so it is never lost and never silently accepted: an operator allocates its tax and attaches it to the owning order.

A record already flagged `PendingManualReview` is never regressed by a later webhook, and a duplicate notification is idempotent because it re-correlates to the same record. As everywhere in the checkout, a refund result is only recorded when the gateway confirms it.

## Distributed safety

The framework is built to run on multiple nodes:

- **`PaymentSessionCache`** relays short-lived signals (such as a webhook result) between the payment endpoints and the provider webhooks using `IDistributedCache`, coordinated by `IDistributedLock`. It is a *notification/optimization* layer only — losing an entry can slow a checkout but can never lose money, because completion always re-verifies against the durable ledger and the provider.
- **`IPaymentAttemptLimiter`** enforces a fixed-window attempt limit through the distributed cache to mitigate card-testing abuse of the anonymous payment endpoints, consistently across every instance.

## Currency-correct money

Money is compared and rounded through the provider-neutral **`Money`** and **`CurrencyScale`** helpers in `CrestApps.OrchardCore.Payments.Abstractions`:

- `CurrencyScale.GetDecimalPlaces` knows the ISO-4217 precision of each currency, so a `JPY` amount is never multiplied by 100 (which would overcharge 100×) and a `KWD` amount is settled in thousandths.
- `Money.AreEqual` / `Money.IsGreaterThan` compare amounts after normalizing to whole minor units, so binary floating-point drift (for example `19.99 + 10.00` not being exactly `29.99`) can never reject a valid payment or treat two different amounts as equal.

## Taxation

The framework never calculates tax itself. It consumes the [Taxation](taxation) framework through the **`ICheckoutTaxService`** seam:

- When the Taxation feature is **disabled**, a no-op implementation leaves the invoice untaxed and checkout keeps working.
- When the **Checkout** and **Taxation** features are **both enabled**, a taxation-aware implementation is wired in automatically (via `[RequireFeatures]`, so there is no separate integration feature to switch on). It determines the tax for the amount due now, folds any exclusive tax into the charged amount, and captures an immutable snapshot. Recurring cycles are taxed with the rules effective at billing time, each carrying its own snapshot, so historical tax is never recalculated.

An **`ICheckoutTaxProfileProvider`** resolves the merchant origin, customer destination, and classification from the flow, so tax is recomputed whenever a tax-relevant detail (such as the customer's address) changes.

## Extending checkout

To use the framework in your own module:

1. Add a reference to `CrestApps.OrchardCore.Checkout.Core` (services) and `CrestApps.OrchardCore.Checkout.Abstractions` (contracts).
2. Implement **`ICheckoutHandler`** to contribute your steps and billing items and to react to completion.
3. Create a session with `ICheckoutSessionStore.NewAsync(referenceType, referenceId, referenceVersionId)` and drive the `CheckoutFlow`.
4. To add a gateway, implement **`ICheckoutPaymentProvider`** and register it; the framework's reconciliation and ledger handle the safety guarantees for you.
5. To let a gateway refund a settled payment, also implement **`ICheckoutPaymentRefundProvider`** and issue refunds through **`ICheckoutRefundService`** — never by calling the gateway directly — so the durable refund ledger, tax allocation, and distributed over-refund protection apply.

## Related

- [Payments](payments) — the lower-level provider-agnostic payment contracts and the Stripe provider.
- [Pay Later](pay-later) — a built-in offline payment provider packaged as its own module.
- [Subscriptions](subscriptions) — a consumer of the checkout framework for recurring billing.
- [Taxation](taxation) — the tax determination framework consumed through `ICheckoutTaxService`.
