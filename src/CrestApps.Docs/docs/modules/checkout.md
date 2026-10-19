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

For access and erasure requests, `ICheckoutSessionStore.GetByCustomerAsync(ownerId, email, skip, take)` returns the sessions owned by a user or whose buyer gave an email (`CheckoutContactInfo.Email`, matched without case sensitivity), oldest first, and `GetManyAsync(sessionIds)` returns sessions by id. Neither enforces ownership or runs the checkout handlers, so their sessions must never resume a customer-facing flow. They read the `CheckoutSessionCustomerIndex` (session id, owner, normalized email and creation time), created by `CheckoutSessionCustomerIndexMigrations`. A session with neither an owner nor an email is not indexed. When the table is created, the sessions saved before it are saved once more in batches of 100 after the migration commits, so they are indexed too. See [Personal data requests](commerce#personal-data-requests).

`ICheckoutSessionStore.ListByReferenceAsync(referenceType, referenceId, skip, take)` returns every session started for a reference, oldest first, including the earlier attempts that `GetByReferenceAsync` (the newest only) does not reach. Like the lookups above, it does not enforce ownership or run the checkout handlers.

`CheckoutSession.Anonymize(now, replacementName)` removes who a session was for: the owner (`OwnerId`), the buyer's `CheckoutContactInfo` (the name becomes `replacementName` or is cleared, and the billing address keeps only its country and region), the IP address, the user agent and the saved steps. The amounts, invoice, status and dates stay, and `AnonymizedUtc` records when it happened. The modification time is left alone, so the abandoned-checkout sweep still expires a pending session, which no one can resume once its owner is gone. **`ICheckoutSessionAnonymizer`** applies it to stored sessions: `AnonymizeAsync(sessionId)` and `AnonymizeByReferenceAsync(referenceType, referenceId)`, which anonymizes every attempt for the reference and skips sessions already anonymized. The Customers feature calls it for the orders it anonymizes; a module or application with its own checkout source calls it with its own reference type when it erases a person, for example `TransactionsConstants.ReferenceTypes.Transaction` for a charge paid through a transaction checkout.

#### The canonical order reference

The reference triple is intentionally generic so any consumer can drive a checkout, but ecommerce orders use one stable relationship, published as the well-known constants in **`CheckoutReferenceTypes`**:

- `ReferenceType` is `CheckoutReferenceTypes.Order` (`"Order"`);
- `ReferenceId` is the owning order's stable item id;
- `ReferenceVersionId` identifies the draft or quote version only when the order requires versioning, and is otherwise left empty.

The order owns the reverse link by storing its checkout session id, and `ICheckoutSessionStore.GetByReferenceAsync(referenceType, referenceId, referenceVersionId)` resolves the most recent session for a reference when only the order is known. Payment attempts and refunds remain owned by Checkout and correlate through the session and provider transaction references. Because payment stays authoritative in the durable attempt ledger, **an order must never be marked paid from a session status alone**.

The [Orders](carts-and-orders) feature is the consumer of this reference: it checks a cart out as an order and marks the order paid in its `CompletingAsync` handler. Its billing items carry a quantity and the product's tax details; see [Checkout line details](carts-and-orders#checkout-line-details).

### Checkout flow and steps

A **`CheckoutFlow`** provides step navigation (first/next/previous/current) over the session's ordered **`CheckoutFlowStep`** list. Features contribute steps and their **billing items** while a session is being activated by implementing **`ICheckoutHandler`** (or deriving from `CheckoutHandlerBase`). The handler lifecycle is `Activating` → `Activated` when the checkout is created, then `Initializing` → `Initialized` → `Loading` → `Loaded` every time it is loaded again, and finally `Completing` → `Completed`, with `Failed` when the payment is declined. A checkout that ends unpaid also raises `Canceled` (with the caller's reason) when `ICheckoutEngine.CancelAsync` cancels it and `Expired` when the reconciliation sweep expires it; both run after the new status is committed, inside the session lock, and their writes are committed with it.

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

Capabilities let the framework select a suitable provider and enforce constraints — for example, refusing to add a separate up-front fee to a provider-hosted page that cannot represent one. A provider that records a promise to pay instead of collecting money sets `DefersPayment`. Such a provider is neither offered nor accepted on a checkout that pays off an existing balance (`CheckoutSession.SettlesExistingBalance`), because it would mark that balance paid with nothing collected. `CheckoutPaymentMethodSelector.GetEligible` applies the rule for both the payment step and the engine.

### Built-in payment providers

- **Pay Later** — provided by the standalone **[Pay Later](pay-later)** module (`CrestApps.OrchardCore.PayLater`). It records an offline commitment instead of moving money through a gateway. Because it never contacts a processor, its verification reports that it is *not* the authoritative source of a charged amount, so the checkout records the commitment on the strength of a recorded transaction id alone — without an amount cross-check — while still flowing through the exact same durable ledger and reconciliation as a real gateway. This keeps the safety guarantees intact and never fabricates a *paid* record a processor could contradict.
- **Stripe** — provided by the **[Stripe](payments#stripe-as-a-generic-checkout-provider)** module. When both the Stripe and Checkout features are enabled, Stripe registers a generic `ICheckoutPaymentProvider` (and `ICheckoutPaymentRefundProvider`) so *any* checkout — subscriptions today, a storefront tomorrow — can collect and refund a card payment through a Stripe PaymentIntent without depending on the subscription-specific endpoints. It verifies against Stripe's authoritative API and converts every amount through `StripeCurrency`.
- **Test payments** — the **Test payments** feature (`CrestApps.OrchardCore.Checkout.TestProvider`) in this module. It confirms every payment without moving money, for development and automated tests. See [Test payments](#test-payments).

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

<YouTube video="commerce" title="Narrated video of creating a coupon" start={130} end={161} />


Anything that reduces what a customer pays goes through **`ICheckoutDiscountService`**, and it runs **before** tax. Taxing the full price and then discounting the total charges the customer tax on money they never paid, which is wrong for them and wrong on the return the site owner files.

A provider implementing **`ICheckoutDiscountProvider`** decides only *what* to take off. The checkout decides *how*, so three rules hold no matter how many providers a site installs:

- a total never goes below zero, because a coupon worth more than the basket makes the purchase free rather than a payment the site owes the customer;
- the one-time amount and the first recurring cycle are reduced separately, so a setup-fee coupon never eats a monthly charge and "first month half price" never halves every month after;
- what is recorded on the invoice is what was actually taken off, so a receipt cannot show a discount larger than the price it applied to.

Within its bucket a discount takes off only what it can discount (`CheckoutDiscountBase`): a one-time discount, the goods bought now but never a shipping line; a first-cycle discount, the recurring charges collected now. What it took off is spread across those lines in proportion to each line's total, into `CheckoutLineItem.DiscountAmount`, and the last line takes what rounding leaves, so the shares add up to the discount exactly. `CheckoutTaxContextFactory` passes each share to tax as `TaxableItem.DiscountAmount`, so a line is taxed on what the buyer pays for it, and shipping, which no coupon touches, is taxed in full. A coupon's percentage and its minimum amount count the same lines: a 10% coupon on a $50 item with $15 shipping takes $5 off, and with a 10% tax charges $4.50 on the item and $1.50 on shipping.

The **coupon catalog** ships in the box. Manage codes under **Commerce → Coupons**: percentage, fixed amount or free shipping, targeting the one-time amount or the first cycle, with an optional validity window, minimum and maximum amount, usage limit and limit per customer. A fixed-amount coupon only applies to an invoice in its own currency, because ten dollars off is not ten euros off.

A **free-shipping** coupon (`CouponKind.FreeShipping`) contributes a discount aimed at `DiscountTarget.Shipping`: it takes off the shipping lines charged now, and nothing else, while its minimum and maximum count the items. A coupon can be **limited to some products or categories** (`Coupon.ProductIds`, `Coupon.CategoryIds`, taxonomy term ids): only the lines it applies to count toward its amounts, and its `DiscountLine.LineItemIds` keeps the share off the other lines. A line knows its product and categories through `BillingItem.ProductId` and `BillingItem.CategoryIds`; the [Orders](carts-and-orders) feature fills them in and adds the product and category pickers to the coupon editor (through `IDisplayManager<Coupon>`, so other modules can add editors too).

Entering a code and pressing **Apply** saves it and rebuilds the invoice immediately, so the customer sees what the code actually took off *before* they agree to pay. A discount a customer cannot see until after the charge is a discount they cannot check.

A first-cycle coupon reduces only what is due for the first cycle. The recurring price the gateway is told, and the amount later cycles are invoiced for, is always the plan's full cycle amount; at Stripe the reduction is expressed as a single-use coupon on the agreement.

A code is consumed when the purchase **completes**, not when it is applied. That is what makes a usage limit mean something: a customer who applies a single-use code and then abandons the checkout does not burn it, so a code that leaks cannot be exhausted by people who never bought anything.

### Holding a use while the buyer pays

Between applying a code and completing, a use is **held**, like stock. `ICheckoutPaymentGate` implementations run inside the checkout's lock just before a payment begins (or, for a checkout with nothing to pay, before it completes). `CouponHoldPaymentGate` asks `ICouponRedemptionService.HoldAsync` to hold a use of each coupon on the invoice; when one can't be held, it takes the code off, rebuilds the invoice and refuses the payment with `InvoiceChanged`, and the payment script loads the page again with the new total and *The code LAST1 ran out before you paid, so it was taken off.* A buyer is never charged for a use that's gone.

Each use is a `CouponRedemption` in the coupon collection (`CouponRedemptionIndex`): **Held** with an expiry as long as the checkout can live (`CheckoutSettings.SessionLifetimeHours`), **Redeemed** when the checkout completes, **Released** when it is canceled, fails or expires, and **Returned** when its order is canceled (`ReturnAsync`, called by the Orders feature). A coupon has used `RedemptionCount`, plus uses recorded while its lock couldn't be taken, plus the live holds of other checkouts. Every change is made under the coupon's lock and committed before the lock is let go, so the second of two buyers racing for the last use is told it's gone. A limit per customer counts the customer's redeemed and held uses by account or by email, so a buyer who checks out as a guest with the same email is the same customer. The coupon's edit page lists its recent uses and how many are held.

Applying codes is throttled per address (`IPaymentAttemptLimiter`, scope `coupon-code`), since short codes can be guessed.

### Several codes and combining rules

A checkout carries up to `CheckoutCoupon.MaxCodes` (5) codes in `CheckoutCoupon.Codes` (a session saved before that used `Code`, which `GetCodes()` still reads). `CouponCheckoutFlowDisplayDriver` adds a code on **Apply** and takes one off for the form action `remove-coupon:{code}` (`CheckoutConstants.FormActions.RemoveCouponPrefix`).

Every `DiscountLine` has a kind, `DiscountClass` (`Product`, `Order` or `Shipping`; when a provider leaves `Class` null it's worked out from `Target` and `LineItemIds`), and `CombinesWith`, the `DiscountCombination` flags of the kinds it can be used with. A provider that says nothing combines with everything, as before. `DefaultCheckoutDiscountService.Choose` keeps every discount when each pair can be combined (each allows the other's kind); otherwise it searches the subsets of up to 12 candidates for the compatible set that takes off the most (ties keep the earlier set), and past that takes the largest first. What's left goes to `CheckoutInvoice.SkippedDiscounts`, each with `GaveWayTo`, the description of a kept discount it can't be combined with. A coupon's kind is `Coupon.DiscountClass` (free shipping is `Shipping`; one limited to products, and every buy X get Y offer, is `Product`; else `Order`), and its `CombinesWithProductDiscounts`, `CombinesWithOrderDiscounts` and `CombinesWithShippingDiscounts` make `Coupon.CombinesWith`.

### Automatic discounts

`Coupon.Method` is `CouponMethod.Code` or `CouponMethod.Automatic`. `CouponIndex.IsAutomatic` (migration version 3) lets `ICouponStore.ListAutomaticAsync` find the enabled automatic coupons, which `CouponDiscountProvider` offers on every invoice when they're redeemable, in scope, not in `CheckoutCoupon.Declined`, and `ICouponRedemptionService.GetAvailabilityAsync` says they're available to this buyer. Their lines have `DiscountLine.IsAutomatic`, keep the coupon's code (holds, uses and limits work by code as for any coupon), and show the description; `OrderCheckoutHandler` records them on the order with no code. When `CouponHoldPaymentGate` can't hold one, it adds its code to `CheckoutCoupon.Declined`, so rebuilding the invoice doesn't put it back. An automatic coupon's code entered by hand is refused (`Coupon.CanBeTyped`).

### Buy X get Y and minimum quantities

A `CouponKind.BuyXGetY` coupon has `BuyQuantity`, `GetQuantity`, `Percentage` (off each item got), `MaxApplicationsPerOrder`, and `BuyProductIds`/`BuyCategoryIds` for what counts as bought; `ProductIds`/`CategoryIds` choose what's got. `Coupon.GetBuyXGetYDiscount` expands the one-time lines into units, and each time the offer applies takes the cheapest units that can be got and the dearest remaining units that can be bought; a unit is used once. The line carries `DiscountLine.LineShares`, what it takes off each line, and `Allocate` spreads the discount by those shares rather than by line totals, so tax sees the discount on the free item's line. `Coupon.MinimumQuantity` counts the units of the lines a coupon counts (the bought lines for buy X get Y).

### Discount links and generated codes

`GET discount/{code}` (`DiscountLinkController`) stores a redeemable code a buyer can type in the `orchard-discount-code` cookie (`CheckoutConstants.DiscountLinkCookieName`, HTTP-only, the tenant's path, seven days) and redirects to `redirect` when it's a local path, with the tenant's path base added when it's missing, or to the home page. `DiscountLinkCheckoutHandler.ActivatingAsync` adds the cookie's code to each new checkout's `CheckoutCoupon`, except one that settles an existing balance.

`ICouponStore.GenerateCodesAsync` makes `CouponCode` documents in the coupon collection (`CouponCodeIndex`, migration version 4): a random eight-character code from an alphabet without 0, O, 1 or I after an optional prefix, its own `MaxRedemptions` and `RedemptionCount`, and the coupon's id. Codes are drawn again when they clash with each other or with codes in use. `CouponStore.GetByCodeAsync` falls back to the generated codes and returns the coupon with the code's code, uses and limit and `Coupon.GeneratedCodeId` set; the store's `ICatalog<Coupon>.UpdateAsync` writes that coupon's `RedemptionCount` to the `CouponCode`, never over the coupon. So holds, limits, redemptions and returns work per code without changes. `Coupon.OnlyGeneratedCodes` turns the coupon's own code off (`Coupon.CanBeTyped`). The coupons list reads only `Coupon` documents, so thousands of codes don't slow it. `CouponsAdminController.ExportCodes` downloads them as CSV; deleting a coupon deletes its codes (`DeleteGeneratedCodesAsync`).

### Coupons in recipes and deployments

The **`Coupons`** recipe step adds or updates coupons by code; a deployment plan's **Coupons** step exports them. How often a coupon was used belongs to the site, so neither carries `RedemptionCount`.

```json
{
  "name": "Coupons",
  "Coupons": [
    { "Code": "SPRING10", "Description": "Spring sale", "Kind": "Percentage", "Percentage": 10, "MaxRedemptions": 500, "MaxRedemptionsPerCustomer": 1, "EndsUtc": "2027-06-01T00:00:00Z", "IsEnabled": true },
    { "Code": "SHIPFREE", "Kind": "FreeShipping", "MinimumAmount": 75, "IsEnabled": true }
  ]
}
```

### Coupons on a checkout that settles an existing balance

A checkout that pays a balance that already exists (`CheckoutSession.SettlesExistingBalance`, set by the [Transactions](transactions) module for a checkout with `ReferenceType = "Transaction"`) has no price of its own to reduce. A discount there is only safe when the module that settles the balance also takes it off the balance, so the session carries a second flag, `CheckoutSession.AcceptsDiscountsOnBalance`:

- When it is `false`, `CouponCheckoutFlowDisplayDriver` renders no promotion code box and ignores a posted code, and `DefaultCheckoutDiscountService` applies no discounts (and clears any recorded earlier), so `InitialPaymentAmount` and `DueNow` are untouched and nothing is redeemed.
- When it is `true`, discounts apply as on any checkout. `CouponRedemptionCheckoutHandler` still redeems nothing for a session that settles an existing balance: the settling module redeems the codes itself, once the balance is settled.

The Transactions module sets both flags. On completion, `TransactionSettlementCheckoutHandler` records each one-time discount line the invoice applied as a `Discount` adjustment of exactly that amount (`ITransactionCommandService.AdjustAsync`, never more than is still owed), then applies the confirmed payment. A 100% code settles the transaction with no payment attempt; a partial code plus the payment settles it in full. Only when the transaction ends with nothing owed does it call each `ICheckoutDiscountProvider.RedeemAsync`, so a code is never used up by a checkout that left the balance owing. A partial code with no confirmed payment records nothing. See [Transaction commands](transactions.md#transaction-commands).

### Limiting a coupon to a source, a reference type or one customer

A `Coupon` can be limited (`Coupon.IsScoped`) by:

| Property | Matched against |
| --- | --- |
| `Sources` | `CheckoutDiscountScope.Source`, for example the source of the transaction a checkout settles. Case-insensitive. |
| `ReferenceTypes` | `CheckoutSession.ReferenceType`, or `CheckoutDiscountScope.ReferenceType` (what the transaction is for). Case-insensitive. |
| `CustomerId` | `CheckoutSession.OwnerId`, or `CheckoutDiscountScope.CustomerId` (the transaction's owner, a user id or a guest customer id). |
| `CustomerEmail` | `CheckoutDiscountScope.CustomerEmail`, the `CheckoutContactInfo.Email` the checkout collected, or the email of the session owner's account through `ICustomerContactResolver`. Case-insensitive. |

`CheckoutDiscountScope` is stored in the session's property bag by the module that knows what the checkout pays for; `TransactionSettlementCheckoutHandler` stores the transaction's `Source`, `ReferenceType`, `OwnerId` and guest email. Every limit set must match, and a scoped coupon is refused on a checkout that does not say what the limit needs (fail closed). `CouponScopeEvaluator` holds the rule, and both places a code is checked use it: `CouponCheckoutFlowDisplayDriver` refuses the code when it is entered (*That code can't be used for this payment.*), and `CouponDiscountProvider` stops applying it when the invoice is rebuilt. The coupon editor sets the limits under **Who and what it is for**; see [Coupons](../user-manual/commerce/coupons.md#limit-who-and-what-a-coupon-is-for).

:::warning
A coupon with neither a usage limit nor an end date will keep discounting until somebody notices. The coupon list warns when one exists.
:::

## Taxation

The framework never calculates tax itself. It consumes the [Taxation](taxation) framework through the **`ICheckoutTaxService`** seam:

- When the Taxation feature is **disabled**, a no-op implementation leaves the invoice untaxed and checkout keeps working.
- When the **Checkout** and **Taxation** features are **both enabled**, a taxation-aware implementation is wired in automatically (via `[RequireFeatures]`, so there is no separate integration feature to switch on). It determines the tax for the amount due now, folds any exclusive tax into the charged amount, and captures an immutable snapshot. Recurring cycles are taxed with the rules effective at billing time, each carrying its own snapshot, so historical tax is never recalculated.

An **`ICheckoutTaxProfileProvider`** resolves the merchant origin, customer destination, and classification from the flow, so tax is recomputed whenever a tax-relevant detail (such as the customer's address) changes. The default provider's destination is the address the order ships to (`CheckoutShippingInfo`, set by [Shipping](shipping)), else the buyer's billing address (`CheckoutContactInfo`); with neither, only rules without a jurisdiction apply.

The default provider then runs every **`ICheckoutTaxProfileHandler`**, at checkout and when a recurring charge is taxed, so modules add what they know without replacing it:

- `TaxOriginCheckoutTaxProfileHandler` (with Taxation) sets `CheckoutTaxProfile.Origin` from the store's address under **Commerce > Taxation > Origin** (`TaxOriginSettings`), for taxes sourced where the seller is.
- `CustomerCheckoutTaxProfileHandler` (with [Customers](customers)) sets `CheckoutTaxProfile.Customer` for a signed-in buyer: business (`B2B`) or person, their tax id as the registration and VAT number, `IsTaxExempt`, and the ids of the exemption certificates on file for them. A rule the customer is exempt from charges nothing (`TaxTreatment.Exempt`). A guest is taxed as a person.

A billing item whose tax was already decided sets **`BillingItem.ExcludeFromTax`** and is left out of the
determination. Settling an outstanding [transaction](transactions) does this: the transaction already carries its
own tax, and paying it is not a new sale.

## Balances that pay after tax

A **tender** is part of a checkout's total paid from a balance the buyer holds rather than charged by a payment provider: a [gift card](carts-and-orders#gift-cards), [store credit](customers#store-credit) or [loyalty points](customers#loyalty-points). It is not a discount. Tax is worked out on the full price first, and a tender then pays toward the total, tax included, like a second way to pay. The seam ships with the Checkout feature and does nothing until a feature registers an `ICheckoutTenderProvider`.

| Type | Role |
| --- | --- |
| `ICheckoutTenderProvider` | One kind of balance. Says which of its balances the buyer chose and what's on them, and moves money on and off them. |
| `ICheckoutTenderService` (`DefaultCheckoutTenderService`) | Decides how much each chosen balance pays, and takes it off what the payment provider charges. |
| `ICheckoutTenderLedger` (`CheckoutTenderLedger`) | Holds what each balance pays while the buyer pays the rest, takes it off when the checkout completes, lets it go when it doesn't, and puts it back for a refund. |
| `TenderHoldPaymentGate` | An `ICheckoutPaymentGate` that holds the balances before a payment begins. |
| `TenderCheckoutHandler` | A checkout handler that redeems the holds while completing and releases them on cancel, failure and expiry. |

### Providers

| `ICheckoutTenderProvider` member | Responsibility |
| --- | --- |
| `Key` | The stable key recorded on every `TenderLine` it gives, such as `gift-card`, `store-credit` or `loyalty-points`. |
| `GetTendersAsync(CheckoutTenderContext)` | Returns a `TenderLine` for each of its balances in `context.References` that can pay this checkout, with `Amount` set to everything on it. A balance in another currency, disabled or expired is left out. `CheckoutTenderContext` carries the `Invoice` (tax already applied), the `Flow` and the `References` the buyer chose, in order. |
| `GetBalanceAsync(reference, currency)` | What's on a balance now, or `null` when it can't pay at all. The ledger asks it under the balance's lock. |
| `DebitAsync(reference, amount, currency, flow)` | Takes the amount off, once per checkout, under the balance's lock. |
| `CreditAsync(reference, amount, currency, reason)` | Puts an amount back, such as for a refund, under the balance's lock. |

A `TenderLine` has `ProviderKey`, `Reference` (which balance pays, such as the gift card's item id, never a secret the buyer typed), `Description` (what the buyer sees, such as *Gift card ending 4F2K*), `Amount` (tax included) and `TaxAmount` (the part of `Amount` that is tax).

The balances a buyer chose are stored on the session as a `CheckoutTenderSelection`: `Choices`, a list of `CheckoutTenderChoice` (`ProviderKey`, `Reference`) in the order they were chosen, with `Add`, `Remove` and `For(providerKey)`. The selection is what the buyer asked for; `CheckoutInvoice.Tenders` is what the balances actually pay, worked out again every time the invoice is rebuilt.

### Applying them to the invoice

`CheckoutInvoiceBuilder` applies discounts, then tax, then `ICheckoutTenderService.ApplyTendersAsync`. `DefaultCheckoutTenderService`:

1. Clears `CheckoutInvoice.Tenders`, and stops there for a session that settles an existing balance (`SettlesExistingBalance`), saves a payment method for later charges (`SavePaymentMethod`), has no selection, or has nothing to pay now (`InitialPaymentAmount` of zero).
2. Asks each provider for the lines of the references chosen with its key. A provider that throws is logged and skipped, so the buyer pays the rest by card rather than being stopped.
3. In the buyer's order, gives each line the smaller of what's on it, less what other checkouts hold (`ICheckoutTenderLedger.GetHeldByOthersAsync`), and what's left to pay, rounded with `Money.Round`. Lines that would pay nothing are dropped.
4. Takes what they pay off `InitialPaymentAmount`. `GrandTotal` stays the full total.
5. Lets the payment provider carry the tax first: only the tax the remaining charge can't hold, `Math.Max(0, TaxAmount - InitialPaymentAmount)`, is spread over the tenders in proportion to what they pay, into `TenderLine.TaxAmount`, the last tender taking what rounding leaves.

Tenders never pay a recurring obligation: the agreement the provider sets up is always for the full cycle amount.

The invoice exposes the result through computed members that aren't serialized: `TenderTotal` (what the tenders pay), `TenderTaxAmount` (their share of the tax) and `AmountDue` (`GrandTotal - TenderTotal`, never below zero). `DefaultCheckoutEngine.GetObligationAmounts` charges the one-time obligation `InitialPaymentAmount`, with a tax of `Math.Min(Math.Max(0, TaxAmount - TenderTaxAmount), gross)`, so the payment the provider reports still splits into the amount and the tax the invoice expects. For example, $90 of goods with $10 tax and a $95 gift card: the card is charged $5, all of it tax, and the gift card carries the other $5 of tax.

`CheckoutInvoiceSummary` shows **Total** (`GrandTotal`), a line per tender and **Due now** (`AmountDue`) when there are tenders, and **Due now** alone otherwise. The Stripe panel offers wallet buttons for `AmountDue`.

### The ledger

`CheckoutTenderLedger` keeps one `CheckoutTenderHold` per checkout and balance (`ItemId`, `SessionId`, `ProviderKey`, `Reference`, `Description`, `Currency`, `Amount`, `ReturnedAmount`, `Status`, `CreatedUtc`, `RedeemedUtc`), indexed by `CheckoutTenderHoldIndex`, which `CheckoutTenderMigrations` creates with indexes on the provider key, reference and status, and on the session and status. `Status` is `Held`, `Redeemed` or `Released`.

Every change to a balance takes the lock `CheckoutTenderLocks.GetKey(providerKey, reference)` (`TENDER_{providerKey}_{reference}`): the ledger's, and a provider's own, such as a staff adjustment. The ledger commits each hold before letting the lock go, so the second of two checkouts spending the same money sees what the first holds.

| `ICheckoutTenderLedger` member | What it does |
| --- | --- |
| `GetHeldByOthersAsync` | How much of a balance other checkouts hold now. |
| `HoldAsync(flow, tender)` | Under the balance's lock, asks the provider's `GetBalanceAsync` and holds `tender.Amount` when that much is left once other holds are counted. Returns a `TenderAvailability`: `Available`, `Insufficient` (less is left), `Unavailable` (it can't pay at all) or `Busy` (the lock couldn't be taken). |
| `ReleaseOthersAsync(sessionId, keep)` | Lets go of the checkout's holds on balances no longer on its invoice. |
| `RedeemAsync(flow)` | Calls `DebitAsync` for each hold and marks it `Redeemed`. A hold is taken even when its lock can't be had, with a warning logged, because the hold already kept the money for this checkout. Safe to call again. |
| `ReleaseAsync(sessionId)` | Marks every hold of the checkout `Released`. |
| `GetRedeemedAsync(sessionId)` | What was taken for a checkout and not put back yet. |
| `ReturnAsync(sessionId, amount, reason)` | Puts up to `amount` back through `CreditAsync`, the balance used last first, recording `ReturnedAmount`. Returns how much went back. |

### Holding, redeeming and releasing

`TenderHoldPaymentGate` is registered after `CouponHoldPaymentGate`, so it holds what's left after a code comes off. Inside the checkout's lock, just before a payment begins, or before a checkout with nothing to pay completes, it releases the holds on balances the buyer took off, then holds each tender on the invoice:

- `Busy` refuses the payment with *We couldn't reserve … just now. Please try again.*
- `Insufficient` rebuilds the invoice, which uses what's left on the balance, and refuses with `InvoiceChanged` and *… has less on it than when it was applied, so it pays less. Check what's left to pay, then pay again.*
- `Unavailable` takes the balance off the `CheckoutTenderSelection`, rebuilds the invoice, and refuses with `InvoiceChanged` and *… can't be used any more, so it was taken off. Check what's left to pay, then pay again.*

Each message names the tender by its `Description`. As with coupons, the payment script loads the page again with the new total.

`TenderCheckoutHandler.CompletingAsync` calls `RedeemAsync` while completing, not after, so taking the balances is committed with the completion: when it fails, the checkout isn't complete and the reconciliation sweep tries again. `CanceledAsync`, `FailedAsync` and `ExpiredAsync` call `ReleaseAsync`. A hold has no expiry of its own: an abandoned checkout lets go of its holds when the sweep expires it after **Session lifetime**.

### Superseded payment attempts

A buyer can begin a card payment, go back, apply a gift card and pay again. The attempt begun for the old total must not be resumed, because it would charge what the invoice no longer asks for. `PaymentAttempt.IsSuperseded` marks an attempt that was set aside and never charged.

- In `BeginPaymentAsync`, an existing `Created` or `Pending` attempt whose `ExpectedAmount` or `ExpectedTaxAmount` doesn't match `GetObligationAmounts` for the current invoice is superseded, and a new attempt is made for the new total.
- In `TryCompleteAsync`, a checkout whose invoice now has no obligations supersedes its `Created` and `Pending` attempts, so they neither keep it waiting nor get charged.

`TrySupersedeAsync` asks the provider first (`VerifyAsync`) about a `Pending` attempt the provider was already given. An attempt the provider says succeeded, or one it couldn't be asked about, stands, with a warning logged, so a payment the buyer finished in another tab is never dropped. Otherwise the attempt becomes `Canceled`, with `IsSuperseded` and the failure reason *The total changed before it was paid.* `CheckoutReconciliationService` leaves superseded attempts out of the checkout's payments.

### Nothing left to pay

When the tenders (or a code) cover everything, `CheckoutObligations.GetExpectedObligationIds` is empty. The payment step shows *No payment is required to complete this checkout.* instead of the payment methods, and the submit button reads **Place order** instead of **Pay now**, on the stepped layout (`CheckoutFlowButtons`) and on one page (`CheckoutOnePageViewModel.NothingToPay`). The post completes through `TryCompleteAsync`, which runs the payment gates with no provider key, so the balances are held before the checkout completes.

### The payment step

Each provider's box is a `CheckoutFlowDisplayDriver` for the payment step in its own module, rendered in the `Aside` zone under the promotion code box. A box posts one of two form actions, which `CheckoutController` saves and shows again, like applying a coupon, without taking a payment:

- `CheckoutConstants.FormActions.ApplyTender` (`apply-tender`). The controller accepts any action that starts with it, so a provider can name its own button, such as `apply-tender:store-credit`.
- `CheckoutConstants.FormActions.RemoveTenderPrefix` (`remove-tender:`) followed by the provider key, a colon and the reference, such as `remove-tender:gift-card:` and the card's item id.

The driver adds or removes the choice on the `CheckoutTenderSelection`, puts it back on the session and rebuilds the invoice through `CheckoutInvoiceBuilder.BuildAsync`, so the buyer sees what the balance pays before they pay. Gift cards, store credit and points render at `Aside:6`, `Aside:7` and `Aside:8`.

### Refunds

What a balance paid is refundable too. The [Orders](carts-and-orders#managing-an-order) feature counts each redeemed hold's `Amount - ReturnedAmount` as refundable, and `OrderOperationsService.RefundAsync` refunds the card first (`RefundPaymentsAsync`, then `RefundTransactionsAsync`), and only what's left goes back on the balances (`RefundTendersAsync`, through `ICheckoutTenderLedger.ReturnAsync`).

### Adding a kind of balance

1. Implement `ICheckoutTenderProvider` and register it as a scoped service. Keep every change to your balance, staff adjustments included, under `CheckoutTenderLocks.GetKey(Key, reference)`.
2. Add a `CheckoutFlowDisplayDriver` for `CheckoutConstants.PaymentStepKey` that adds your references to the session's `CheckoutTenderSelection` on `apply-tender` and removes them on `remove-tender:` with your key and the reference, then rebuilds the invoice.
3. Read what was paid from `CheckoutInvoice.Tenders`; the Orders feature copies it to `Order.Tenders`. The checkout holds, redeems, releases and returns the money through your provider, so never change a balance from a checkout handler yourself.

## The checkout engine

**`ICheckoutEngine`** is the single entry point for every money-moving step. Controllers, endpoints, webhooks, and background tasks all go through it and never call a payment provider, the attempt ledger, or the reconciliation service directly. Centralizing the orchestration is what makes the guarantees above real rather than conventions each caller has to remember.

| Member | What it does |
| --- | --- |
| `StartAsync` | Creates the session, runs the handlers so features contribute their steps and billing items, builds the invoice, and persists it. |
| `BeginPaymentAsync` | Creates one durable attempt **per obligation**, persists each before contacting the provider, stores the provider's reference the moment it returns, and hands back what the client needs to finish (a client secret, a redirect, or nothing). |
| `TryCompleteAsync` | Verifies every outstanding obligation against the provider's own API and, only when all are confirmed, runs the completion handlers and marks the session complete. |
| `CancelAsync` | Releases the remote resources of a checkout the customer abandoned. |

A checkout normally belongs to whoever starts it. **`StartCheckoutRequest.OwnerId`** names someone else instead:
an administrator taking a payment for a customer, or a background charge with no request at all. The session,
its attempts and what it settles then belong to that customer. Only server code sets it.

When a provider reaches the gateway and the payment is refused — a saved card charged without the customer, for
example — it returns `PaymentBeginResult.Decline`. The engine records the attempt as **failed** with the gateway's
reason, reports it as `PaymentBeginOutcome.Declined` with `ProviderErrorMessage`, and gives the next attempt for the
same obligation its own idempotency key, so retrying with another card is not answered with the old decline.

### Where the customer returns

The module that starts a checkout can say where the customer goes back to and what the confirmation is called, through three optional `StartCheckoutRequest` properties:

| Property | What it does |
| --- | --- |
| `ReturnUrl` | The page the customer returns to, usually the one that started the checkout. The confirmation page shows a **Continue** button to it; the payment step (and the first step, which has no **Back** of its own) and the page of a canceled, expired or failed checkout show a **Back** link to it. |
| `ReturnText` | The text of that button and link, for example *Back to my payments*. When empty the pages say **Continue** or **Back** (or **Leave checkout** on a payment step that also has a **Back** to the previous step). |
| `Title` | The confirmation heading, for example *Payment received*, so a checkout that is not an order is not headed *Order confirmation* or thanked for a *purchase*. |

```csharp
var session = await checkoutEngine.StartAsync(new StartCheckoutRequest
{
    ReferenceType = "RegistrationFee",
    ReferenceId = fee.Id,
    ReturnUrl = Url.Action("Index", "Payments"),
    ReturnText = S["Back to my payments"],
    Title = S["Payment received"],
});
```

They are stored on the session as a `CheckoutReturnInfo` and read back with `CheckoutReturnInfo.GetFrom(session)`. `ReturnUrl` must be **local**: a path such as `Url.Action(...)` or `Url.RouteUrl(...)` produces (which includes the tenant's URL prefix), or an application-relative `~/` path. `StartAsync` throws `ArgumentException` for anything else, including absolute and protocol-relative (`//host`) URLs, before the session is created, so a checkout can never send a customer to another site. The rule is `CheckoutReturnInfo.IsLocalUrl`, the same one as ASP.NET Core's `IUrlHelper.IsLocalUrl`, and the pages apply it again when they render the link.

The [Transactions](transactions) pay-link page passes itself as the return URL, with *Back to the payment page* and the heading *Payment received*.

A checkout that is no longer live still has a page rather than a `404`: a completed one redirects to its confirmation, and a canceled, expired or failed one shows *This checkout is no longer available.* with the reason and the **Back** link. The page is only shown to the visitor who owns the checkout, through the same ownership-checked lookup as every other checkout page.

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

### Prices the buyer chose

A product may be offered on several sets of terms, and `StartCheckoutRequest.PriceSelection` carries which
one the buyer took, along with a quantity and any amount they named. It is applied to the session **before**
the flow is built, because building the flow is what creates the steps and their charges: a selection
applied afterwards is read by nothing, and the checkout quietly bills the default instead. Nothing in it is
trusted — the pricing seam refuses a withdrawn price, an amount outside the bounds, or a quantity beyond
the maximum.

At a gateway, a fixed price becomes one reusable price object identified by a key that includes the amount,
so the same offer is not recreated per customer and raising a price creates a new one rather than repricing
everyone already on it. An amount the buyer named has no reusable offer behind it and is sent inline.

### Compensation

When one obligation fails at the provider, the engine refunds the obligations that already settled — through `ICheckoutRefundService`, never straight to the gateway — so the customer is not left paying for half a purchase, and the gateway's own refund notification correlates to a local record instead of being quarantined for an operator.

## The public checkout

Enabling the feature adds a customer-facing checkout at `/Checkout/{sessionId}/{step}`:

- Steps are rendered by display drivers against `CheckoutFlow`, so a feature contributes a step without touching the page. Derive from **`CheckoutFlowDisplayDriver`** and it renders only while its own step is current.
- The shared frame (progress stepper, invoice summary, navigation) is a separate driver, so a step never has to re-render its surroundings. Override the `CheckoutFlow` shape template in a theme to restyle the whole checkout.
- The payment step lists the payment providers **actually registered on the tenant** and filters out any that cannot settle what the invoice owes, so the page can never offer a method the framework cannot execute.
- Each provider renders its own panel through an `IDisplayDriver<CheckoutFlowPaymentMethod>` grouped by its provider key. Only the selected panel is shown.
- Two JSON endpoints (`checkout/{sessionId}/payment/begin` and `.../status`) carry the begin-then-poll conversation. Both are rate limited per visitor per checkout, resolve the session through the ownership-checked lookup, and require a same-origin request as defense in depth.
- The begin endpoint drops the saved-card keys (`savePaymentMethod`, `offSession`, `savedCustomerReference`, `savedPaymentMethodReference`) from the provider data the browser sends. Only server code decides whether a card is kept, and nothing a browser sends can spend a card kept earlier.
- A session whose `CheckoutSession.SavePaymentMethod` is set (server code sets it, for example when a customer chooses to pay a balance in [installments](installment-plans#installments-at-checkout)) keeps the payment method for later charges without the customer: the payment step offers only providers with `SupportsSavedPaymentMethods`, tells the customer the card is saved, and renders each provider panel with `CheckoutFlowPaymentMethod.SavePaymentMethod`; `ICheckoutEngine.BeginPaymentAsync` refuses any other provider and adds `CheckoutPaymentDataKeys.SavePaymentMethod` to the provider data itself.

A customer who owes nothing (a free plan, a balance already covered, or an order a gift card or a code pays in full) completes without being asked for a payment method at all; the button reads **Place order**. See [Nothing left to pay](#nothing-left-to-pay).

### Payment methods a checkout can rule out

A provider that implements **`ICheckoutPaymentProviderAvailability`** decides for each checkout whether it can be chosen, for example one the store turned off, or cash on delivery for an order with nothing to deliver. `CheckoutPaymentMethodSelector.GetAvailableAsync` leaves it out of the payment step, and `ICheckoutEngine.BeginPaymentAsync` refuses it too, since a request can name any provider. **`ICheckoutPaymentProviderDescription`** gives the line under the provider's name. The confirmation says *your order is placed* instead of *your purchase is complete* when the checkout was completed with a provider whose capabilities say `DefersPayment`.

A provider can report a fraud rating with the verification (`PaymentVerificationResult.RiskLevel` — `normal`, `elevated` or `highest` — and `RiskScore`), which the reconciliation keeps on the `PaymentAttempt`.

### One-page layout

`CheckoutSettings.Layout` (**Settings > Commerce > Checkout**, group `checkout`, permission `ManageCheckoutSettings`) is `CheckoutLayout.Steps` (the default) or `CheckoutLayout.OnePage`. On one page, `CheckoutController` builds the editor of every visible step in order, each with the flow's current step set to it, and renders their `Header`, `Content` and `Footer` zones as sections of the `OnePage` view, with the last section's `Aside` zone (the invoice summary and the coupon box). Step drivers need no change.

- A post updates every section it showed, in order, and redirects back (post, redirect, get). The payment section joins the page once every visible step with `CollectData` before it has its key in `SavedSteps`.
- With payment showing, the payment script takes the submit as on the stepped layout. A post from that page with no `checkout-action` completes the checkout, for a purchase that needs no gateway.
- A change to a section above payment switches the button to **Update order**, a submit named `checkout-action` with `CheckoutConstants.FormActions.Update`, which saves and shows the new total before anything is charged.

## Test payments

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Checkout.TestProvider` (**Test payments**) |
| **Category** | Payment Providers |
| **Dependencies** | `CrestApps.OrchardCore.Checkout` |
| **Provider key** | `test` |

The **Test payments** feature adds a payment method for development and automated tests. It lets a consumer test a whole checkout, in CI or on a developer's machine, without gateway keys or a browser that can drive a card form. A suite can pay a checkout with two requests:

```http
POST /checkout/{sessionId}/payment/begin
Content-Type: application/json

{ "providerKey": "test" }
```

```http
POST /checkout/{sessionId}/payment/status
```

The first request begins the payment and needs no client action (`requiresAction` is `false`). The second completes the checkout, runs the completion handlers, and settles a transaction that the checkout pays.

### When the provider is active

The test method confirms every payment without moving money, so anyone who can open a checkout could take a purchase for free with it. Turning the feature on is therefore not enough on its own:

- In the **Development** environment (`IHostEnvironment.IsDevelopment()`), the provider is registered.
- In any other environment, the provider is registered only when `CrestApps:Checkout:TestProvider:AllowOutsideDevelopment` is `true`, and a warning is logged at startup that anyone can complete a checkout without paying.
- Otherwise nothing is registered: the method is not offered on the payment step, a begin request that names `test` is refused like any unknown provider, and a warning is logged that the feature is enabled but inactive.

```json
{
  "OrchardCore": {
    "CrestApps": {
      "Checkout": {
        "TestProvider": {
          "AllowOutsideDevelopment": true
        }
      }
    }
  }
}
```

```text
OrchardCore__CrestApps__Checkout__TestProvider__AllowOutsideDevelopment=true
```

The setting is read when the tenant starts, so a change takes effect after the tenant is reloaded. See the [Configuration reference](../configuration.md#test-payments).

### What it does

`TestCheckoutPaymentProvider` implements `ICheckoutPaymentProvider`, `ICheckoutPaymentRefundProvider`, `ICheckoutSavedPaymentMethodProvider` and `ICheckoutRecurringPaymentProvider` under the key `test`, so it goes through the same ledger, reconciliation and checks as a real gateway:

| Operation | Behavior |
| --- | --- |
| `BeginAsync` | Succeeds at once with a `test_pay_…` reference and no client action. A retried begin keeps the attempt's reference. |
| `VerifyAsync` | Reports `Succeeded` with `ReportsAuthoritativeAmount = true`, the attempt's expected amount and tax, its currency, and `GatewayMode.Testing`. Because the amount is authoritative, the checkout cross-checks it exactly as it does for Stripe. |
| `CancelAsync` | Always succeeds. |
| `RefundAsync` | Succeeds for the requested amount with `GatewayMode.Testing`. The refund reference is derived from the idempotency key, so a retried refund returns the same refund. Refunds flow back to the transaction like any other refund. |
| `GetSavedPaymentMethodAsync` | After a payment begun with `CheckoutPaymentDataKeys.SavePaymentMethod`, returns a saved method (`Test •••• 4242`) whose references a later off-session charge (`CheckoutPaymentDataKeys.ForOffSessionCharge`) accepts. An off-session charge naming any other method fails. |
| `BeginRecurringAsync` | Establishes an agreement with a `test_sub_…` reference. Cancel, pause and update always succeed. |

The provider declares one-time, recurring and combined payments, refunds and saved payment methods. It declares in-page collection (`SupportsEmbeddedElements`) because its panel is shown on the page, which is what lets the installment plan down payment screen offer it. It does not defer payment, so it can pay off an existing balance.

On the payment step it contributes a one-line panel that says no money moves.

### Magic amounts

The last two digits of what an obligation charges, tax included, in the currency's smallest unit (cents for USD, yen for JPY) choose the outcome, the way gateway test cards do:

| Amount ends in | Outcome |
| --- | --- |
| `02` (for example `10.02`) | The payment is **declined** when it begins: the attempt is recorded as failed and the begin request returns an error, as a declined card does. Off-session charges and recurring agreements are declined too. |
| `05` (for example `10.05`) | The payment stays **pending** for two minutes (`TestCheckoutPaymentProvider.PendingSettlementDelay`) after the attempt was created. The status endpoint reports `Pending` and the browser stops waiting; the reconciliation sweep picks the attempt up once it is that old and completes the checkout. |
| Anything else | The payment succeeds. |

## Extending checkout

To use the framework in your own module:

1. Add a reference to `CrestApps.OrchardCore.Checkout.Core` (services) and `CrestApps.OrchardCore.Checkout.Abstractions` (contracts).
2. Implement **`ICheckoutHandler`** to contribute your steps and billing items and to react to completion.
3. Start a checkout with `ICheckoutEngine.StartAsync(...)` and redirect the customer to the `CheckoutStep` route. Do not drive payment yourself. Set `ReturnUrl`, `ReturnText` and `Title` on the request to bring the customer back to your page (see [Where the customer returns](#where-the-customer-returns)).
4. Render your step by deriving from **`CheckoutFlowDisplayDriver`** and registering it as an `IDisplayDriver<CheckoutFlow>`.
5. To add a gateway, implement **`ICheckoutPaymentProvider`**, register it, and add an `IDisplayDriver<CheckoutFlowPaymentMethod>` for its panel (grouped with `.OnGroup(yourProviderKey)`); the framework's reconciliation and ledger handle the safety guarantees for you.
6. To let a gateway refund a settled payment, also implement **`ICheckoutPaymentRefundProvider`** and issue refunds through **`ICheckoutRefundService`** — never by calling the gateway directly — so the durable refund ledger, tax allocation, and distributed over-refund protection apply.
7. To let a gateway bill on a cycle, also implement **`ICheckoutRecurringPaymentProvider`** under the same provider key. Implementations must be idempotent on the attempt's idempotency key, so a retried begin resumes the same agreement instead of creating a second one and billing the customer twice.
8. To let buyers pay with a balance they hold, such as a voucher or a wallet, implement **`ICheckoutTenderProvider`**; see [Adding a kind of balance](#adding-a-kind-of-balance).

## Related

- [Payments](payments) — the lower-level provider-agnostic payment contracts and the Stripe provider.
- [Pay Later](pay-later) — a built-in offline payment provider packaged as its own module.
- [Subscriptions](subscriptions) — a consumer of the checkout framework for recurring billing.
- [Taxation](taxation) — the tax determination framework consumed through `ICheckoutTaxService`.
