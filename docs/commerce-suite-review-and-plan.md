# Commerce suite review and production-readiness plan

**Date:** 2026-09-07 · **Branch:** `ma/subscribtions` (`21989d5c7`) · **Scope:** every project under
`src/Modules`, `src/Core`, and `src/Abstractions` in the Commerce area (Checkout, Commerce, Pay Later,
Products, Stripe, Subscriptions, Taxation, Transactions) plus the supporting Payments, Customers,
Receipts, Wizard, Addresses, and Reports projects they depend on, their tests, and their docs.

---

## Implementation status (updated 2026-09-07, after the suite was run end to end)

**Phase 0 — complete.** All seven tasks landed: the tenant administrator password is unprotected before
setup (and wiped afterwards), payments carry their own collection dates, renewals advance the
subscription expiration, failed renewals and gateway-side cancellations are recorded through two new
provider-neutral events, compensation refunds are written to the durable refund ledger, the transaction
management screens share a state machine and handle concurrency, and the docs no longer describe
unreachable paths.

**Status: the plan is implemented and the suite has now been run.** The legacy signup flow is gone, buying a
plan goes through the checkout, and the whole path was exercised in a browser against a real site. Two items
remain deliberately undone: the ledger relocation and the pricing rework (5.1 to 5.4). Reasons below.

| Task | Status |
| --- | --- |
| 1.2 `ICheckoutEngine` | Done |
| 1.3 Async completion (sweep completes, no request-blocking poll) | Done |
| 1.4 Recurring provider contract, engine routing, provider data | Done |
| 1.5 Stripe recurring adapter (inline pricing, subscription verify/cancel/update) | Done, not exercised against a live gateway |
| 1.6 Pay Later recurring adapter + renewal sweep | Done, verified in the browser |
| 1.7 Public checkout UI | Done, verified in the browser |
| 1.8 Guest ownership token | Done |
| 1.9 Transactions online settlement reaches the checkout | Done, verified in the browser |
| 1.10 Admin payments/refunds screens + `ManageRefunds` | Done, verified in the browser |
| 1.1 Ledger relocation to Transactions | **Not done** — deliberately deferred |
| 2.1 Durable `Subscription` + index, store, manager, migrations | Done, verified in the browser |
| 2.2 Subscription created from a completed checkout, **legacy flow retired** | Done |
| 2.3 `ISubscriptionLifecycleService` (locked, idempotent transitions) | Done |
| 2.4 Provider to lifecycle bridge (`IPaymentEvent`) | Done |
| 2.5 Pay Later renewals | Done |
| 2.6 Customer portal (**My Plans**, self-service cancel) | Done, verified in the browser |
| 2.7 Admin actions (**Agreements**: cancel, pause, resume) | Done, verified in the browser |
| 2.8 Subscription workflow events | Done |
| 2.9 Reports rebased on the ledger | Done, verified in the browser |
| 3.x Tenant provisioning as a checkout consumer | Done; feature enables and migrates cleanly, provisioning itself not exercised |
| 4.x Provider, engine-concurrency and refund tests | Done, plus a SQLite-backed store test for the session identity. Gated end-to-end scenarios not started; an HTTP-driven purchase was run by hand instead. |
| 5.1–5.4 `ProductPricePart`, resolver, plan chooser, Stripe price sync removal | Partly obsolete — the sync service is deleted; the rest is **not done**, see below |
| 5.5 Trials | Done — the plan editor now has a **Free Trial Days** field, which it previously lacked |
| 5.6 Coupons | Done, verified in the browser end to end |
| 6.x Entitlements and member access | Done, verified in the browser (buying a plan granted the role) |
| 7.x Module READMEs, starter recipe, tests README, docs | Done; the starter recipe was executed |

### Why two items were left alone

**1.1 Ledger relocation.** A large mechanical namespace move with no user-visible effect. Nothing built since
depends on it, and it can still be done as its own pass.

**5.1 to 5.4, the pricing rework.** These replace `ProductPart.Price` with a repeatable `ProductPricePart` and
rewrite the plan chooser, touching the content schema of every existing site. The goal they serve — selling
any product at any price point — is already met by the Stripe adapter's inline pricing, so what is left is a
nicer editing experience rather than a missing capability.

### Independent review

After the browser run, the implementation was reviewed against the plan as a reader rather than as its
author. That found nine defects the browser could not: a free trial that could never complete, a
first-cycle coupon that was display-only (and would have recurred forever at Stripe), a cycle limit that
Stripe ignored for inline prices, gateway renewals that never reached the local agreement, offline
agreements that were never renewed at all, webhook completion blocked for every signed-in buyer, a
fulfillment failure that lost its own status write and was never retried, session expiry that was never
built, and a handful of commit-ordering and ownership gaps. Each is fixed and each has a regression test;
the 3.0.0 changelog lists them under *Fixes from an independent review*.

### Verification

Release build with `-warnaserror` is clean, **2,298 tests pass**, and the docs site builds.

More importantly, the suite was **run**. A site was set up from scratch, the Commerce starter recipe was
executed, and the following were exercised in a browser: creating a plan, the public plan list, starting a
checkout, the invoice with a one-time fee and a recurring charge, the payment method list and provider panel,
paying with Pay Later, the confirmation, the durable agreement in both the admin and customer screens, the
transaction and payment ledgers, creating and applying a coupon and seeing the usage count increment,
the six reports, buying anonymously with an account created and signed in mid-checkout, and a plan's role
entitlement reaching the buyer.

Running it found eight defects that the 2,275 passing unit tests could not, because in every case the page
returned `200` and quietly did the wrong thing. They are listed in the 3.0.0 changelog under *Fixes found by
running the checkout*; the worst was that a subscription checkout totalled `0.00` and completed for nothing,
because the plan's charges sit on a step that is never drawn and the invoice was built only from visible
steps. Each now has a regression test, two of which read the source tree so the failure fails a build rather
than a customer's purchase.

**Still unproven:** every live Stripe call, tenant provisioning against a real setup service (the feature
enables and migrates cleanly, but no site was provisioned), the renewal and lapse sweeps over real time, and
refunding a real payment.

This document has two halves:

1. **Review** (sections 1–6): what exists, how it compares to industry products, what is broken, what is
   missing, and how well it is tested and documented.
2. **Plan** (sections 7–12): a phased, task-level implementation plan written so an engineer or an AI
   coding agent can execute it without this conversation. Every task names files, behavior, acceptance
   criteria, and tests.

Read `docs/ecommerce-plan.md` first for the long-term e-commerce roadmap. This document does **not**
replace it; it corrects its baseline claims where the code disagrees (see §6.6) and defines the work
that must land **before** the Orders/Carts/Customers phases of that roadmap start.

---

## 0. Verdict in one page

**Is it a powerful checkout + subscriptions suite today?** Not yet. The *foundation* is unusually
good for a CMS commerce stack: `decimal` money with per-currency scale, a taxation engine with
snapshots and refund allocation, a durable payment-attempt and refund ledger with provider-authoritative
reconciliation, webhook de-duplication, distributed locks, and a provider-neutral obligation ledger with
reminders. Those pieces are well written and well tested (479 commerce-area tests pass).

**The problem is that the pieces are not connected.** Verified from source:

| # | Fact | Consequence |
| --- | --- | --- |
| 1 | Nothing in `src` calls `ICheckoutPaymentProvider.BeginAsync`, nothing creates a `PaymentAttempt`, nothing ever sets `CheckoutSessionStatus` to anything but `Pending`, and there is no controller, endpoint, or display driver that renders a `CheckoutFlow`. | The **Checkout framework has no runtime driver**. The generic Stripe card provider, the Pay Later provider, the refund service, the Pay Later → Transactions bridge, and online settlement of outstanding transactions **never execute in production**. `TransactionController.Pay` creates a session and redirects to a page that cannot pay it. |
| 2 | Subscriptions runs its **own** flow, session, invoice, and payment pipeline (`SubscriptionSession`, `SubscriptionPaymentSession` distributed cache, four Stripe endpoints, `CreatePayLaterEndpoint`). It shares only the rate limiter with Checkout. | The only real purchase flow on the branch uses the **cache-as-truth** model the Checkout docs say the framework exists to eliminate. A cache eviction between a successful Stripe charge and completion fails the checkout after the customer was charged. |
| 3 | `TenantOnboardingSubscriptionHandler` passes the **data-protected** admin password string to `ISetupService` without unprotecting it. | Every tenant sold through the onboarding flow is created with an unusable admin password. |
| 4 | No handling of `invoice.payment_failed`, `customer.subscription.deleted`, or `customer.subscription.updated`; renewals never extend `SubscriptionInfo.ExpiresAt`; no cancel/pause/upgrade for customers or admins; roles granted at signup are never revoked. | There is **no subscription lifecycle** after the first payment. Reports show every active subscription as expiring after one cycle. |
| 5 | Pay Later subscriptions never create a `Transaction`. | The documented "outstanding balance is tracked, reminded, and payable online" story is false for the only flow that exists. |

**Recommendation:** treat this release as *"make the framework real"*: put one checkout engine under
every purchase (subscriptions, one-time, settlement), finish the subscription lifecycle, fix the
verified bugs, then add the pricing flexibility, entitlements, and operational tooling that a
subscription business needs. The e-commerce roadmap (Orders/Carts) should wait until §8 Phases 0–4 are
done, because those phases change contracts the roadmap builds on.

---

## 1. Method and baseline

- Read every `.cs` in the eight Commerce modules and their Core/Abstractions projects, the Wizard,
  Receipts, Customers, and Payments projects they depend on, the Razor views and JS that drive payment,
  the seven Docusaurus pages, and the four `docs/ecommerce-*.md` planning documents.
- Cross-referenced claims against code with searches (callers of `BeginAsync`, writers of
  `PaymentAttempt`, assignments of `CheckoutSessionStatus`, handlers of Stripe event types, etc.).
- Built the test project and ran the commerce-area suites:

```text
dotnet build tests/CrestApps.OrchardCore.Tests/CrestApps.OrchardCore.Tests.csproj -c Debug
dotnet tests/CrestApps.OrchardCore.Tests/bin/Debug/net10.0/CrestApps.OrchardCore.Tests.dll \
  -filter "/*/CrestApps.OrchardCore.Tests.Checkout*" ... -filter "/*/CrestApps.OrchardCore.Tests.Wizard*"
Total: 479, Failed: 0
```

- Compared the feature set against Stripe Billing, Chargebee, Recurly, Paddle, Lemon Squeezy (SaaS
  subscription products), Shopify and Medusa (checkout/order/transaction models), and Orchard Core
  Commerce by Lombiq (the closest in-ecosystem comparison).

---

## 2. As-is architecture

### 2.1 Projects

| Area | Abstractions | Core | Module | Role today |
| --- | --- | --- | --- | --- |
| Payments | `Payments.Abstractions` (`Money`, `CurrencyScale`, `IPaymentEvent`, event contexts, `DurationType`, `PaymentMethodOptions`) | `Payments.Core` (empty) | — | Provider-neutral webhook event contract and money helpers. |
| Checkout | `Checkout.Abstractions` (session, flow, steps, invoice, `PaymentAttempt`, `PaymentRefund`, provider/refund contracts, stores) | `Checkout.Core` (stores, reconciliation, refund service, tax bridge, limiter, cache) | `Checkout` (DI, migrations, reconciliation task, taxation wiring) | Framework only. **No UI, no driver.** |
| Stripe | — | `Stripe.Core` (service interfaces, request/response models, `StripeCurrency`, idempotency key) | `Stripe` (settings + one-step connect, webhook endpoint, Stripe services, generic `StripeCheckoutPaymentProvider`, workflow events) | Provider adapter. Card PaymentIntent provider registered but unused; subscription behavior is driven from Subscriptions. |
| Pay Later | — | — | `PayLater` (`ICheckoutPaymentProvider`, `PayLaterTransactionCheckoutHandler`, settings) | Offline provider for the Checkout framework. Unused at runtime (see §0). Subscriptions has a separate Pay Later endpoint. |
| Products | — | `Products.Core` (`ProductPart`, `ISellableProduct`, `IPriceResolver`, `IProductSnapshotResolver`) | `Products` (editors, currencies catalog, recipe/deployment steps, taxable item provider) | Catalog seam. One price + one currency per item. |
| Subscriptions | `Subscriptions.Abstractions` (**duplicate** flow/step/session/billing types) | `Subscriptions.Core` (handlers, invoice, payment cache, Stripe price sync, indexes, tax) | `Subscriptions` (wizard bridge, Stripe endpoints, Pay Later endpoint, dashboard, admin list, reports, tenant onboarding) | The only working purchase flow. Hard-references `Stripe.Core`. |
| Taxation | `Taxation.Abstractions` | `Taxation.Core` (engine, methods, resolvers) | `Taxation` (admin CRUD, part, recipes/deployments, taxonomy classification) | Complete, well tested, reusable. |
| Transactions | `Transactions.Abstractions` (`Transaction`, statuses, events, stores) | `Transactions.Core` (store, manager, sources) | `Transactions` (admin console, customer statement, reminders feature, settlement handler) | Outstanding-obligation ledger. Online settlement unreachable (see §0). |
| Commerce | `Commerce.Abstractions` (financial document policy contracts) | — | `Commerce` (admin menu, receipts-only policy) | Menu shell. |
| Supporting | `Customers.Abstractions` (`CustomerOwner`, contact resolver), `Receipts.*`, `Wizard.*`, `Addresses.*`, `Reports.*` | | | Wizard is the third flow engine; Receipts renders on demand. |

### 2.2 Runtime flows that actually exist

```mermaid
flowchart LR
  subgraph Subscriptions signup (works)
    A[ServicePlans list] --> B[Wizard controller/engine]
    B --> C[SubscriptionWizardHandler ↔ ISubscriptionHandler bridge]
    C --> D[Payment step: browser JS]
    D -->|Stripe endpoints| E[Stripe SetupIntent / PaymentIntent / Subscription]
    D -->|Pay Later endpoint| F[SubscriptionPaymentSession cache]
    E -->|webhooks| F
    C -->|CompletingAsync polls cache ≤60s| G[SubscriptionSession Completed]
    G --> H[UserRegistration / Content / TenantOnboarding CompletedAsync]
  end
  subgraph Checkout framework (no driver)
    I[ICheckoutSessionStore.NewAsync] -. only TransactionController.Pay .-> J[CheckoutSession Pending]
    J -. nobody calls BeginAsync / creates PaymentAttempt .-> K[Reconciliation / Refunds / PayLater→Transactions]
  end
```

Three flow engines coexist: `CheckoutFlow`/`CheckoutSession`, `SubscriptionFlow`/`SubscriptionSession`,
and `WizardFlow`/`WizardSession`. `SubscriptionWizardSessionMapper` copies a `SubscriptionSession` into
a `WizardSession` and back on **every** request so the shared Wizard controller can host it.

---

## 3. Findings: correctness and production readiness

Severity: **P0** = money, security, or data loss; **P1** = customer-visible malfunction; **P2** =
robustness/maintainability. Each finding names the evidence so it can be re-verified.

### 3.1 Checkout framework

| ID | Sev | Finding | Evidence |
| --- | --- | --- | --- |
| C-01 | P0 | **The Checkout framework is never driven.** No caller of `ICheckoutPaymentProvider.BeginAsync`; no creation of `PaymentAttempt`; `CheckoutSessionStatus` is only ever `Pending`; no `IDisplayDriver<CheckoutFlow>` or `IDisplayDriver<CheckoutFlowPaymentMethod>` exists; no controller renders a `CheckoutFlow`. | `grep` across `src` for `.BeginAsync(`, `new PaymentAttempt`, `CheckoutSessionStatus.Completed`; only `Checkout/Startup.cs`, the reconciliation task, `DefaultCheckoutRefundService`, `PayLater/Startup.cs`, and `TransactionController` reference `ICheckoutSessionStore`. |
| C-02 | P1 | `TransactionController.Pay` creates a settlement `CheckoutSession` and redirects to the detail page with "complete the payment with a configured provider". There is no page that can. | `src/Modules/CrestApps.OrchardCore.Transactions/Controllers/TransactionController.cs` `Pay`. |
| C-03 | P1 | `CheckoutReconciliationBackgroundTask` verifies attempts but never transitions the session to `Completed`/`Failed` nor runs `ICheckoutHandler.CompletedAsync`. A checkout settled only by the sweep (customer closed the browser after paying) never fulfills. | `Checkout/Tasks/CheckoutReconciliationBackgroundTask.cs`; `PaymentCheckoutHandler.CompletingAsync` is the only completion path and it is request-bound. |
| C-04 | P2 | `PaymentCheckoutHandler.CompletingAsync` busy-waits up to 60 × 1 s inside the web request (same pattern in `PaymentSubscriptionHandler`). Under the wizard engine's 2-minute completion lock and typical proxy timeouts this is fragile, and it ties a web worker per customer. | `Checkout.Core/Handlers/PaymentCheckoutHandler.cs` `MaxCompletionAttempts`. |
| C-05 | P1 | `StripeCheckoutPaymentProvider.CancelAsync` refunds a succeeded intent by calling `IStripeRefundService` directly, bypassing `ICheckoutRefundService`. No `PaymentRefund` is written, so the later `charge.refunded` webhook quarantines the refund as `remote_refund_without_local_request`. | `Stripe/Services/StripeCheckoutPaymentProvider.cs` `CancelAsync`. |
| C-06 | P2 | Two provider registries: `PaymentMethodOptions` (Subscriptions UI) and `ICheckoutPaymentProvider` (framework). Two settings objects with the same fields: `CheckoutSettings` and `SubscriptionSettings` (`Currency`, `DefaultPaymentMethod`). | `Payments.Abstractions/Models/PaymentMethodOptions.cs`, `Checkout.Abstractions/Models/CheckoutSettings.cs`, `Subscriptions.Core/Models/SubscriptionSettings.cs`. |
| C-07 | P2 | Guest session ownership is IP + User-Agent. Mobile carriers rotate IPs mid-session, and everyone behind one NAT with the same browser build shares the key. Documented as a known weakness in `ecommerce-foundation-decisions.md` F2, but it is on the live path. | `Checkout.Core/Services/CheckoutSessionStore.cs`, `SubscriptionSessionStore.cs`, Wizard store. |

### 3.2 Subscriptions

| ID | Sev | Finding | Evidence |
| --- | --- | --- | --- |
| S-01 | P0 | **Payment truth lives in a distributed cache.** `PaymentSubscriptionHandler.CompletingAsync` requires `InitialPaymentMetadata` / `SubscriptionPaymentsMetadata` from `SubscriptionPaymentSession` (IDistributedCache, 1-day TTL), which is populated only by webhooks. If the cache entry is missing (eviction, Redis restart, webhook delay > 60 s, webhook not configured in dev) the flow throws after Stripe has charged the card. No durable `PaymentAttempt` exists for subscription payments. | `Subscriptions.Core/Handlers/PaymentSubscriptionHandler.cs`, `SubscriptionPaymentSession.cs`, `Subscriptions/Handlers/SubscriptionPaymentHandler.cs`. |
| S-02 | P0 | **Tenant admin password bug.** The onboarding driver stores `protector.Protect(model.AdminPassword)` in `SavedSteps`; `TenantOnboardingSubscriptionHandler.CompletedAsync` passes `info.AdminPassword` straight into `SetupConstants.AdminPassword`. There is no `Unprotect` anywhere in `Subscriptions.Core`. The unit test seeds a raw password and therefore cannot catch it. | `Subscriptions/Drivers/Steps/TenantOnboardingStepSubscriptionFlowDisplayDriver.cs` lines 222–225; `Subscriptions.Core/Handlers/TenantOnboardingSubscriptionHandler.cs` line 192; `tests/.../TenantOnboardingSubscriptionHandlerTests.cs` line 253. |
| S-03 | P0 | **No lifecycle after first payment.** Webhook `SupportedEvents` lacks `invoice.payment_failed`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.upcoming`. `PaymentSucceededAsync` for `subscription_cycle` appends a `PaymentInfo` but never advances `SubscriptionInfo.ExpiresAt`. `SubscriptionInfo` has no status. `SubscriptionSessionStatus.Suspended` is never assigned. No customer or admin cancel/pause/resume/upgrade. `SubscriptionRoleSettings` roles are granted at signup and never revoked. | `Stripe/Endpoints/CreateWebhookEndpoint.cs` `SupportedEvents`; `Subscriptions/Handlers/SubscriptionPaymentHandler.cs`; `Subscriptions/Controllers/AdminController.cs` has GET `Edit` only. |
| S-04 | P1 | **Renewal dates are lost.** `PaymentInfo` has no timestamp; `SubscriptionTransactionIndexProvider` stamps every payment with `session.CreatedUtc`. Revenue-by-month, receipts (`IssuedAt = session.CreatedUtc`), and tax-collected reports attribute every renewal to the signup month. | `Subscriptions/Indexes/SubscriptionTransactionIndexProvider.cs`; `DashboardController.Receipt`. |
| S-05 | P1 | **Pay Later subscriptions create no `Transaction`.** `CreatePayLaterEndpoint` writes cache metadata and a `SubscriptionInfo` with `ExpiresAt = null`; `PayLaterTransactionCheckoutHandler` is an `ICheckoutHandler` and is never reached. Docs promise statements, reminders, and online settlement. | `Subscriptions/Endpoints/CreatePayLaterEndpoint.cs`; no reference to `ITransactionManager` in Subscriptions. |
| S-06 | P1 | Tenant provisioning (`ISetupService.SetupAsync`) runs inline in the completion request after payment. No durable job, no retry, no status page; failure raises a workflow event only. The Wizard completion lock expires after 2 minutes. | `TenantOnboardingSubscriptionHandler.CompletedAsync`. |
| S-07 | P2 | `Subscriptions.Core` references `Stripe.Core` (`StripeCurrency`, `StripeLimits`, `StripeMetadata`); `Subscriptions.Core/Money.cs` is a two-decimal duplicate of `Payments.Money`; `InvoiceLineItem.GetLineTotal()` rounds to 2 decimals regardless of currency. "Provider-agnostic" is not true at compile time. | `Subscriptions.Core.csproj`; `Money.cs`; `InvoiceLineItem.cs`. |
| S-08 | P2 | `Subscriptions.Abstractions` duplicates `BillingDurationKey`, `BillingItem`, both JSON converters, `*Flow`, `*FlowStep`, `*Session`, and all nine lifecycle context classes from `Checkout.Abstractions`; Wizard adds a third copy. Every request maps `SubscriptionSession` ↔ `WizardSession`. | Directory listings of the three Abstractions projects; `SubscriptionWizardSessionMapper.cs`. |
| S-09 | P2 | Admin "Edit" is read-only; the subscription list filter offers `Suspended` although nothing sets it; there is no refund, cancel, resend-receipt, or change-plan action. | `Subscriptions/Controllers/AdminController.cs`. |
| S-10 | P2 | Hosted Checkout return path (`SubscriptionsController.CheckoutReturn`) records `Amount = details.AmountTotal` as the first payment with `TransactionId = SubscriptionId` (not the invoice/charge id) and then re-enters the same 60 s cache poll. Works, but diverges from the Payment Elements path and the ledger. | `SubscriptionsController.CheckoutReturnCoreAsync`. |
| S-11 | P2 | `StripePriceSyncService` keys Stripe prices by content item **version** id; every edit/publish creates a new Stripe Price and deactivates the old one. Fine for a small catalog; unbounded growth and Stripe rate-limit exposure for a large one. Stripe subscriptions created earlier keep their old price (correct) but "Sync prices" admin action has no progress/outcome UI. | `Subscriptions.Core/Services/StripePriceSyncService.cs`. |

### 3.3 Transactions, Pay Later, Products, Taxation, Stripe

| ID | Sev | Finding | Evidence |
| --- | --- | --- | --- |
| T-01 | P2 | Admin actions (`RecordPayment`, `MarkPaid`, `Cancel`, `AddNote`) have no state-machine guard (a `Canceled` or `Paid` transaction can be marked paid again, a canceled one can receive a payment) and do not catch `ConcurrencyException` although `TransactionStore.CheckConcurrency = true`, so a conflicting write returns HTTP 500. `RecordPayment` formats with `"0.00"` and does not round `applied` to the currency scale. | `Transactions/Controllers/AdminController.cs`. |
| T-02 | P2 | `TransactionStatus.Refunded`, `Failed`, `Abandoned`, `Pending` are never assigned by any code path. | grep of assignments. |
| T-03 | P2 | The obligation ledger and the payment/refund ledger are two unrelated stores (`Transaction` vs `PaymentAttempt`/`PaymentRefund`) with no shared admin surface. There is no "Payments" or "Refunds" screen anywhere. | `Transactions` module has no views for attempts/refunds; Checkout module has no views at all. |
| P-01 | P2 | Products: single `Price` + `Currency` per item; no multiple prices per product (interval × currency), no trial, no quantity/tiered pricing, no custom amount. `ProductSnapshotContext.Sku/VariantId/Quantity` exist but the default resolver ignores them. | `Products.Core`. |
| X-01 | P2 | Taxation is strong. Gaps: `ExemptionCertificate` and `MerchantTaxRegistration` have stores but no admin UI or recipe/deployment steps; `TaxCollectedReport` lives in Subscriptions and reads only `SubscriptionTransactionIndex`; economic-nexus accumulation is documented as "host responsibility" and nothing accumulates it. | `Taxation` module file list; `Subscriptions/Reports/TaxCollectedReport.cs`. |
| K-01 | P2 | `StripeOptionsConfiguration.Configure` blocks on `ISiteService.GetSettingsAsync(...).GetAwaiter().GetResult()`. Common in Orchard but a deadlock/thread-pool risk under load. | `Stripe/Services/StripeOptionsConfiguration.cs`. |
| K-02 | P2 | `CurrencyScale` and `StripeCurrency` disagree on ISK, UGX, MGA, IQD, LYD, CLF, UYW. Documented as deferred; will matter once one-time card payments through the generic provider go live. | Both classes. |
| K-03 | P2 | Stripe `StripeLimits` minimum-amount table lives in `Subscriptions.Core`; it is provider data and belongs in `Stripe.Core` behind a capability (`MinimumChargeAmount(currency)`). | `Subscriptions.Core/StripeLimits.cs`. |

### 3.4 Security notes

| ID | Finding |
| --- | --- |
| SEC-01 | Anonymous JSON payment endpoints use `DisableAntiforgery`. Safe only because they require `application/json` bodies (CORS preflight protects them). Document that CORS must never allow credentials from foreign origins, and add a same-origin check (`Sec-Fetch-Site`/`Origin`) as defense in depth. |
| SEC-02 | Guest ownership by IP/UA (C-07). Replace with a high-entropy, hashed session token in a data-protected cookie (the decision record already names this as the intended hardening). |
| SEC-03 | Registration password and tenant admin password are stored data-protected in Redis/YesSql for up to a day. Acceptable, but the tenant password must be unprotected only at provisioning time and wiped from `SavedSteps` after use (today it stays in the session document forever). |
| SEC-04 | Tenant name is used as YesSql `TablePrefix` and shell name. The driver validates availability; confirm it also restricts to `[A-Za-z0-9_]` (Orchard's tenant validator) before the value reaches `ShellSettings`. |
| SEC-05 | Webhook processing is correct (signature, event-id lock, processed-event marker committed under lock). Keep it. |

---

## 4. Findings: features versus the market

Legend: ✅ present and working · ◐ partial / present but unreachable · ✗ absent.

### 4.1 Checkout

| Capability | Stripe Checkout / Shopify / Medusa baseline | Here |
| --- | --- | --- |
| Server-authoritative totals, currency-correct rounding | Required | ✅ |
| Durable payment attempt before provider call, idempotent retries | Required | ✅ framework · ◐ not used by subscriptions |
| Provider verification, webhook replay safety | Required | ✅ |
| Refunds (full/partial) with tax allocation, remote refund reconciliation | Required | ✅ service · ✗ no UI, no caller |
| Generic checkout UI (steps, payment method selection, review, confirmation) | Required | ✗ |
| Guest and authenticated checkout with secure guest token | Required | ◐ IP/UA only |
| Multiple providers side by side (card, offline, PayPal…) | Required | ◐ contract only |
| Hosted redirect + embedded elements | Expected | ◐ subscriptions only |
| 3-D Secure / `requires_action` continuation | Required | ◐ client handles it for subscriptions; framework reports `RequiresAction` but has no continuation step |
| Saved payment methods / customer vault | Expected | ✗ |
| Coupons / promo codes | Expected | ✗ |
| Order/receipt/invoice documents with numbering | Expected | ◐ receipts only, unnumbered |
| Admin: payments list, refund action, dispute view | Required | ✗ |

### 4.2 Subscriptions

| Capability | Stripe Billing / Chargebee / Recurly / Paddle baseline | Here |
| --- | --- | --- |
| Plans as content with recurring interval, setup fee, cycle limit, start delay | Required | ✅ |
| Multiple prices per plan (monthly/yearly, multi-currency) | Required | ✗ |
| Free trials, trial-without-card | Required | ◐ `StripeSubscriptionService` supports `TrialEnd` but nothing sets it |
| Custom / pay-what-you-want amount, quantity (seats), tiered/volume pricing, metered usage | Common | ✗ |
| Renewal tracking, `ExpiresAt` advance, grace period | Required | ✗ (S-03/S-04) |
| Dunning: failed renewal → retries, notifications, past-due state, suspend/cancel | Required | ✗ |
| Cancel (immediate / at period end), pause/resume, reactivate | Required | ✗ |
| Upgrade/downgrade with proration | Required | ✗ |
| Update payment method (customer portal) | Required | ✗ |
| Entitlements: what a subscriber is allowed to do (roles, features, limits) with revocation | Required for SaaS | ◐ roles granted once, never revoked |
| Customer portal: subscriptions, invoices/receipts, payment methods | Required | ◐ read-only dashboard |
| Admin: list/filter, detail with timeline, cancel/refund/extend/comp, notes | Required | ◐ list only |
| Events/webhooks for other modules (created, renewed, past due, canceled, expired) | Required | ◐ Stripe workflow events only, no subscription-domain events |
| Revenue reports: MRR, churn, active, expiring | Expected | ◐ revenue/active/expiring exist but on wrong dates (S-04) |
| Tenant-as-product provisioning | Niche (SaaS builders) | ◐ exists, broken password, inline, no lifecycle |

### 4.3 Pay Later / Transactions

| Capability | Baseline (invoice / net-terms products) | Here |
| --- | --- | --- |
| Record obligation with due date, reminders, statements | Required | ✅ (unreachable from subscriptions) |
| Pay online later | Required | ◐ dead end (C-02) |
| Partial payments, write-off, refund, dispute states | Expected | ◐ statuses exist, transitions do not |
| Credit limits / approval before granting terms | Common | ✗ |

### 4.4 Taxation

Meets or exceeds typical in-CMS engines (Lombiq OCC, nopCommerce): jurisdiction hierarchy, rules with
effective dates, tables, inclusive/exclusive, compound, reverse charge, exemptions, nexus, snapshots,
refund allocation, recipes/deployments. Missing only the admin UI for exemption certificates and
merchant registrations, per-jurisdiction reports, and an external-provider adapter (Stripe Tax /
Avalara) implementing `ITaxDeterminationProvider`.

---

## 5. Findings: tests and docs

### 5.1 Tests

479 tests pass in the commerce area. Coverage is **deep where the code is pure** (tax engine, refund
reconciliation, obligation settlement, money helpers, report aggregation) and **absent where money
actually moves**:

| Untested area | Why it matters |
| --- | --- |
| `StripeCheckoutPaymentProvider` (Begin/Verify/Cancel/Refund) | The generic card provider has zero tests; `Stripe/` has only `StripeCurrencyTests`. |
| `StripeSubscriptionService`, `StripePaymentIntentService`, `StripeCheckoutService.BuildOptions`, `StripeConnectService` | Idempotency, schedule phase math, `payment_intent_unexpected_state` fallback, webhook provisioning. |
| `CheckoutSessionStore` / `SubscriptionSessionStore` / `WizardSessionStore` ownership | The only guard between two guests' sessions. |
| The four Stripe endpoints and `SubscriptionsController.CheckoutReturn` | Authorization, validation, idempotency keys, metadata contract with the webhook (`sessionId`). |
| `CheckoutReconciliationBackgroundTask`, `PaymentAttemptStore`, `PaymentRefundStore` | Never exercised against YesSql. |
| `TenantOnboardingStepSubscriptionFlowDisplayDriver` and the protected-password round trip | Would have caught S-02. |
| `SubscriptionWizardSessionMapper` round trip (`BillingItems` through `Data`) | Silent data loss risk. |
| Transactions controllers (admin + customer) | State transitions, concurrency, authorization. |
| Any browser/JS or end-to-end scenario | Payment Elements, hosted return, Pay Later click path. |
| Concurrency: two nodes completing one session, replayed webhooks against a real store | Distributed-lock paths are unit-faked only. |

### 5.2 Documentation

The Docusaurus pages are well written but describe the **intended** architecture rather than the
shipped one. Statements that are currently false or misleading:

| Page | Statement | Reality |
| --- | --- | --- |
| `checkout.md` | "Built-in payment providers … Pay Later … Stripe … so *any* checkout can collect and refund a card payment" | No checkout uses them (C-01). |
| `checkout.md` | "Create a session with `ICheckoutSessionStore.NewAsync` … and drive the `CheckoutFlow`" | No component renders or drives a `CheckoutFlow`. |
| `subscriptions.md` | "a consumer of the checkout framework" / "Depends on `CrestApps.OrchardCore.Checkout`" | Feature dependency only; runtime pipeline is separate (S-01). |
| `pay-later.md` | "The customer sees it on their **My Transactions** statement and can **pay it online** later" | Subscriptions Pay Later creates no transaction (S-05); online pay is a dead end (C-02). |
| `transactions.md` | Online settlement described end to end | Unreachable (C-02). |
| `subscriptions.md` | Admin filter documents `Suspended`; admin can "manage subscriptions" | Nothing sets `Suspended`; admin is read-only (S-09). |
| `ecommerce-plan.md` inventory | "Pay Later … checkout handler now creates idempotent outstanding Transactions entries"; "Subscriptions remains a recurring-billing consumer [of Checkout]" | True only for the unused generic path. |
| All modules except Commerce | No `README.md` | Every other CrestApps module ships one. |

---

## 6. Answers to the review questions

### 6.1 Enough features for a powerful checkout + subscription suite?

No. The money-safety core is at or above industry standard; the product surface is roughly 40 % of a
minimum viable subscription business (see §4.2) and the one shipped flow bypasses the safe core. The
three product requirements you named map as follows:

- **"Subscribe to any product at any price point."** Needs a price model richer than one
  `ProductPart.Price`: multiple recurring prices per product (interval × currency), optional setup fee
  per price, trials, quantity, and an optional customer-chosen amount with min/max. Stripe supports
  inline `price_data` on subscription creation, so the per-content-version Stripe Price sync can become
  an optimization instead of a prerequisite. Plan §8 Phase 5.
- **"Subscribe to purchase a site (tenant)."** Exists but must be fixed (S-02), moved to a durable
  background provisioning job with a status page (S-06), and tied to the subscription lifecycle
  (suspend the tenant on past-due, disable on cancel, re-enable on reactivation). Plan §8 Phase 4.
- **"User-level / member-only subscriptions later."** Needs an entitlement layer: a subscription owns
  a set of grants (roles, feature flags, limits) that are applied on activation and revoked on
  expiry/cancel, plus a content gate. This branch already has a Content Access Control module; the
  entitlement layer should feed it rather than invent a second gate. Plan §8 Phase 6 lays the
  contracts now so it is additive later.

### 6.2 Is there a better project split?

Yes. Keep the Abstractions/Core/Module triad convention, but change **which** projects exist and
the dependency direction:

```text
Payments.Abstractions      money helpers, provider events (webhooks)             ← unchanged
Transactions.Abstractions  ALL durable money records: PaymentAttempt, PaymentRefund,
                           Transaction (obligation), TransactionEvent, financial-document policy
Transactions.Core          stores + managers for the above (moved from Checkout.Core / Commerce.Abstractions)
Checkout.Abstractions      session/flow/steps/invoice, provider contracts (one-time + recurring), refund/reconciliation contracts
Checkout.Core              orchestration engine: begin/verify/complete/cancel, reconciliation, refund service, tax bridge
Checkout (module)          the ONLY checkout UI: wizard definition, payment step, provider panels, confirmation, admin screens
Products.Core / Products   catalog + prices (multi-price), currencies
Subscriptions.Abstractions subscription DOMAIN only: Subscription record, status, entitlements, events   (flow types deleted)
Subscriptions.Core         plan → billing items, lifecycle service, dunning, Stripe-neutral
Subscriptions (module)     plans UI, portal, admin, reports; ICheckoutHandler contributions
Subscriptions.Tenants      (new module) tenant provisioning job + lifecycle hooks (from TenantOnboarding feature)
Stripe.Core / Stripe       provider adapter: ICheckoutPaymentProvider + ICheckoutRecurringPaymentProvider + webhook → events
PayLater                   provider adapter (unchanged shape)
Taxation.*                 unchanged
Receipts.*                 renderer only; becomes a feature of Transactions (or keep separate, but Transactions owns the policy)
Commerce                   menu + composition only (drop Commerce.Abstractions; policy contracts move to Transactions.Abstractions)
Wizard.*                   generic multi-step host; Checkout is a wizard consumer, Subscriptions no longer has its own flow
```

Rules enforced by architecture tests:

1. `Subscriptions.*` must not reference `Stripe.*`.
2. `Checkout.*` references `Transactions.Abstractions/Core` (ledger), never the reverse.
3. Provider modules (`Stripe`, `PayLater`) reference `Checkout.Abstractions` only.
4. Exactly one flow/session/step model: `WizardSession` for UI state, `CheckoutSession` for money
   state, linked by session id. `SubscriptionSession` is deleted.

Why this split serves the future: Orders/Carts (roadmap) will consume the same Checkout module and
Transactions ledger; a storefront becomes another `ICheckoutHandler` + wizard definition; a member-only
site becomes another entitlement consumer; a second gateway is one adapter project.

### 6.3 What should this version add to be robust?

Ordered by value ÷ effort (details in §8):

1. One checkout engine under every purchase, with durable attempts, async completion, and a generic UI.
2. Subscription lifecycle: status, renewal advance, dunning, cancel/pause/resume, entitlement revocation.
3. Tenant provisioning as a durable job; fix the password bug; tie tenant state to subscription state.
4. Pricing: multiple prices per product, trials, quantity, custom amount, coupons (basic).
5. Customer portal actions (cancel, update card, download receipts) and admin actions (cancel, refund,
   extend, comp, notes) on the same subscription record.
6. Financial records surface: payments, refunds, obligations, receipts in one Transactions area with
   numbering.
7. Operational tooling: subscription-domain events + workflow activities, reconciliation dashboards,
   health checks, structured logs with correlation ids.

### 6.4 Is the code sound and production-worthy for payments and recurring payment?

- **Sound:** money representation, tax engine, refund ledger and reconciliation, webhook
  de-duplication, Stripe idempotency keys, minor-unit handling, provider-authoritative verification.
- **Not production-worthy as shipped:** the recurring flow (cache-as-truth, no lifecycle, lost renewal
  dates), the tenant flow (password bug), Pay Later on subscriptions (no ledger entry), online
  settlement (dead end), and the absence of any admin money screen. A merchant could take money today
  and have no way to refund, cancel, or see renewals from the CMS.
- **Tests:** enough to trust the pure engines; not enough to trust the integration. There is no test
  that a customer can complete a purchase end to end, and the generic Stripe provider has none.

### 6.5 Docs

Up to date in tone and structure, out of date in truth. §11 lists every page and the statements to
change. Module READMEs are missing for seven of the eight modules.

### 6.6 Relationship to `docs/ecommerce-plan.md`

The roadmap's "closed" foundation items remain valid, but its inventory overstates connection: G3
(Transactions integration) and the Phase 0 claim "reusable payment checkout exists" assume a driven
Checkout framework. After this plan's Phases 0–4, update the roadmap's inventory table and start its
Phase 1 (Customers) on top of the unified engine. Do not start Orders/Carts before then; they would
have to be rewritten when `CheckoutSession` gains a real lifecycle and the ledger moves to Transactions.

---

## 7. Target design (to-be)

### 7.1 One checkout engine

`ICheckoutEngine` (new, `Checkout.Core`) is the single orchestration entry point. All money-moving
operations go through it; controllers, endpoints, and background tasks never touch provider services.

```csharp
public interface ICheckoutEngine
{
    Task<CheckoutSession> StartAsync(StartCheckoutRequest request, CancellationToken ct = default);
    Task<PaymentBeginOutcome> BeginPaymentAsync(string sessionId, string providerKey, BeginPaymentOptions options, CancellationToken ct = default);
    Task<CheckoutCompletionResult> TryCompleteAsync(string sessionId, CancellationToken ct = default);
    Task CancelAsync(string sessionId, string reason, CancellationToken ct = default);
    Task ExpireAsync(string sessionId, CancellationToken ct = default);
}
```

Behavior:

- `StartAsync` runs `ICheckoutHandler.Activating/Activated` (existing), builds the invoice, persists the
  session as `Pending`, and returns it. Callers: the Checkout wizard definition (public UI), the
  Transactions "Pay" action, the Subscriptions plan signup, and (later) Orders.
- `BeginPaymentAsync` computes obligations (`CheckoutObligations.GetExpectedObligationIds`), creates one
  `PaymentAttempt` per obligation (`Created`), selects the provider by key, checks
  `PaymentProviderCapabilities` against the invoice (recurring, combined, hosted vs embedded), calls
  `BeginAsync`, persists `ProviderReference`, sets the attempt `Pending` and the session
  `AwaitingProvider`, and returns what the client needs (`ClientSecret`, `RedirectUrl`, `RequiresAction`).
- `TryCompleteAsync` acquires the session lock, reconciles (`ICheckoutReconciliationService`), and if
  fully settled runs `Completing` → status `Completed` → commit → `Completed` handlers. If outstanding,
  sets `PaymentPending` and returns `Pending`; if any obligation failed, sets `Failed`, runs `Failed`
  handlers, and compensates other succeeded attempts through `CancelAsync` **via the refund service**
  (fix C-05). No `Task.Delay` loops: the client polls `TryCompleteAsync` through an endpoint, webhooks
  call it, and the reconciliation task calls it.
- `ExpireAsync` is driven by a background task for sessions older than `CheckoutSettings.SessionLifetime`
  with no succeeded attempts; expired sessions cancel pending attempts.

### 7.2 Recurring payments through the same engine

Add `ICheckoutRecurringPaymentProvider` (additive, like the refund provider):

```csharp
public interface ICheckoutRecurringPaymentProvider
{
    string Key { get; }
    Task<RecurringSetupResult> SetupAsync(RecurringSetupContext ctx, CancellationToken ct = default);      // customer + payment method (SetupIntent) or hosted session
    Task<RecurringBeginResult> BeginAsync(RecurringBeginContext ctx, CancellationToken ct = default);      // creates the remote subscription(s) for one BillingDurationKey group
    Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext ctx, CancellationToken ct = default); // reads the remote subscription's latest invoice
    Task<RecurringCancelResult> CancelAsync(RecurringCancelContext ctx, CancellationToken ct = default);   // immediate or at period end
    Task<RecurringUpdateResult> UpdateAsync(RecurringUpdateContext ctx, CancellationToken ct = default);   // change price/quantity with proration policy
}
```

Stripe implements it with the existing `IStripeSubscriptionService`, `IStripeSetupIntentService`,
`IStripeCheckoutService`, and `IStripeCustomerService`; Pay Later implements it by recording an
obligation per cycle. The Subscriptions module stops owning Stripe endpoints; the Checkout module
exposes provider-neutral endpoints (`checkout/{sessionId}/payment/begin`, `.../complete`,
`.../continue`) and the Stripe module contributes the browser panel (Payment Elements or hosted
redirect) as an `IDisplayDriver<CheckoutFlowPaymentMethod>`.

### 7.3 Subscription domain record

Replace "a completed `SubscriptionSession` with metadata" by a first-class, durable `Subscription`
document (YesSql, own collection) created by the Subscriptions `ICheckoutHandler.CompletedAsync`:

```csharp
public sealed class Subscription : CatalogItem
{
    public string SubscriptionNumber;                 // tenant-scoped public token (IFinancialDocumentNumberGenerator)
    public CustomerOwner Owner;                       // user or guest
    public string PlanContentItemId, PlanContentItemVersionId, PriceId;
    public SubscriptionStatus Status;                 // Trialing, Active, PastDue, Paused, Canceled, Expired, Incomplete
    public DateTime StartedUtc; public DateTime CurrentPeriodStartUtc, CurrentPeriodEndUtc;
    public DateTime? TrialEndUtc, CanceledUtc, CancelAtUtc, PausedUtc, EndedUtc;
    public string ProviderKey, ProviderSubscriptionId, ProviderCustomerId; public GatewayMode GatewayMode;
    public string CheckoutSessionId;                  // origin
    public IList<SubscriptionLine> Lines;             // snapshot: item, description, quantity, unit price, currency, interval
    public IList<SubscriptionEntitlement> Entitlements; // roles/feature keys/limits granted
    public IList<SubscriptionEvent> Events;           // timeline
    public int RenewalCount; public int? BillingCycleLimit; public DateTime? NextBillingUtc;
}
```

`ISubscriptionLifecycleService` owns transitions (`ActivateAsync`, `RecordRenewalAsync`,
`MarkPastDueAsync`, `CancelAsync(atPeriodEnd)`, `PauseAsync`, `ResumeAsync`, `ExpireAsync`,
`ChangePlanAsync`) and raises `ISubscriptionEventHandler` events. Every transition writes a
`SubscriptionEvent` and, when money moved, a `PaymentAttempt` (renewal) through the ledger so
reports read one source. Provider webhooks map to lifecycle calls; they never write metadata bags.

### 7.4 Ledger ownership

Move `PaymentAttempt`, `PaymentRefund`, their stores, indexes, and migrations from `Checkout.*` to
`Transactions.*` (namespace move, no remodel). `Transaction` (obligation) stays. Add
`IPaymentAttemptStore.GetByOwnerAsync` and an admin **Commerce → Transactions** area with three tabs:
Payments (attempts), Refunds, Outstanding (obligations). Move `IFinancialDocumentPolicy` and
`IFinancialDocumentNumberGenerator` from `Commerce.Abstractions` to `Transactions.Abstractions`, ship a
YesSql-backed sequence generator (tenant-scoped, lock-protected), and delete `Commerce.Abstractions`.

### 7.5 Prices

Add `ProductPricePart` (attachable, repeatable entries) alongside `ProductPart`:

```csharp
public sealed class ProductPrice
{
    public string PriceId;            // stable id (IdGenerator), the key checkout and Stripe correlate on
    public string Currency; public decimal Amount;
    public PriceKind Kind;            // OneTime, Recurring
    public int? BillingDuration; public DurationType? DurationType; public int? BillingCycleLimit; public int? StartDayDelay;
    public decimal? SetupFee; public string SetupFeeDescription;
    public int? TrialDays;
    public bool AllowCustomAmount; public decimal? MinimumAmount, MaximumAmount;
    public bool AllowQuantity; public int? MaximumQuantity;
    public bool IsDefault; public bool IsActive; public DateTime? EffectiveFromUtc, EffectiveToUtc;
}
```

`IPriceResolver` resolves `(product, priceId, quantity, customAmount, currency)` into a `PriceResult`
carrying the recurring plan. `SubscriptionPart` keeps only non-price plan behavior (content types to
collect, entitlements) and becomes optional: a product with a recurring price is subscribable. Stripe
prices are created lazily per `(PriceId, Amount, Currency, Interval)` hash with `lookup_key`, or passed
inline as `price_data` for custom amounts.

### 7.6 Entitlements (contracts now, member-site feature later)

`SubscriptionEntitlement { Kind: Role | Feature | Limit, Key, Value }` on the plan (editor) and
snapshotted on the subscription. `IEntitlementApplier` implementations: `RoleEntitlementApplier`
(assign/remove Orchard roles), `TenantEntitlementApplier` (enable/disable the provisioned tenant,
apply feature profile). Applied on `Active`/`Trialing`, revoked on `Canceled`/`Expired`/`PastDue`
(configurable grace). A later `Subscriptions.MemberAccess` feature adds `IContentAccessRule` bridging
to the Content Access Control module.

---

## 8. Implementation plan

Conventions for every task (from `CLAUDE.md`, the hardening plan, and current code):

- Forward-only. No compatibility shims; delete replaced code. Bump nothing below `VersionPrefix` 3.0.0;
  add changelog bullets to `src/CrestApps.Docs/docs/changelog/3.0.0.md`.
- Money is `decimal` + ISO currency; round with `Money.Round`/`CurrencyScale`; provider minor units only
  inside the provider adapter.
- Use `IClock`, `IdGenerator.GenerateId()`, `DocumentCatalog<T, TIndex>` for stores, `IDistributedLock`
  for cross-node serialization, `ISession.SaveChangesAsync` inside locks when a state transition must be
  visible to concurrent readers.
- Public types get XML docs; new admin screens use display drivers + shapes; new settings use
  `ISite` display drivers; new user-addable entities get recipe + deployment + schema steps.
- Tests: xUnit v3 in `tests/CrestApps.OrchardCore.Tests`, run with
  `dotnet <Tests>.dll -filter "/*/Namespace*"`. Every task lists its tests; a task is not done until
  they pass and `dotnet build -c Release -warnaserror` is clean.
- Docs: update the Docusaurus page(s) and add/refresh the module `README.md` in the same task.

Phases are ordered by dependency. Effort labels: S (< 1 day), M (1–3 days), L (3–7 days), XL (> 1 week).

### Phase 0 — Stop the bleeding (P0 bugs that need no redesign) — M

| Task | Files | Work | Acceptance / tests |
| --- | --- | --- | --- |
| 0.1 Tenant admin password | `Subscriptions.Core/Handlers/TenantOnboardingSubscriptionHandler.cs`, `Subscriptions/Drivers/Steps/TenantOnboardingStepSubscriptionFlowDisplayDriver.cs`, `Subscriptions.Core/Models/TenantOnboardingStep.cs` | Rename the stored field to `ProtectedAdminPassword`. In `CompletedAsync`, unprotect with `IDataProtectionProvider.CreateProtector(TenantOnboardingStepSubscriptionFlowDisplayDriver.ProtectorPurpose)` (move the purpose constant to `Subscriptions.Core/SubscriptionConstants`). After `SetupAsync` succeeds or fails terminally, remove the protected value from `SavedSteps` and save the session. | New test: driver-protected value round-trips into `SetupContext.Properties[AdminPassword]` as the raw password; the session no longer contains the secret after completion. Existing `TenantOnboardingSubscriptionHandlerTests` updated to seed a protected value. |
| 0.2 Renewal timestamps | `Subscriptions.Core/Models/PaymentInfo.cs` (+ `CreatedUtc`), `Subscriptions/Handlers/SubscriptionPaymentHandler.cs`, `Subscriptions/Indexes/SubscriptionTransactionIndexProvider.cs`, `Subscriptions/Controllers/DashboardController.cs` (receipt `IssuedAt`), `Subscriptions/Endpoints/CreatePayLaterEndpoint.cs`, `SubscriptionsController.CheckoutReturnCoreAsync` | Stamp `PaymentInfo.CreatedUtc = clock.UtcNow` everywhere a `PaymentInfo` is created; index uses it (fallback `session.CreatedUtc` for legacy rows). | Test: renewal recorded on day 31 indexes under day 31; receipt shows the payment date. |
| 0.3 Renewal advances expiry | `Subscriptions/Handlers/SubscriptionPaymentHandler.cs` | On `SubscriptionCycle`, find the `SubscriptionInfo` by `SubscriptionId`, set `ExpiresAt = BillingSchedule.GetNextBillingDate(previous ExpiresAt ?? now, key)`, persist. | Test: two renewals produce `ExpiresAt` two cycles ahead; duplicate webhook does not advance twice. |
| 0.4 Failed-renewal + cancellation webhooks | `Stripe/Endpoints/CreateWebhookEndpoint.cs` (`SupportedEvents` + dispatch), `Payments.Abstractions` (new `SubscriptionPaymentFailedContext`, `SubscriptionStatusChangedContext`, `IPaymentEvent` members with no-op defaults in `PaymentEventBase`), `Subscriptions/Handlers/SubscriptionPaymentHandler.cs` | Handle `invoice.payment_failed`, `customer.subscription.updated`, `customer.subscription.deleted`; map to a new `SubscriptionInfo.Status` (`Active`, `PastDue`, `Canceled`) and `CanceledAt`. Reconnecting Stripe must add the new event types to the provisioned webhook (`StripeConnectService` reads `SupportedEvents`, so this is automatic on reconnect; document it). | Dispatch tests for the three events; handler tests for status transitions; idempotent on replay. |
| 0.5 Compensation refunds through the ledger | `Stripe/Services/StripeCheckoutPaymentProvider.cs`, `Checkout.Core` | `CancelAsync` on a succeeded intent returns `PaymentCancelResult.RequiresRefund` (new flag); the caller (engine, Phase 1) issues the refund through `ICheckoutRefundService`. Until Phase 1 lands, keep the direct refund but also write the `PaymentRefund` record via the refund store with the same idempotency key. | Test: cancel of a succeeded attempt produces a `PaymentRefund` correlated by provider reference; the `charge.refunded` webhook matches it instead of quarantining. |
| 0.6 Transactions admin guards | `Transactions/Controllers/AdminController.cs`, new `Transactions.Core/Services/TransactionStateMachine.cs` | Centralize allowed transitions (`Outstanding/PartiallyPaid → Paid/Canceled/PartiallyPaid`, `Paid → Refunded`, no transitions out of `Canceled`/`Refunded`); round `applied` with `CurrencyScale.Round`; format with `CurrencyScale.Format`; catch `ConcurrencyException` → warning notifier + redirect. | Tests for each illegal transition and for the concurrency path. |
| 0.7 Docs truth pass | `checkout.md`, `subscriptions.md`, `pay-later.md`, `transactions.md`, `docs/ecommerce-plan.md` | Until Phase 1 ships, add an explicit "Current status" admonition to each page stating what is wired and what is not (§5.2 table). Remove the `Suspended` filter option or document it as reserved. | Docs build passes; no page claims an unreachable path. |

### Phase 1 — One checkout engine with a real UI — XL

| Task | Files | Work | Acceptance / tests |
| --- | --- | --- | --- |
| 1.1 Ledger relocation | Move `Checkout.Abstractions/Models/PaymentAttempt*.cs`, `PaymentRefund*.cs`, `Services/IPaymentAttemptStore.cs`, `IPaymentRefundStore.cs` → `Transactions.Abstractions`; `Checkout.Core/Services/PaymentAttemptStore.cs`, `PaymentRefundStore.cs`, `Indexes/PaymentAttemptIndex*.cs`, `PaymentRefundIndex*.cs` → `Transactions.Core`; split `CheckoutMigrations` (session index stays; attempt/refund tables move to `TransactionMigrations`). Move `Commerce.Abstractions/FinancialDocuments/*` → `Transactions.Abstractions/FinancialDocuments`; delete `Commerce.Abstractions`. Update `ReceiptsOnlyFinancialDocumentPolicy` registration to the Transactions module. | Namespaces become `CrestApps.OrchardCore.Transactions.Models/Services`. `Checkout.Core` references `Transactions.Core`. `Transactions` module must not reference `Checkout.Core`; move `TransactionSettlementCheckoutHandler` to the Checkout module under `[RequireFeatures(Transactions)]`. Update `CommerceModuleBoundaryTests`, `ProductsModuleBoundaryTests`, `MoneyTypeContractTests`, `CheckoutReferenceContractTests`. | Solution builds; all 479 tests still pass after namespace updates; new architecture test asserts the dependency direction in §6.2. |
| 1.2 `ICheckoutEngine` | New `Checkout.Abstractions/Services/ICheckoutEngine.cs` + request/result types; `Checkout.Core/Services/DefaultCheckoutEngine.cs` | Implement §7.1. Session lock key `CHECKOUT_SESSION_{id}` (10 s timeout, 2 min expiry). `TryCompleteAsync` commits status + attempts under the lock (`ISession.SaveChangesAsync`). Compensation on partial failure uses `ICheckoutRefundService` for succeeded attempts and provider `CancelAsync` for pending ones; results recorded on attempts (`FailureReason`, new `CompensationState`). Remove the 60-iteration `Task.Delay` loop from `PaymentCheckoutHandler.CompletingAsync` (it now only validates the invoice; settlement is the engine's job). | Engine tests: start→begin→verify→complete happy path per provider capability; partial failure compensates; concurrent `TryCompleteAsync` calls complete once; expired session cancels pending attempts; `RequiresAction` returns continuation data without settling. Use `InMemoryPaymentAttemptStore` and fakes already in `tests/Checkout`. |
| 1.3 Async completion | `Checkout/Tasks/CheckoutReconciliationBackgroundTask.cs`, new `CheckoutExpirationBackgroundTask`, new `Checkout/Handlers/CheckoutPaymentEventHandler.cs` (`IPaymentEvent`) | Reconciliation task calls `ICheckoutEngine.TryCompleteAsync` per session (so background settlement fulfills). Payment events (`payment_intent.succeeded`, `invoice.payment_succeeded` with `checkout_session_id` metadata) call `TryCompleteAsync`. Expiration task expires abandoned sessions after `CheckoutSettings.SessionLifetime` (new, default 24 h). | Tests: a session whose attempt succeeds only at the provider is completed by the sweep and runs `CompletedAsync` handlers exactly once; webhook-triggered completion is idempotent with the sweep. |
| 1.4 Recurring provider contract | `Checkout.Abstractions/Services/ICheckoutRecurringPaymentProvider.cs` + contexts/results; `PaymentProviderCapabilities.SupportsSetupBeforeCharge`, `MinimumChargeAmount(currency)` hook via `ICheckoutPaymentProvider.GetLimitsAsync` | Implement §7.2. Engine `BeginPaymentAsync` routes the one-time obligation to `ICheckoutPaymentProvider` and each recurring group to `ICheckoutRecurringPaymentProvider`; refuses combinations the capabilities cannot express (e.g., hosted checkout + separate setup fee) with a typed error the UI renders. | Contract tests with fakes; capability-audit test extended to assert `SupportsRecurringPayments` ⇔ implements the recurring interface. |
| 1.5 Stripe recurring adapter | `Stripe/Services/StripeRecurringPaymentProvider.cs` (new), reuse `StripeSubscriptionService`, `StripeSetupIntentService`, `StripeCustomerService`, `StripeCheckoutService`; move `StripeLimits` from `Subscriptions.Core` → `Stripe.Core` | `SetupAsync` creates/reuses the Stripe customer (metadata `checkout_session_id`, `owner_id`) and a SetupIntent (Payment Elements) or a hosted Checkout Session (hosted mode). `BeginAsync` creates the subscription with `price_data` or a looked-up price, `payment_behavior=default_incomplete`, metadata `checkout_session_id`, `obligation_id`, `attempt_id`; returns the invoice PaymentIntent client secret when confirmation is required. `VerifyAsync` reads the subscription's latest invoice status. `CancelAsync` / `UpdateAsync` map to Stripe subscription cancel/update with proration behavior. Delete the four endpoints and `StripeCheckoutRequestFactory` from Subscriptions; keep `HostedCheckoutReturnValidator` logic inside the provider's hosted return verification. | Tests against `IStripe*Service` fakes for every branch; hosted return validation tests move with the code; idempotency keys asserted deterministic. |
| 1.6 Pay Later recurring adapter | `PayLater/Services/PayLaterCheckoutPaymentProvider.cs` | Implement `ICheckoutRecurringPaymentProvider`: `BeginAsync` records one attempt per group with a locally generated reference; `PayLaterTransactionCheckoutHandler` creates one `Transaction` per succeeded attempt with `DueUtc` from `NetTermDays`, `ReferenceType`/`Id` from the session, and (new) `RecurringPlan` metadata so the reminder text can say "for the period …". A `PayLaterRenewalBackgroundTask` creates the next-cycle obligation when `CurrentPeriodEndUtc` passes (Phase 2 hooks it to the lifecycle service). | Existing `PayLaterTransactionCheckoutHandlerTests` extended for recurring groups. |
| 1.7 Checkout UI (wizard consumer) | Checkout module: `Services/CheckoutWizardDefinition.cs` (`IWizardDefinition`, `WizardType = "Checkout"`), `Handlers/CheckoutWizardHandler.cs` (bridges `IWizardHandler` → `ICheckoutHandler`, replacing `SubscriptionWizardHandler`), `Services/CheckoutWizardSessionStore.cs` (maps `WizardSession` ↔ `CheckoutSession` by shared `SessionId`, without copying steps: `CheckoutSession` becomes the persisted session for wizard type `Checkout`), `Drivers/PaymentStepCheckoutFlowDisplayDriver.cs`, `Drivers/ReviewStepCheckoutFlowDisplayDriver.cs`, `Drivers/ContactStepCheckoutFlowDisplayDriver.cs` (guest email/name → `CheckoutContactInfo`), `Views/*`, endpoints `checkout/{sessionId}/payment/begin`, `.../complete`, `.../status` (JSON, anonymous, rate-limited, same-origin check), `Assets/js/checkout-payment.js` | Reuse `Subscriptions/Views/PaymentMethods.Edit.cshtml`, `SubscriptionFlowStepper.cshtml`, `PaymentStepInvoice.Edit.cshtml`, `payment-option-selection.js` by moving them to the Checkout module and generalizing names. Payment method list comes from `ICheckoutPaymentProvider` registrations (delete `PaymentMethodOptions`). Each provider contributes an `IDisplayDriver<CheckoutFlowPaymentMethod>` (Stripe: Payment Elements + hosted; Pay Later: confirmation panel). The client flow: select method → POST begin → provider-specific confirmation (Stripe.js `confirmPayment`/`confirmSetup`, or redirect) → POST complete → poll status until `Completed`/`Failed`/`Pending` → redirect to the wizard confirmation route. | Playwright or Razor-integration smoke test of the public checkout for a Pay Later purchase (no external calls) and a Stripe purchase with `stripe-mock` if CI allows; unit tests for endpoints (authorization, ownership, throttling, same-origin). |
| 1.8 Guest ownership token | `Checkout.Core/Services/CheckoutSessionStore.cs`, `Wizard.Core/Services/WizardSessionStore.cs`, `Wizard/Services/WizardResumeCookieManager.cs` | Replace IP/UA with a 32-byte random token stored hashed (SHA-256) on the session and delivered in the existing data-protected resume cookie. Authenticated owner check unchanged. Keep IP/UA as audit fields only. | Tests: another visitor with the same IP/UA cannot resume; the token holder can; token rotates on completion. |
| 1.9 Transactions online settlement | `Transactions/Controllers/TransactionController.cs` `Pay` | Start the session through `ICheckoutEngine.StartAsync` and redirect to the Checkout wizard route. | Integration test: Pay → checkout → complete settles the transaction (existing handler tests already cover settlement math). |
| 1.10 Admin money screens | Transactions module: `Controllers/PaymentsAdminController.cs`, `RefundsAdminController.cs`, views; refund request form posting to `ICheckoutRefundService.RequestRefundAsync`; manual-review resolution actions for `PendingManualReview` refunds | Commerce → Transactions gets tabs Payments / Refunds / Outstanding. Permission `ManageRefunds` (new). | Controller tests for authorization and for refund request validation; refund service already tested. |

### Phase 2 — Subscription domain and lifecycle — XL

| Task | Files | Work | Acceptance / tests |
| --- | --- | --- | --- |
| 2.1 Domain model | `Subscriptions.Abstractions/Models/Subscription.cs`, `SubscriptionStatus.cs`, `SubscriptionLine.cs`, `SubscriptionEvent.cs`, `SubscriptionEntitlement.cs`; `Subscriptions.Core/Indexes/SubscriptionIndex.cs` (rewrite: `OwnerId`, `Status`, `PlanContentItemId`, `ProviderSubscriptionId`, `CurrentPeriodEndUtc`, `NextBillingUtc`), store `SubscriptionStore : DocumentCatalog`, manager, migrations | Implement §7.3. Delete `SubscriptionSession`, `SubscriptionsMetadata`, `SubscriptionInfo`, `PaymentInfo`, `PaymentsMetadata`, `StripeMetadata`, `SubscriptionPaymentSession`, `Subscriptions.Abstractions` flow/step/session/billing duplicates, `SubscriptionSessionStore`, `SubscriptionWizardSessionMapper`, `SubscriptionWizardFlowFactory`, `SubscriptionWizardSessionStore`, `SubscriptionWizardHandler`, `SubscriptionWizardFlowDisplayDriver`. | Architecture test: `Subscriptions.*` has no reference to `Stripe.*`; no type named `*Session` in Subscriptions.Abstractions. |
| 2.2 Subscriptions as a checkout consumer | `Subscriptions.Core/Handlers/*` re-based on `CheckoutHandlerBase`: `PlanBillingCheckoutHandler` (adds billing items from `IPriceResolver`), `UserRegistrationCheckoutHandler`, `ContentCheckoutHandler`, `SubscriptionActivationCheckoutHandler` (`CompletedAsync` creates the `Subscription` from settled attempts and the invoice) | `ServicePlansController` "Subscribe" starts `ICheckoutEngine.StartAsync(new StartCheckoutRequest { ReferenceType = "SubscriptionPlan", ReferenceId = plan.ContentItemId, ReferenceVersionId = version, PriceId, Quantity, CustomAmount })` and redirects to the Checkout wizard. Delete `SubscriptionsController` legacy routes except a 301 for `Subscription/Signup/{id}`. | Handler tests migrate from `PaymentSubscriptionHandlerTests`, `SubscriptionTaxIntegrationTests`, `SubscriptionSessionInvoicePersistenceTests`; new test: completed checkout with a recurring attempt yields an `Active` subscription with correct period dates and lines. |
| 2.3 Lifecycle service | `Subscriptions.Abstractions/Services/ISubscriptionLifecycleService.cs`, `ISubscriptionEventHandler.cs`; `Subscriptions.Core/Services/DefaultSubscriptionLifecycleService.cs` | Transitions per §7.3 with a distributed lock per subscription; each writes a `SubscriptionEvent`; renewal writes a `PaymentAttempt` (Succeeded, `ObligationId = subscription.ItemId + ":" + periodStart`) so revenue reports read the ledger; dunning state `PastDue` with `GraceEndsUtc` from settings; `ExpireAsync` when `BillingCycleLimit` reached or grace elapsed. | State-machine tests for every legal and illegal transition; idempotency on replayed renewal. |
| 2.4 Provider → lifecycle bridge | `Subscriptions.Core/Handlers/SubscriptionPaymentEventHandler.cs` (`IPaymentEvent`) | Map `invoice.payment_succeeded` (cycle) → `RecordRenewalAsync`; `invoice.payment_failed` → `MarkPastDueAsync`; `customer.subscription.deleted` → `CancelAsync(immediate, source=provider)`; `customer.subscription.updated` → sync `CurrentPeriodEnd`, status; correlate by `ProviderSubscriptionId` (index), never by cache. | Webhook dispatch + handler tests using the Stripe event fixtures already in `CreateWebhookEndpointDispatchTests`. |
| 2.5 Pay Later renewals | `PayLater/Tasks/PayLaterRenewalBackgroundTask.cs` | For `Active` Pay Later subscriptions whose period ended, create the next obligation (`Transaction`) and call `RecordRenewalAsync` with a Pay Later attempt; unpaid past `NetTermDays` + grace → `MarkPastDueAsync`; settled obligation (via Transactions settlement handler) → `ResumeAsync`. | Tests with `TestClock`. |
| 2.6 Customer portal | `Subscriptions/Controllers/PortalController.cs` (replaces `DashboardController`), views, `SubscriberDashboardDisplayDriver` rewrite | Actions: cancel at period end / immediately (per plan setting), resume, pause (if plan allows), update payment method (Stripe `SetupIntent` through the recurring provider `SetupAsync` with `UpdatePaymentMethod` intent), view/print receipts (from ledger + `IReceiptService`), view outstanding transactions. Authorization: owner only. | Controller tests for authorization and each action's lifecycle call; receipt test uses attempt dates. |
| 2.7 Admin | `Subscriptions/Controllers/AdminController.cs` detail page with timeline; actions cancel/resume/extend period/comp (free period)/refund last payment (through refund service)/add note/re-send receipt; filters by status, plan, provider, owner; bulk cancel | Replace read-only `Edit`. | Controller + query service tests. |
| 2.8 Subscription domain events → workflows | `Subscriptions/Workflows/Events/Subscription{Activated,Renewed,PastDue,Canceled,Expired,Resumed}Event.cs` + drivers | Raised from `ISubscriptionEventHandler`. | Event tests mirror `SubscribedTenantWorkflowEventTests`. |
| 2.9 Reports on the ledger | `Subscriptions/Reports/*` rewrite over `PaymentAttemptIndex` (add `OwnerId`, `ReferenceType`, `ReferenceId`, `CreatedUtc`, `ConfirmedAmount`, `Currency` columns in Phase 1.1) and `SubscriptionIndex`; add MRR, churn, active by plan; group by currency (no cross-currency sums); move `TaxCollectedReport` to the Taxation module reading `PaymentAttemptIndex` + snapshots | | Aggregator tests updated; currency-split assertions. |

### Phase 3 — Provisioned tenants as a durable job — L

| Task | Files | Work | Acceptance / tests |
| --- | --- | --- | --- |
| 3.1 New module `CrestApps.OrchardCore.Subscriptions.Tenants` (Core + module; `DefaultTenantOnly`) | Move `TenantOnboardingPart`, its driver, schema, settings, `FeatureProfiles*`, `TenantOnboardingStep*`, workflow events | `ITenantProvisioningService.EnqueueAsync(TenantProvisioningRequest)` persists a `TenantProvisioningJob` (`Queued`, `Running`, `Succeeded`, `Failed`, attempts, last error, `SubscriptionId`) and a background task (`* * * * *`, distributed lock per job) runs `SetupAsync`; retry with backoff up to N; on success store `TenantName` on the subscription and raise `SubscribedTenantSetupSucceededEvent`; on terminal failure raise the failed event and flag the subscription `ManualReview` (new status flag, not a lifecycle status). | Job tests with a fake `ISetupService`: retry, terminal failure, idempotent re-run after crash. |
| 3.2 Checkout step | `TenantOnboardingCheckoutHandler : CheckoutHandlerBase` (step + validation + `CompletedAsync` → enqueue) | Password handling per Task 0.1 (protect in step, unprotect only inside the job, wipe after). Validate tenant name against Orchard's `TenantValidator` rules. | Tests for validation and for secret wiping. |
| 3.3 Status page | `Wizard` confirmation shape + `Subscriptions.Tenants/Views/ProvisioningStatus.cshtml`, endpoint `provisioning/{jobId}/status` | Customer sees "Your site is being created…" with polling; email/notification on success with the site URL (via `INotificationService` when Notifications is enabled). | UI smoke test. |
| 3.4 Tenant entitlement applier | `Subscriptions.Tenants/Services/TenantEntitlementApplier.cs` (`IEntitlementApplier`) | `PastDue` (after grace) → set shell state `Disabled` with a configurable banner recipe; `Canceled`/`Expired` → disable; `Active`/`Resumed` → enable; plan change → apply feature profile. | Tests with a fake `IShellHost`. |

### Phase 4 — Test and operations hardening — L

| Task | Work | Acceptance |
| --- | --- | --- |
| 4.1 Provider tests | `StripeCheckoutPaymentProvider`, `StripeRecurringPaymentProvider`, `StripeSubscriptionService` (schedule phase duration, trial end), `StripePaymentIntentService` (`unexpected_state` fallback), `StripeCheckoutService.BuildOptions`, `StripeConnectService` (webhook provisioning success/failure, reconnect deletes old webhook). | Each public method has success, provider-error, and idempotency-replay tests. |
| 4.2 Store tests on SQLite | Add a YesSql SQLite test fixture (pattern: OrchardCore `DbConnectionTests`); cover `PaymentAttemptStore.GetPendingAsync`, `PaymentRefundStore` lookups, `TransactionStore.PageAsync`, `SubscriptionStore` indexes, `CheckoutSessionStore` token ownership, concurrency (`CheckConcurrency`). | Runs in CI without Docker. |
| 4.3 Engine concurrency tests | Two simulated nodes call `TryCompleteAsync` concurrently with a real `LocalLock`; webhook + sweep race; duplicate `BeginPaymentAsync` returns the same attempt. | Deterministic, no `Task.Delay` sleeps. |
| 4.4 End-to-end scenarios (Playwright, `tests/CrestApps.OrchardCore.E2E` or existing harness) | (a) Pay Later subscription with registration → Active subscription + outstanding transaction + reminder due; (b) Stripe Payment Elements subscription with `stripe-mock` or Stripe test keys behind an env guard → Active + ledger attempt; (c) hosted checkout return; (d) tenant provisioning job to Succeeded on a SQLite host; (e) online settlement of a transaction; (f) cancel at period end → Expired at period end via lifecycle sweep with `TestClock`. | Gated by `COMMERCE_E2E=1`; documented in `tests/README`. |
| 4.5 Observability | Structured log scopes with `CheckoutSessionId`, `AttemptId`, `SubscriptionId`; `EventId`s for money events; health check `commerce-reconciliation` (pending attempts older than 1 h, quarantined refunds); admin "Reconciliation" widget listing outstanding attempts and manual-review refunds. | Health check registered when Checkout is enabled; widget visible under Commerce. |
| 4.6 Security pass | Same-origin check on anonymous JSON endpoints; `[ValidateAntiForgeryToken]` on every admin POST (verify auto-validation covers minimal APIs, else add explicitly); secrets wiped from session documents; audit that no view renders provider secrets; rate-limit groups documented. | `security-review` skill run clean. |

### Phase 5 — Pricing flexibility ("any product at any price point") — L

| Task | Files | Work | Acceptance / tests |
| --- | --- | --- | --- |
| 5.1 `ProductPricePart` | `Products.Core/Models/ProductPrice*.cs`, `Products/Drivers/ProductPricePartDisplayDriver.cs` (repeatable editor, currency dropdown from catalog), migration, `Schemas/ProductPricePartSchemaDefinition.cs`, index `ProductPriceIndex` (`PriceId`, `ContentItemId`, `Currency`, `Kind`, `IsActive`) | §7.5. `ProductPart.Price/Currency` remain as the default one-time price; `SubscriptionPart` keeps `ContentTypes`, `Sort`, entitlements (Phase 6) and its billing fields are migrated into a default recurring `ProductPrice` by a data migration. | Editor validation tests (min ≤ max, trial ≥ 0, one default per kind); schema tests. |
| 5.2 Resolver | `DefaultPriceResolver` handles `PriceId`, `Quantity`, `CustomAmount` (validated against min/max), effective window, currency match; `ISellableProduct` gains `Prices` | | Resolver tests for every rule. |
| 5.3 Plan chooser | `ServicePlansController` + `Subscription.Summary.cshtml` render price options (interval toggle, currency, quantity input, custom amount input) and pass `priceId/quantity/customAmount` to checkout start | | View model tests. |
| 5.4 Stripe pricing | `StripeRecurringPaymentProvider.BeginAsync` uses `price_data` for custom amounts and quantity, `lookup_key = price:{PriceId}:{Currency}:{minor}:{interval}` cache for fixed prices; delete `StripePriceSyncService`, `SubscriptionsContentHandler`, `StripeSyncController` and the "Sync prices" admin page (or keep as an optional maintenance action that only ensures lookup-key prices exist). | | Tests for lookup-key derivation and inline price data. |
| 5.5 Trials | `TrialDays` on price → `RecurringBeginContext.TrialEnd`; lifecycle status `Trialing` → `Active` on first paid invoice; trial without card when `SupportsSetupBeforeCharge` and plan allows. | | Lifecycle + provider tests. |
| 5.6 Coupons (basic) | New `Checkout.Abstractions/Services/IDiscountProvider` + `DiscountLine` on `CheckoutInvoice`; a `Commerce.Coupons` feature can come later per the e-commerce roadmap; for this version implement fixed/percent one-time and first-cycle coupons stored as a catalog with usage limits, applied before tax (`ITaxableItem.DiscountAmount` already exists). | | Invoice math tests incl. tax on discounted base; usage-limit concurrency test. |

### Phase 6 — Entitlements and member access (contracts now, feature scaffold) — M

| Task | Work | Acceptance |
| --- | --- | --- |
| 6.1 Contracts | `SubscriptionEntitlement`, `IEntitlementApplier`, plan editor section (roles multi-select, feature keys, numeric limits), snapshot on subscription, apply/revoke from lifecycle; replace `SubscriptionRoleSettings` (site-wide roles) with per-plan role entitlements plus a site default. | Applier tests; roles removed on `Canceled`/`Expired`, kept during grace when configured. |
| 6.2 `ISubscriptionAccessService.HasActiveEntitlementAsync(user, key)` | Cached per request; used by a `[RequireEntitlement]` MVC filter and a Liquid/Razor helper. | Tests. |
| 6.3 Member-only content (scaffold) | Feature `Subscriptions.MemberAccess` that registers an access rule with the Content Access Control module keyed by entitlement; documented as preview. | One integration test. |

### Phase 7 — Docs, samples, release — M

See §11 for the page-by-page list. Add a `samples/` recipe "Commerce starter" enabling Checkout,
Stripe, Pay Later, Products, Subscriptions, Taxation, Transactions with one demo plan, one price in two
currencies, a tax rule, and a Pay Later net-30 setting.

### Dependency order

`0 → 1 → 2 → 3 → 4` strictly. `5` needs `1.4/1.5` (provider contract) and `2.2`. `6` needs `2.3`.
`7` runs alongside from Phase 1 on. Phase 4 tests should be written **with** Phases 1–3, not after;
it is listed separately only so its scope is explicit.

---

## 9. Detailed design notes for implementers

### 9.1 Checkout session state machine

```text
Pending ──begin──▶ AwaitingProvider ──verify: all settled──▶ Completed
   │                    │  verify: some outstanding ──▶ PaymentPending ──(poll/webhook/sweep)──▶ Completed
   │                    └─ any failed ──▶ Failed (compensate succeeded attempts)
   ├──cancel──▶ Canceled (cancel pending attempts)
   └──lifetime elapsed, nothing succeeded──▶ Expired
```

Terminal states are never left. `Completed` handlers run exactly once (guarded by the status
transition committed under the lock). `Failed` handlers run once; `UserRegistrationCheckoutHandler`
keeps its "delete the user we just created" compensation.

### 9.2 Attempt/obligation identity

`ObligationId` values: one-time = `CheckoutObligations.OneTime` constant; recurring groups =
`BillingDurationKey` string (existing `GetExpectedObligationIds`). Renewals recorded by the lifecycle
service use `"{subscriptionId}:{periodStartUtc:yyyyMMdd}"`. `IdempotencyKey` for provider calls =
`StripeIdempotencyKey.Compute(scope, sessionId, obligationId, amountMinor, currency, providerCustomerId)`
so a retry after a price change gets a new key.

### 9.3 Webhook → engine correlation

Every remote object the provider creates carries metadata `checkout_session_id`, `checkout_attempt_id`,
`obligation_id` (already the pattern in `StripeCheckoutPaymentProvider`). The Stripe event handler
resolves the attempt by `ProviderReference` first (`PaymentAttemptIndex.ProviderReference`), then by
metadata. Unknown references are logged at `Warning` and ignored (never create records from webhooks
except quarantined refunds, which already exist).

### 9.4 Currency policy

`CheckoutSettings.Currency` becomes the *display/default* currency only; the invoice currency comes from
the resolved price. Mixed-currency line items in one session are rejected at `StartAsync`. Reports
group by currency. Replace the `StripeCurrency`/`CurrencyScale` divergence with one table in
`Payments.Abstractions` plus a Stripe-specific `RequiresMultipleOfTen(currency)` rule.

### 9.5 Deletions checklist (forward-only)

`Subscriptions.Abstractions/*Flow*`, `*Session*`, `BillingItem.cs`, `BillingDurationKey.cs`, `Json/*`;
`Subscriptions.Core/SubscriptionPaymentSession.cs`, `Money.cs`, `StripeLimits.cs`, `Models/StripeMetadata.cs`,
`Models/PaymentInfo.cs`, `Models/PaymentsMetadata.cs`, `Models/SubscriptionInfo.cs`, `Models/SubscriptionsMetadata.cs`,
`Models/SubscriptionPaymentsMetadata.cs`, `Models/InitialPaymentMetadata.cs`, `Services/SubscriptionSessionStore.cs`,
`Services/StripePriceSyncService.cs`, `SubscriptionFlowDisplayDriver.cs`;
`Subscriptions/Endpoints/*`, `Services/SubscriptionWizard*.cs`, `Services/StripeCheckoutRequestFactory.cs`,
`Services/HostedCheckoutReturnValidator.cs` (moves to Stripe), `Handlers/SubscriptionWizardHandler.cs`,
`Handlers/SubscriptionPaymentHandler.cs` (replaced), `Handlers/SubscriptionsContentHandler.cs`,
`Controllers/StripeSyncController.cs`, `Drivers/StripePaymentSubscriptionFlowDisplayDriver.cs`,
`Drivers/PayLaterPaymentSubscriptionFlowDisplayDriver.cs`, `Drivers/SubscriptionWizardFlowDisplayDriver.cs`,
`Assets/js/stripe-subscription-checkout.js` (moves to Stripe as a checkout panel script);
`Payments.Abstractions/Models/PaymentMethod*.cs`; `Commerce.Abstractions` project;
`Checkout.Abstractions/Models/CheckoutSettings.DefaultPaymentMethod` (replaced by provider ordering setting).

---

## 10. Test plan summary

| Layer | Target after this plan |
| --- | --- |
| Unit (pure) | Keep all 479; add engine state machine, lifecycle state machine, price resolver, coupon math, entitlement appliers, provider adapters with fakes. Target ≥ 700 tests in the commerce namespaces. |
| Store (SQLite) | Every `DocumentCatalog` store and index provider; concurrency on transactions and subscriptions. |
| Integration (in-process Orchard host) | Feature enable/disable matrix (Checkout alone; + Stripe; + Pay Later; + Taxation; + Transactions.Notification); migrations from an empty DB; recipe import of prices/tax rules. |
| E2E (Playwright, gated) | The six scenarios in Task 4.4. |
| Architecture | Dependency direction rules in §6.2; no `double`/`float` money; provider capability truthfulness (existing); no `Task.Delay` in request-bound handlers (new analyzer-style test scanning the Checkout/Subscriptions assemblies for `Task.Delay` usage). |

---

## 11. Documentation update list

Do these in the phase that changes the behavior; the page must never describe an unreachable path.

| Page / file | Changes |
| --- | --- |
| `src/CrestApps.Docs/docs/modules/checkout.md` | Rewrite around `ICheckoutEngine`, the state machine (§9.1), the public wizard, endpoints, provider panels, recurring provider contract, async completion, guest token, settings (`SessionLifetime`, provider order). Move ledger sections to `transactions.md` with a link. |
| `subscriptions.md` | Replace flow description with "Subscriptions is a Checkout consumer"; document `Subscription` record, statuses, lifecycle actions (portal + admin), dunning settings, entitlements, events, reports (currency-split), prices on products, trials, coupons. Remove Stripe endpoint and "Sync prices" sections. |
| `pay-later.md` | Recurring Pay Later behavior (obligation per cycle, renewal task, past-due), settlement path now real. |
| `transactions.md` | Now the home of payments, refunds, obligations, receipts policy, numbering; admin tabs; refund manual-review resolution. |
| `payments.md` | Provider event list (add failed/updated/deleted subscription events); Stripe section: adapter implements one-time + recurring + refunds; webhook events list; reconnect note. |
| `products.md` | `ProductPricePart`, price kinds, custom amount, quantity, trial, effective windows, schema. |
| `taxation.md` | Add exemption certificate / merchant registration admin (if X-01 UI is done), Tax collected report relocation. |
| `wizard.md` | "Checkout is a wizard consumer" example replacing the Subscriptions example. |
| `commerce.md` | Remove financial-document policy mention (moved). |
| New `subscriptions-tenants.md` | Provisioning job, status page, tenant entitlement behavior, workflow events. |
| Module `README.md` (all eight + Tenants) | Short purpose, features, dependencies, link to the docs page (match the Commerce README style). |
| `docs/ecommerce-plan.md` | Correct the inventory table rows for Checkout, Pay Later, Subscriptions, Transactions; note that Orders/Carts start after this plan's Phase 4. |
| `src/CrestApps.Docs/docs/changelog/3.0.0.md` | One bullet per user-visible change per phase; breaking-change section listing the deletions in §9.5. |
| `tests/README` (new) | How to run commerce suites, SQLite store tests, and gated E2E. |

---

## 12. Release readiness checklist

- [ ] `dotnet build -c Release -warnaserror` clean for the solution.
- [ ] All commerce test namespaces green; SQLite store tests green; E2E green with `COMMERCE_E2E=1` on at least one machine with Stripe test keys.
- [ ] Fresh tenant setup with the "Commerce starter" recipe: buy a plan with Pay Later, buy with Stripe test card (Payment Elements and hosted), settle the Pay Later balance online, cancel at period end, refund a payment from admin, provision a tenant and sign in with the chosen password.
- [ ] Multi-node check with Redis: replayed webhook + concurrent completion produce one subscription and one ledger entry.
- [ ] Docs site builds; every §11 page updated; module READMEs present.
- [ ] Changelog and breaking-change list complete.
- [ ] Architecture tests enforce §6.2 rules.
- [ ] `docs/ecommerce-plan.md` inventory corrected and Phase 1 (Customers) unblocked.
