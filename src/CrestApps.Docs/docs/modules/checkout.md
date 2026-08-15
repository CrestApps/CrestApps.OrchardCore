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

- **Pay Later** (`CrestApps.OrchardCore.Checkout` → *Checkout — Pay Later* feature) records an offline commitment instead of moving money through a gateway. Because it never contacts a processor, its verification reports that it is *not* the authoritative source of a charged amount, so the checkout records the commitment on the strength of a recorded transaction id alone — without an amount cross-check — while still flowing through the exact same durable ledger and reconciliation as a real gateway. This keeps the safety guarantees intact and never fabricates a *paid* record a processor could contradict.

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
- When Taxation is **enabled**, a taxation-aware implementation determines the tax for the amount due now, folds any exclusive tax into the charged amount, and captures an immutable snapshot. Recurring cycles are taxed with the rules effective at billing time, each carrying its own snapshot, so historical tax is never recalculated.

An **`ICheckoutTaxProfileProvider`** resolves the merchant origin, customer destination, and classification from the flow, so tax is recomputed whenever a tax-relevant detail (such as the customer's address) changes.

## Extending checkout

To use the framework in your own module:

1. Add a reference to `CrestApps.OrchardCore.Checkout.Core` (services) and `CrestApps.OrchardCore.Checkout.Abstractions` (contracts).
2. Implement **`ICheckoutHandler`** to contribute your steps and billing items and to react to completion.
3. Create a session with `ICheckoutSessionStore.NewAsync(referenceType, referenceId, referenceVersionId)` and drive the `CheckoutFlow`.
4. To add a gateway, implement **`ICheckoutPaymentProvider`** and register it; the framework's reconciliation and ledger handle the safety guarantees for you.

## Related

- [Payments](payments) — the lower-level provider-agnostic payment contracts and the Stripe provider.
- [Subscriptions](subscriptions) — a consumer of the checkout framework for recurring billing.
- [Taxation](taxation) — the tax determination framework consumed through `ICheckoutTaxService`.
