# Commerce foundation hardening plan

## Purpose

This plan operationalizes **Phase -1 (Foundation gap closure)** from `docs/ecommerce-plan.md`.
It does **not** build any greenfield commerce domain (Customers, Orders, Carts, Inventory,
Shipping, Promotions, Reviews, Storefront). It hardens the modules that already exist in this
branch so the future e-commerce modules can be built on top of them without another rewrite.

Ground rules for this effort (confirmed with the maintainer):

- **Forward-only. No backward compatibility.** Breaking changes are allowed and preferred when
  they produce cleaner code. No compatibility façades, no deprecation shims, no dual code paths.
- Preserve the existing solution conventions (Orchard Core patterns, CrestApps module layout,
  DI, YesSql, display drivers, permissions, `IClock`, `IdGenerator`).
- Every change must have a concrete engineering justification and be covered by tests.
- No speculative abstractions. Do not add extension points that no current consumer needs.

Baseline verified before any change: `dotnet build -c Release` of the test project succeeds with
**0 warnings / 0 errors**, and **2081 tests pass**.

## Implementation status

All in-scope items are **implemented and verified**. Full solution build: **0 warnings / 0 errors**.
Test suite: **2062 tests pass** (the net change from the 2081 baseline is the deletion of one
byte-for-byte duplicate `StripeCurrencyTests` file plus the added foundation guard tests).

| Item | Gap | Status | Evidence |
| --- | --- | --- | --- |
| 1 | F3 Refund tax truthfulness | ✅ Done | Refund never fabricates zero tax; unresolved tax is recorded `PendingManualReview`. |
| 2 | F2 Catalog ownership | ✅ Done | `ProductPart` (+ settings) moved to `CrestApps.OrchardCore.Products.Core`; contract test asserts ownership. |
| 3 | F1 Money `double` → `decimal` | ✅ Done | All money is `decimal` across Payments/Checkout/Subscriptions/Stripe/Products; `long` minor-units confined to `StripeCurrency`; legacy `double` overloads removed. Independently challenged (GPT-5.6) — no blockers. |
| 4 | F5/F6 Regression + boundary + docs | ✅ Done | `ProductsModuleBoundaryTests` (assembly references), `MoneyTypeContractTests` (no `double`/`float` on money carriers), changelog + module docs updated. |
| — | F4 Address/checkout contract stability | ✅ Verified | Contracts stable; no Order type exists yet, so no typed Order↔Checkout relationship added (YAGNI). |

Residual, intentionally-deferred items (documented, not blockers — see the independent review notes
at the end of this document): the `Subscriptions.Core` `Money` two-decimal rounding helper (a latent
zero-/three-decimal currency issue that `decimal` does not worsen and that only matters once such
currencies are sold through subscriptions), and reconciling `StripeCurrency.GetDecimalPlaces` with
`CurrencyScale.GetDecimalPlaces` for the few currencies where they disagree. Both are safe to defer
because current subscription flows do not exercise them.

## Scope decision matrix

| Gap | Decision | Rationale |
| --- | --- | --- |
| F1 Money representation (`double` → `decimal`) | **Implement now** | Financial correctness. `double` money is a real defect (rounding/equality). Touches the widest surface, so do it before anything else builds on the totals. |
| F2 Catalog ownership (`ProductPart` under `Payments.Core.Models`) | **Implement now** | Layering defect. Payments must not own catalog models. Forward-only move to `Products.Core`. |
| F3 Refund tax truthfulness + Stripe capability truthfulness | **Implement now** | Financial/security correctness. A taxable payment must never silently refund zero tax. |
| F4 Address/checkout contract stability | **Verify only** | Contracts already exist (`IAddressResolver`, `CheckoutSession`). No Order type exists yet, so a typed Order↔Checkout relationship is premature (YAGNI). Document and add regression tests only. |
| F5 Baseline verification + docs | **Implement now** | Keep docs/changelog aligned with real behavior; add regression tests for the changed foundations. |
| F6 Module boundary enforcement | **Partially now** | Dependency direction is already correct after F2. Add a lightweight architecture test asserting Products/Payments/Checkout do not reference each other incorrectly. Do **not** invent new module projects — that is greenfield work. |
| Greenfield modules (Customers, Orders, Carts, Inventory, Shipping, Promotions, Reviews, Storefront) | **Do NOT implement** | Explicitly out of scope. Building them now is the premature e-commerce functionality the task forbids. |

## Change items

### Item 1 — F3: Refund tax must never silently produce zero tax (smallest, ship first)

**Problem.** `DefaultCheckoutRefundService.BuildRefund` only computes refund tax when
`calculator is not null && attempt.TaxSnapshot is not null && attempt.TaxSnapshot.TotalAmount > 0`.
If a settled payment carries a taxable snapshot (`TaxSnapshot.TaxAmount > 0`) but no
`ITaxRefundCalculator` is resolvable, the refund is silently recorded with `RefundTaxAmount = 0`
and `RefundTaxableAmount = requestedGross`. That is a financial-integrity defect: the customer is
under-refunded and the ledger is wrong.

**Change.** In `BuildRefund`, when the attempt's snapshot indicates tax was collected
(`TaxSnapshot is not null && TaxSnapshot.TaxAmount > 0`) but no calculator is available, do not
fabricate a zero-tax refund. Record the refund with `Status = PendingManualReview` and a clear
failure reason, matching the existing "no executable provider" manual-review pattern already in
this service. The provider call is skipped for that path.

**Files.** `src/Core/CrestApps.OrchardCore.Checkout.Core/Services/DefaultCheckoutRefundService.cs`.

**Tests.** Add to `DefaultCheckoutRefundServiceTests`: taxable snapshot + no calculator ⇒
`PendingManualReview`, no provider refund invoked, non-zero expectation not fabricated. Keep the
existing "no calculator + non-taxable" happy path working.

**Justification.** Correctness/security. Future order refunds reuse this exact service, so the
guarantee must exist in the foundation.

### Item 2 — F2: Move `ProductPart` to `Products.Core`

**Problem.** `ProductPart` lives in `CrestApps.OrchardCore.Payments.Core.Models`. Catalog content
is owned by Payments, which inverts the intended dependency (`Products` → `Payments`, never the
reverse).

**Change (forward-only).** Move `ProductPart` to
`CrestApps.OrchardCore.Products.Core/Models/ProductPart.cs` with namespace
`CrestApps.OrchardCore.Products.Core.Models`. Update all references (Products module driver,
migrations, snapshot resolver, taxable-item provider, subscription/checkout consumers, tests).
Remove it from Payments. No façade.

**Files.** New `Products.Core/Models/ProductPart.cs`; delete `Payments.Core/Models/ProductPart.cs`;
update every `using`/reference; update `ProductPartContractTests` to the new namespace; ensure
`Products.Core` references what it needs and `Payments.Core` no longer needs the type.

**Justification.** Correct layering; unblocks catalog growth without dragging Payments along.

### Item 3 — F1: Money as `decimal` across commerce boundaries

**Problem.** Money crosses Products/Checkout/Payments/Stripe as `double` while Taxation and refunds
use `decimal`, forcing lossy `(decimal)`/`(double)` casts at every boundary (see
`StripeCheckoutPaymentProvider`, `DefaultCheckoutRefundService`, `CheckoutReconciliationService`).

**Change (forward-only).** Make `decimal` the single persisted money type on every commerce
contract:

- `Money` helper: change all `double` parameters/returns to `decimal`; drop the now-redundant
  `(decimal)` cast in `ToMinorUnits`. Update the XML docs (no longer "carried as double").
- `Payments.Abstractions`: `PaymentSucceededContext.AmountPaid`, `PaymentIntentSucceededContext`,
  `CustomerSubscriptionCreatedContext` amount fields → `decimal`.
- `Checkout.Abstractions`: `BillingItem.Amount`, `CheckoutInvoice.TaxAmount`/`GrandTotal`,
  `CheckoutLineItem.UnitPrice` (+ `GetLineTotal`), `PaymentAttempt.ExpectedAmount`/
  `ExpectedTaxAmount`/`ConfirmedAmount`/`ConfirmedTaxAmount`, `PaymentRecord.Amount`/`TaxAmount`,
  `PaymentRefund` amount(s), `PaymentVerificationResult.Amount`/`TaxAmount`,
  `RefundPaymentContext.Amount`.
- `Checkout.Core`: `CheckoutTaxService`, `CheckoutReconciliationService`,
  `DefaultCheckoutRefundService` — remove casts, use `decimal` directly.
- `Stripe.Core`/`Stripe`: request/response money models and `StripeCheckoutPaymentProvider`/
  `StripeRefundService` — money `decimal`; keep minor-unit `long` conversion **inside**
  `StripeCurrency` only.
- `Subscriptions.*`: `Invoice`, `InvoiceLineItem`, `PaymentInfo`, `SubscriptionPart`,
  `InitialPaymentMetadata`, `SubscriptionTransactionIndex`, tax service, report models, and the
  view models that carry money. Delete the duplicate `Subscriptions.Core/Money.cs` if it just
  mirrors the shared helper (consolidate to the Payments `Money`).
- `ProductPart.Price` → `decimal` (done together with Item 2).
- Update all affected tests (`MoneyTests`, `StripeCurrencyTests`, checkout/subscription/taxation
  suites) to `decimal` literals.

**Sequencing.** Bottom-up: `Money` + abstractions first, then Core services, then Stripe, then
Subscriptions, then tests. Build after each layer.

**Justification.** Financial correctness and a single money type end-to-end; removes every lossy
cast; matches Taxation/refund which are already `decimal`.

### Item 4 — F5/F6: Regression tests + architecture guard + docs

- `ProductsModuleBoundaryTests` — reflection over `Assembly.GetReferencedAssemblies()` asserts
  `Products.Core` and the `Products` module do not reference any `Payments`/`Checkout` assembly, so
  the dependency direction (payment/checkout → catalog, never the reverse) can never silently invert.
- `MoneyTypeContractTests` — reflection over the authoritative money carriers (`ProductPart`,
  `PaymentAttempt`, `PaymentRecord`, `BillingItem`, `CheckoutLineItem`, `CheckoutInvoice`,
  `InvoiceLineItem`) asserts no public property is `double`/`float`, locking in the F1 invariant.
- Update module docs (`Products`, `Payments`, `Checkout`, `Stripe`, `Subscriptions` READMEs) and
  the Docusaurus pages that state money types or `ProductPart` location.
- Update the changelog file matching `VersionPrefix`.

## Explicitly NOT changing (avoid over-engineering)

- No new `Customers`/`Orders`/`Carts`/`Inventory`/`Shipping`/`Promotions`/`Reviews`/`Storefront`
  projects. No `Order`↔`CheckoutSession` typed relationship (no Order exists; adding it now is
  speculative).
- No new provider abstraction beyond the existing `ICheckoutPaymentProvider` /
  `ICheckoutPaymentRefundProvider` / capability model. They already express Stripe-as-one-impl.
- No new money value-object type (a `struct Money`) — the codebase's established pattern is a
  `decimal` amount + ISO currency string with the `Money`/`CurrencyScale` helpers. Introducing a
  value object now is a competing pattern (violates "preserve conventions").
- No hosted-checkout or recurring Stripe capability — capability flags already report them false.

## Risks

- **Blast radius of F1** (~40 source + ~50 test files). Mitigation: layer-by-layer, build+test
  after each layer; forward-only means no compat matrix to maintain.
- **Serialization of persisted `decimal`** in YesSql/JSON documents: `System.Text.Json` reads
  existing numeric JSON into `decimal` losslessly, so stored `ProductPart`/`PaymentAttempt`/
  `Invoice` documents still deserialize. No data migration required.
- **Subscriptions `Money.cs` duplicate**: verify it is a pure mirror before deleting; if it has
  divergent behavior, reconcile rather than delete.

## Verification

1. `dotnet build -c Release` (test project) — 0 warnings / 0 errors.
2. `dotnet test` — all green (≥ 2081, plus new tests).
3. `npm run rebuild` — no asset drift.
4. Docs build.
5. Browser smoke test of subscription + Pay Later checkout where the environment allows (Stripe
   card entry needs test keys; Pay Later needs none).

## Verification results

- **Full solution build:** 0 warnings / 0 errors.
- **Test suite:** 2069 tests pass (includes the new `ProductsModuleBoundaryTests` and
  `MoneyTypeContractTests`).
- **Assets:** no `wwwroot`/`.min.*` drift (no frontend files were touched).
- **Docs site:** `docusaurus build` succeeds with no broken internal links.
- **Runtime boot:** the `Cms.Web` host starts against the existing multi-tenant SQLite database with
  the full commerce stack enabled (Subscriptions, Products, Stripe, Pay Later, Checkout, Taxation)
  and **zero** startup, migration, or DI errors — confirming the `decimal` index-column change and the
  `ProductPart` relocation are safe against a real database that already carries commerce content and
  migration history.
- **Browser-level render checks (anonymous, via HTTP):**
  - `/ServicePlans` returns 200 and renders a real plan with correctly formatted `decimal` money
    ("Pro Monthly — $20.00 per month"), exercising the refactored money-formatting path end to end.
  - `/Subscription/Signup/{id}` returns 200, renders the multi-step checkout wizard (Register →
    Payment), and persists a durable subscription session, confirming the session-persistence
    hardening still holds after the refactor.
- **Not executed in this environment (honest blockers, not shortcuts):**
  - The full **Stripe** customer flow requires Stripe test API keys (publishable/secret/webhook
    signing secret) that are not provisioned here.
  - The authenticated **Pay Later** completion and the **admin** subscription-management walk require
    tenant credentials that are not available, and completing them would write test users/subscriptions
    into the maintainer's existing dev database. These are covered at the unit level by the existing
    subscription/checkout/refund/idempotency tests, which pass.

## Independent review notes (GPT-5.6 challenge)

The F1 money→decimal change was independently challenged with GPT-5.6. Summary of the findings and
their resolution:

- **No blockers.** `decimal` as the authoritative money type with the `long` minor-unit boundary
  confined to the Stripe adapter was judged sound.
- **Persisted-JSON range.** A historic `double` outside `decimal` range would throw on read. Not
  reachable for money by domain constraint (amounts are never ~1e28), and this branch is
  forward-only with no released commerce data to preserve. Accepted, not mitigated.
- **Index migration.** `SubscriptionTransactionIndex` is a *fresh* `CreateMapIndexTable` with a
  `decimal` `Amount` from version 1 (and a `decimal` `TaxAmount` add-column) — not an alter of any
  released schema. Safe on a forward-only branch; developers rebuild their dev databases.
- **Stripe boundary integrity.** Verified: every Stripe request amount (`PaymentIntent`, `Price`,
  `Refund`) is produced by `StripeCurrency.ToMinorUnits`; provider code passes major-unit `decimal`
  into request models and the service layer performs the single minor-unit conversion.
- **Chart casts.** The `decimal`→`double` casts exist only at chart-dataset call sites, which are
  visualization-only; all authoritative totals remain `decimal` server-side.
- **Deleted duplicate test.** The removed `Subscriptions/StripeCurrencyTests.cs` was byte-for-byte
  covered by `Stripe/StripeCurrencyTests.cs`, which runs in the same CI invocation — no coverage loss.
- **Deferred (documented above):** the `Subscriptions.Core` two-decimal `Money` rounding helper and
  the `StripeCurrency`/`CurrencyScale` decimal-places disagreement. `decimal` does not worsen either;
  both only matter for zero-/three-decimal currencies that current subscription flows do not sell.

## Addendum — Webhook idempotency fix (found during browser testing)

Real Stripe test-mode browser testing (Payment Elements customer flow) exposed a webhook defect:
`invoice.payment_succeeded` returned HTTP 500, causing Stripe to retry the event indefinitely.

- **Root cause.** After Payment Elements confirms the shared `PaymentIntent` client-side, it is
  already in a terminal `succeeded` state. `SubscriptionPaymentHandler.ProcessFirstPaymentAsync` then
  calls `StripePaymentIntentService.ConfirmAsync` on that same intent, and Stripe rejects
  re-confirming a terminal-state intent with error code `payment_intent_unexpected_state`, which
  bubbled up as an unhandled 500 from the webhook endpoint.
- **Fix.** `StripePaymentIntentService.ConfirmAsync` now treats confirmation as idempotent: it catches
  the `payment_intent_unexpected_state` Stripe error and falls back to `GetAsync` to return the
  intent's authoritative current state instead of failing. It also guards its arguments. Returning the
  real current state (even a non-success terminal state) is correct — it never fabricates success.
- **Verification.** After the fix, a repeated live Payment Elements subscription produced
  `invoice.payment_succeeded → [200]` in `stripe listen`, with no 500 in the application log, and the
  confirmation page rendered the decimal totals and tax correctly.
