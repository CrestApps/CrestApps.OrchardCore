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

The **Checkout** feature provides a provider-agnostic checkout and payment framework. It is the reusable foundation that any purchase flow builds on — recurring [Subscriptions](subscriptions) as well as one-time goods purchases — so the steps, the invoice, taxation, and the money-handling safety guarantees are written once and shared.

It deliberately does **not** implement a storefront. It defines the contracts and the durable, distributed-safe machinery for collecting money; a consuming module (such as Subscriptions) contributes the domain-specific steps and decides what a completed checkout means.

:::warning Current status
The framework now ships a working public checkout: a customer can walk the steps, choose a payment method, pay, and land on a confirmation. Settling an outstanding [transaction](transactions) online goes through it end to end.

Not yet migrated: [Subscriptions](subscriptions) still runs its own payment pipeline rather than this one, so a subscription signup does not currently settle through the engine. Moving it across is the next planned milestone.
:::

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

A **`CheckoutFlow`** provides step navigation (first/next/previous/current) over the session's ordered **`CheckoutFlowStep`** list. Features contribute steps and their **billing items** while a session is being activated by implementing **`ICheckoutHandler`** (or deriving from `CheckoutHandlerBase`). The handler lifecycle is `Activating` → `Activated` when the checkout is created, then `Initializing` → `Initialized` → `Loading` → `Loaded` every time it is loaded again, and finally `Completing` → `Completed`, with `Failed` on error.

The load hooks matter because some of what a step carries is true only for one visitor at one moment and so cannot be persisted: whether the account step applies depends on whether *this* request is signed in, and a visitor can sign in on another tab midway through. Deciding that once, when the session was created, would show a signed-in customer a registration step they must not fill in.

### Checkout invoice

A single **`CheckoutInvoice`** is built for the whole checkout so the customer is charged exactly once regardless of how many steps contributed billing items. It records the one-time amount due now, the first recurring amount charged now, the recurring subtotals grouped by billing interval, and the tax determined for the amount due now (with an immutable `TaxSnapshot`).

Two rules about how it is built are worth stating plainly, because getting either wrong produces an invoice that looks fine and charges the wrong amount:

- **Every step is billed, including the ones that are never drawn.** A step is concealed because there is nothing to ask the customer, not because there is nothing to charge — the plan a subscriber already chose is exactly such a step. Building the invoice only from visible steps drops those charges and completes the checkout for nothing.
- **The currency comes from what is being bought**, falling back to the site setting only when the checkout names none. An invoice built in the site's currency from line items priced in another charges a number belonging to a different currency.

The invoice is **rebuilt** rather than patched whenever something that changes the price changes, such as applying or removing a coupon. A total assembled by adjusting a previous total drifts away from the line items the customer is reading.

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

### Who owns a guest checkout

A guest has no account, so something else has to prove that the browser resuming a pending checkout is the one that started it. An IP address and a user agent cannot: everyone behind one office router or mobile carrier shares an address, a user agent is neither secret nor unique, and both are supplied by the caller.

A checkout started by a guest is therefore issued a 32-byte random **ownership token**, delivered in a data-protected, HTTP-only cookie. Only its SHA-256 hash is stored on the session, and the comparison is fixed-time, so a leaked database does not hand an attacker the ability to resume live checkouts. The IP address and user agent are still recorded, but only as audit fields. The same token protects every checkout, including one that a guest later signs in to finish.

## Currency-correct money

Money is compared and rounded through the provider-neutral **`Money`** and **`CurrencyScale`** helpers in `CrestApps.OrchardCore.Payments.Abstractions`:

- `CurrencyScale.GetDecimalPlaces` knows the ISO-4217 precision of each currency, so a `JPY` amount is never multiplied by 100 (which would overcharge 100×) and a `KWD` amount is settled in thousandths.
- `Money.AreEqual` / `Money.IsGreaterThan` compare amounts after normalizing to whole minor units, so binary floating-point drift (for example `19.99 + 10.00` not being exactly `29.99`) can never reject a valid payment or treat two different amounts as equal.

## What happens when fulfillment fails

Every obligation is confirmed with its provider *before* anything is fulfilled, and that confirmation is committed on its own. Only then do the completing handlers run — creating the agreement, recording the debt, queuing the site build — inside one transaction with the status change to `Completed`.

If a completing handler throws, that transaction is discarded whole, so a half-fulfilled purchase is never committed. The checkout is deliberately **not** marked failed and the customer is **not** refunded: they paid, the confirmation is durable, and a fulfillment hiccup is better fixed by trying again than by returning the money. The reconciliation sweep finds the checkout by its `PaymentPending` status and retries completion; the error is logged each time so an operator sees a purchase that keeps failing.

## Abandoned checkouts

A checkout nobody finishes still holds whatever the provider set aside for it. After **Session lifetime** (Checkout settings, default 24 hours) the sweep calls `ICheckoutEngine.ExpireAsync`, which cancels the pending attempts at the provider and marks the checkout `Expired`. A checkout that has already collected money is never expired — it is an unfulfilled purchase, which the sweep finishes instead.

## Discounts and coupons


Anything that reduces what a customer pays goes through **`ICheckoutDiscountService`**, and it runs **before** tax. Taxing the full price and then discounting the total charges the customer tax on money they never paid, which is wrong for them and wrong on the return the site owner files.

A provider implementing **`ICheckoutDiscountProvider`** decides only *what* to take off. The checkout decides *how*, so three rules hold no matter how many providers a site installs:

- a total never goes below zero, because a coupon worth more than the basket makes the purchase free rather than a payment the site owes the customer;
- the one-time amount and the first recurring cycle are reduced separately, so a setup-fee coupon never eats a monthly charge and "first month half price" never halves every month after;
- what is recorded on the invoice is what was actually taken off, so a receipt cannot show a discount larger than the price it applied to.

The **coupon catalog** ships in the box. Manage codes under **Commerce → Coupons**: percentage or fixed amount, targeting the one-time amount or the first cycle, with an optional validity window, minimum amount, and usage limit. A fixed-amount coupon only applies to an invoice in its own currency, because ten dollars off is not ten euros off.

Entering a code and pressing **Apply** saves it and rebuilds the invoice immediately, so the customer sees what the code actually took off *before* they agree to pay. A discount a customer cannot see until after the charge is a discount they cannot check.

A first-cycle coupon reduces only what is due for the first cycle. The recurring price the gateway is told, and the amount later cycles are invoiced for, is always the plan's full cycle amount; at Stripe the reduction is expressed as a single-use coupon on the agreement.

A code is consumed when the purchase **completes**, not when it is applied. That is what makes a usage limit mean something: a customer who applies a single-use code and then abandons the checkout does not burn it, so a code that leaks cannot be exhausted by people who never bought anything. Redemption takes a lock on the coupon, so two checkouts finishing at the same instant cannot both take the last one.

:::warning
A coupon with neither a usage limit nor an end date will keep discounting until somebody notices. The coupon list warns when one exists.
:::

## Taxation

The framework never calculates tax itself. It consumes the [Taxation](taxation) framework through the **`ICheckoutTaxService`** seam:

- When the Taxation feature is **disabled**, a no-op implementation leaves the invoice untaxed and checkout keeps working.
- When the **Checkout** and **Taxation** features are **both enabled**, a taxation-aware implementation is wired in automatically (via `[RequireFeatures]`, so there is no separate integration feature to switch on). It determines the tax for the amount due now, folds any exclusive tax into the charged amount, and captures an immutable snapshot. Recurring cycles are taxed with the rules effective at billing time, each carrying its own snapshot, so historical tax is never recalculated.

An **`ICheckoutTaxProfileProvider`** resolves the merchant origin, customer destination, and classification from the flow, so tax is recomputed whenever a tax-relevant detail (such as the customer's address) changes.

## The checkout engine

**`ICheckoutEngine`** is the single entry point for every money-moving step. Controllers, endpoints, webhooks, and background tasks all go through it and never call a payment provider, the attempt ledger, or the reconciliation service directly. Centralizing the orchestration is what makes the guarantees above real rather than conventions each caller has to remember.

| Member | What it does |
| --- | --- |
| `StartAsync` | Creates the session, runs the handlers so features contribute their steps and billing items, builds the invoice, and persists it. |
| `BeginPaymentAsync` | Creates one durable attempt **per obligation**, persists each before contacting the provider, stores the provider's reference the moment it returns, and hands back what the client needs to finish (a client secret, a redirect, or nothing). |
| `TryCompleteAsync` | Verifies every outstanding obligation against the provider's own API and, only when all are confirmed, runs the completion handlers and marks the session complete. |
| `CancelAsync` | Releases the remote resources of a checkout the customer abandoned. |

### Completion never blocks a request

`TryCompleteAsync` returns immediately with one of `Completed`, `AlreadyCompleted`, `Pending`, `Blocked`, `Failed`, or `NotFound`. It never waits in a loop for a provider to make up its mind, because that would tie up a web worker per customer and still time out behind a proxy.

Instead the same call is driven from three independent directions, and the session lock plus the status transition make it idempotent no matter which arrives first:

- the customer's browser polls it while the provider settles;
- a provider webhook triggers it when the gateway confirms;
- the reconciliation background task calls it for any session with a stale pending attempt.

That last one is the recovery path that matters most: a customer who pays and then closes the browser still gets their purchase fulfilled, because the sweep completes the checkout server-side rather than merely noting that the attempt succeeded.

### Recurring obligations

An invoice with a recurring line does not become a single charge. `BeginPaymentAsync` groups the recurring lines by billing interval, makes one obligation of each, and routes it to the provider's **recurring** capability rather than its one-time one. Sending it down the one-time path would take a single payment and then silently never bill the customer again.

A provider offers that capability by implementing **`ICheckoutRecurringPaymentProvider`** alongside `ICheckoutPaymentProvider`, under the same provider key. Declaring `SupportsRecurringPayments` without shipping the implementation is refused before any money moves, so a wiring mistake cannot turn a subscription into a one-off charge.

A gateway usually cannot create a recurring agreement without a reusable payment method, and only the browser can produce one without the card details reaching this application. So a provider's client script may implement a `prepare` step whose result is handed back to that provider on the server as opaque provider data. The Stripe panel uses it to tokenize the card before the agreement is created, and then confirms the first invoice against that very payment method rather than attaching a second one.

That opaque provider data reaches the **one-time** obligation as well, which matters as soon as a checkout
has both — a plan sold with a setup fee is exactly that shape. The browser tokenizes one card for the whole
checkout and confirms every obligation with it, so the two obligations must be billed to one customer at the
gateway: Stripe attaches the payment method to the customer the agreement is created against, and then
refuses to confirm a payment intent that names a different customer, or none. The Stripe provider therefore
resolves one customer per checkout, keyed by the checkout rather than by the attempt, so whichever
obligation is begun first creates it and the other reuses it — and a retry in a fresh scope finds the same
one instead of leaving a duplicate behind.

A checkout with nothing recurring in it tokenizes no reusable card and creates no customer, exactly as
before.

### Compensation

When one obligation fails at the provider, the engine refunds the obligations that already settled — through `ICheckoutRefundService`, never straight to the gateway — so the customer is not left paying for half a purchase, and the gateway's own refund notification correlates to a local record instead of being quarantined for an operator.

## The public checkout

Enabling the feature adds a customer-facing checkout at `/Checkout/{sessionId}/{step}`:

- Steps are rendered by display drivers against `CheckoutFlow`, so a feature contributes a step without touching the page. Derive from **`CheckoutFlowDisplayDriver`** and it renders only while its own step is current.
- The shared frame (progress stepper, invoice summary, navigation) is a separate driver, so a step never has to re-render its surroundings. Override the `CheckoutFlow` shape template in a theme to restyle the whole checkout.
- The payment step lists the payment providers **actually registered on the tenant** and filters out any that cannot settle what the invoice owes, so the page can never offer a method the framework cannot execute.
- Each provider renders its own panel through an `IDisplayDriver<CheckoutFlowPaymentMethod>` grouped by its provider key. Only the selected panel is shown.
- Two JSON endpoints (`checkout/{sessionId}/payment/begin` and `.../status`) carry the begin-then-poll conversation. Both are rate limited per visitor per checkout, resolve the session through the ownership-checked lookup, and require a same-origin request as defense in depth.

A customer who owes nothing (a free plan, or a balance already covered) completes without being asked for a payment method at all.

## Extending checkout

To use the framework in your own module:

1. Add a reference to `CrestApps.OrchardCore.Checkout.Core` (services) and `CrestApps.OrchardCore.Checkout.Abstractions` (contracts).
2. Implement **`ICheckoutHandler`** to contribute your steps and billing items and to react to completion.
3. Start a checkout with `ICheckoutEngine.StartAsync(...)` and redirect the customer to the `CheckoutStep` route. Do not drive payment yourself.
4. Render your step by deriving from **`CheckoutFlowDisplayDriver`** and registering it as an `IDisplayDriver<CheckoutFlow>`.
5. To add a gateway, implement **`ICheckoutPaymentProvider`**, register it, and add an `IDisplayDriver<CheckoutFlowPaymentMethod>` for its panel (grouped with `.OnGroup(yourProviderKey)`); the framework's reconciliation and ledger handle the safety guarantees for you.
6. To let a gateway refund a settled payment, also implement **`ICheckoutPaymentRefundProvider`** and issue refunds through **`ICheckoutRefundService`** — never by calling the gateway directly — so the durable refund ledger, tax allocation, and distributed over-refund protection apply.
7. To let a gateway bill on a cycle, also implement **`ICheckoutRecurringPaymentProvider`** under the same provider key. Implementations must be idempotent on the attempt's idempotency key, so a retried begin resumes the same agreement instead of creating a second one and billing the customer twice.

## Related

- [Payments](payments) — the lower-level provider-agnostic payment contracts and the Stripe provider.
- [Pay Later](pay-later) — a built-in offline payment provider packaged as its own module.
- [Subscriptions](subscriptions) — a consumer of the checkout framework for recurring billing.
- [Taxation](taxation) — the tax determination framework consumed through `ICheckoutTaxService`.
