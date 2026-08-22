# Ecommerce modules reconstruction plan

## Status and purpose

**Planning only.** This document defines the ecommerce architecture and delivery sequence. It does
not implement runtime behavior.

This revision re-audits the roadmap against the current branch, including the Transactions, Receipts,
Commerce, TaxTable, address, checkout, refund, and architecture changes now committed. It keeps
gaps that still exist, removes gaps already closed, and records the difference between:

- reusable infrastructure that already exists;
- ecommerce domain capabilities that are still absent;
- newly committed reusable infrastructure that is complete but not yet connected to Orders; and
- decisions that must be approved before schema-committing implementation begins.

The target is a **single-merchant, modular Orchard Core ecommerce solution** that can sell:

- physical goods;
- digital goods and downloads;
- services;
- recurring subscriptions;
- variable products with variants;
- taxable and tax-exempt items;
- shippable and non-shippable items;
- mixed carts containing different item types.

The solution must support guest and authenticated customers, server-rendered and headless use,
customer account management, administrator order and fulfillment management, refunds, inventory,
shipping charges, reporting, exports, and provider-neutral integrations. Marketplace seller
payouts, commissions, vendor settlement, and multi-merchant order splitting are out of the first
scope.

Relative effort labels are directional only:

- **S** — isolated contract, UI, or test work;
- **M** — one bounded module with persistence and UI;
- **L** — several modules or a stateful integration;
- **XL** — financial, concurrency, migration, or cross-module work.

## Executive decision

The current branch now has a strong, tested financial and Orchard foundation, including a generic
outstanding-obligation ledger and reusable receipt builder, but it is **not yet an ecommerce
platform**. The missing center is still the durable commercial domain:

`Customer → Product/Variant → Cart → Draft Order → Checkout → Payment → Fulfillment → Receipt/Report`

Do not build ecommerce behavior inside Subscriptions, Stripe, Products, or Checkout. Add reusable
Customers, Orders, and Carts blocks, then add separate ecommerce capability modules for Inventory,
Shipping, Promotions, Digital Delivery, Reviews, Storefront, and Admin/Reports.

The existing modules must remain usable independently:

- Subscriptions remains a recurring-billing consumer.
- Checkout remains payment-safe orchestration.
- Stripe remains a provider adapter.
- Pay Later remains an offline provider.
- Products remains the catalog owner.
- Taxation remains the tax engine.
- Addresses remains the geographic reference and resolution layer.
- Transactions remains the provider-neutral outstanding-obligation ledger.
- Receipts and Reports remain reusable presentation/infrastructure modules.

## Current branch inventory

The following table is the implementation baseline verified from source. “Present” means the code
and contracts exist; it does not mean that the capability is complete for ecommerce.

| Area | Current implementation | Ecommerce meaning |
| --- | --- | --- |
| Products | `src/Core/CrestApps.OrchardCore.Products.Core` owns `ProductPart`, `ProductType`, `ISellableProduct`, `SellableProduct`, `ProductSnapshotContext`, and `IProductSnapshotResolver`. The Products module provides the editor, schema, migration, snapshot resolver, and Taxation bridge. | Reusable catalog seam exists. Full catalog, variants, schedules, availability, digital delivery, and shipping metadata do not. |
| Checkout | `CheckoutSession`, flow/step contracts, durable session store, payment attempts, payment refunds, reconciliation, tax integration, rate limiting, distributed coordination, and a reconciliation background task exist in `Checkout.Abstractions` and `Checkout.Core`. | Reusable payment checkout exists. A checkout session is not an order, cart, quote, or fulfillment record. |
| Generic Stripe checkout | `StripeCheckoutPaymentProvider` is registered when Checkout is enabled. It supports one-time embedded PaymentIntent payments and Stripe refunds. Stripe webhooks now dispatch payment failure/cancellation, refund, refund-update/failure, and dispute events through provider-neutral payment-event and refund-reconciliation contracts. | Reusable one-time card payment and refund-event reconciliation exist. Orders must use this adapter, not subscription-specific endpoints. |
| Subscription Stripe flows | Stripe services also support products, prices, customers, setup intents, subscriptions, and Checkout Sessions. Subscriptions has its own Payment Elements and hosted Checkout paths, with eligibility rules and return validation. | Hosted and recurring Stripe behavior exists for Subscriptions only. It must not be described as generic ecommerce Checkout capability without a separate design. |
| Pay Later | `PayLaterCheckoutPaymentProvider` implements the generic checkout provider. It supports one-time, recurring, and combined obligations but no processor refund operation. Its checkout handler now creates idempotent outstanding Transactions entries, and settlement checkouts do not create duplicate debts. | Reusable deferred/offline payment and outstanding-balance tracking exist. Guest ownership, concurrency-safe settlement, and order-specific refund/write-off behavior remain ecommerce integration work. |
| Taxation | Tax categories, types, jurisdictions, rules, calculation methods, sourcing strategies, exemptions, merchant registrations, snapshots, refund calculation, TaxationPart, product taxable-item integration, recipes, and deployments exist. Calculation methods include percentage, fixed, per-unit, weight, volume, progressive, threshold, and table-driven calculation. | Strong reusable tax foundation exists. Commerce must provide order-line, shipping, discount, fee, and customer/address tax inputs and persist order snapshots. |
| Tax tables | `TaxTable`/`TaxTableRow`, effective periods, row validation, admin CRUD, protection from deletion while referenced, recipes, deployments, schemas, and tests exist. | TaxTable management is a closed foundation capability. Commerce still supplies order-specific taxable items and immutable order tax snapshots. |
| Addresses | Content-backed Country → Region → County → City → District hierarchy, `GeographicAreaIndex`, `IAddressResolver`, canonical country fallback, and an immutable `Address` snapshot with recipient, company, street lines, postal code, and phone exist. | Geographic resolution and the reusable value contract are complete. Customers and Orders still need to implement address ownership, validation by purpose, and snapshot persistence. |
| Recipes | The Recipes module and `Recipes.Core` provide JSON import/deployment rendering and content-part schema registration patterns used by Products, Subscriptions, and Taxation. TaxTable now has admin, recipe, deployment, schema, and validation coverage. | Reuse the existing recipe/deployment/schema conventions for every future ecommerce entity and content part; do not invent ecommerce-specific transport formats. |
| Commerce | The dependency-only Commerce feature registers the shared top-level Commerce admin menu and icon. It has no order, cart, customer, fulfillment, or orchestration domain yet. | The composition/menu shell exists. The future Commerce layer must remain a thin orchestrator rather than a catch-all domain assembly. |
| Subscriptions | Subscription content parts, recurring invoice/tax behavior, payment flow, Stripe synchronization, hosted Checkout, Pay Later, admin management, tenant onboarding, receipts, indexes, and subscription reports exist. | Provides a reference consumer and regression surface. It is not a generic order/cart/storefront implementation. |
| Receipts | `IReceiptService`, `ReceiptRequest`, `ReceiptDocument`, tax lines, branding settings, permissions, tests, and a reusable printable view exist. Receipts are generated on demand from consumer-supplied data and are not persisted. | Reusable printable receipts are complete. Orders still own order numbers, invoice/credit-note policy, immutable financial documents if required, and order-derived receipt data; no second receipt renderer should be created. |
| Reports | `IReport`, report filters/date ranges, metric/table/chart documents, CSV export, optional OpenXml export, admin navigation, and report rendering exist. | Commerce reports should be separate `IReport` implementations over order/inventory/shipping/refund data. |
| Transactions | Provider-neutral `Transaction` ledger, indexes, customer statement, administrator management report, offline/online settlement, registered sources, optional notification reminders, Pay Later integration, migrations, permissions, and tests exist. | This tracks outstanding obligations, not orders, payment attempts, refunds, or fulfillment. Orders must link to it where deferred balances exist. Guest ownership/access, concurrency-safe settlement, order integration, and refund/chargeback semantics remain to be designed. |
| Customers | Users and subscription customer/provider references exist, but no reusable customer profile, guest identity, merge, address book, tax profile ownership, or customer CRM module exists. | Greenfield reusable block. |
| Carts | No durable guest/authenticated cart, merge, expiration, line selection, or cart API exists. | Greenfield reusable block. |
| Orders | No durable order aggregate, order number, immutable line/address snapshot, order state machine, fulfillment linkage, or order history exists. `SubscriptionOrder` is a subscription-specific model and must not become the generic order. | Greenfield reusable block and the commercial system of record. |
| Inventory | No stock ledger, SKU availability, reservation, movement history, backorder, restock, or concurrency-safe allocation exists. | Greenfield ecommerce capability. |
| Shipping | No zones, methods, rates, shipping charges, packages, shipments, carriers, tracking, or fulfillment workflow exists. | Greenfield ecommerce capability. |
| Promotions | No coupon, campaign, discount allocation, stacking, eligibility, usage counter, or promotion snapshot exists. | Greenfield ecommerce capability. |
| Digital delivery/services | `ProductType.Digital` and `ProductType.Service` exist, but no download entitlement, license/key delivery, service fulfillment, appointment/resource allocation, or completion evidence exists. | Greenfield fulfillment capabilities. |
| Reviews | No review, rating, moderation, verified-purchase, abuse, or aggregate model exists. | Greenfield capability. |
| Generic storefront/API/admin | Existing controllers and endpoints are primarily subscription-specific. There is no generic catalog, cart, checkout, order, customer account, refund, fulfillment, or commerce API surface. | Greenfield presentation and integration layers. |

## What is already closed

The following older plan gaps must not be reopened:

1. **Money representation.** Commerce financial fields use `decimal`; currency-aware minor-unit
   conversion belongs at provider boundaries. New ecommerce models must not introduce `double`,
   `float`, or currency-free minor-unit values.
2. **Catalog ownership.** `ProductPart` and the sellable snapshot contract belong to
   `Products.Core`, not Payments or Checkout.
3. **Generic payment provider seam.** Checkout has provider-neutral begin, verify, cancel,
   capability, and refund contracts. Stripe and Pay Later are separate implementations.
4. **Durable payment ledger.** Payment attempts are persisted before provider calls, provider
   references are stored, and completion requires authoritative verification.
5. **Durable refund ledger.** `PaymentRefund` is persisted before provider mutation, supports
   idempotency and distributed locking, allocates tax from the original snapshot, and can enter
   `PendingManualReview`.
6. **Tax snapshot model.** Historical tax is captured and refund tax is derived from the historical
   snapshot, not current rules.
7. **Product recipe schema.** `ProductPartSchemaDefinition` exists and is registered through the
   Recipes feature. Subscription and tenant-onboarding part schemas also exist.
8. **Taxation catalog foundation.** Tax categories, types, jurisdictions, rules, calculation
   methods, TaxTable effective periods and validation, and deployment/recipe/schema patterns exist.
9. **Reusable receipts and reports.** Future order consumers should use the existing services and
   report document/export contracts instead of creating parallel implementations.
10. **Foundation boundary tests.** Architecture, project-reference, provider-capability, checkout
    reference, address, TaxTable, refund reconciliation, and regression tests now enforce the
    completed foundation contracts.

## Foundation review result

The previous F0–F5 foundation gaps are closed or recorded and must not remain as blockers:

1. **Taxation baseline:** TaxTable effective periods, validation, admin management, deletion
   protection, recipes, deployments, schemas, and tests are implemented.
2. **Payment events and refunds:** Stripe refund, refund-update/failure, dispute, payment-failure,
   and cancellation events use provider-neutral contexts. Refunds are correlated under distributed
   locking, unmatched remote refunds are quarantined for manual review, and stale events cannot
   regress terminal states.
3. **Order-to-checkout contract:** `CheckoutReferenceTypes.Order` defines the canonical reference;
   the future order owns the reverse checkout-session link and payment attempts remain authoritative.
4. **Address value contract:** `Address` is an immutable snapshot with recipient, company, street
   lines, geographic fields, postal code, and phone; the resolver normalizes country values.
5. **Architecture enforcement:** project-reference and assembly dependency tests enforce the
   provider-neutral foundation boundaries and provider capability truthfulness.
6. **Foundation decisions:** approved defaults for product identity, variants, prices, cart ownership,
   order numbers, inventory, shipping, storefront mode, retention, and guest access are recorded in
   [`docs/ecommerce-foundation-decisions.md`](./ecommerce-foundation-decisions.md).
7. **Reusable financial support:** Transactions provides outstanding-obligation tracking and
   settlement; Receipts provides branded printable documents; Reports provides shared reporting and
   exports.

These closures remove the foundation gate. They do **not** mean that the ecommerce domain has been
implemented.

## Remaining gaps before the first ecommerce vertical slice

These are the actual unresolved boundaries revealed by the current source review:

### G1 — Build Customers and guest ownership — L

No reusable Customers module owns profiles, authenticated/guest identity, saved addresses, tax
profiles, merge behavior, retention, or customer authorization. The current Transactions ledger
identifies owners by authenticated user id and its reminders resolve users through `IUserService`;
an anonymous Pay Later checkout therefore has no customer statement or reminder path.

The Customers design must:

- own customer records and address-book entries while using the immutable `Address` value contract;
- define authenticated/guest ownership, guest order access, merge, and PII policy;
- provide a guest-safe reference for outstanding Transactions without exposing another customer's
  balance;
- define how notifications and online settlement work for guest obligations;
- preserve customer/order ownership across tenant boundaries and account deletion.

### G2 — Complete the catalog and price contract — XL

Products still provide a single `ProductPart.Price` and optional `Sku`. `ISellableProduct` is a
useful snapshot seam, but it does not yet provide variants, explicit product currency, price
schedules, sale windows, quantity/customer-group pricing, availability, shipping metadata, or
digital/service metadata.

The approved catalog decisions must be implemented before order snapshots are finalized. Product
price currency must not be inferred only from the tenant Checkout setting when products can carry
different currencies or price lists.

### G3 — Define Transactions integration and concurrency — L

Transactions is complete as a reusable outstanding-balance ledger, but it is not an order ledger,
payment-attempt ledger, refund ledger, or fulfillment ledger. Before Orders consume it:

- link deferred obligations to the canonical order and payment-attempt references;
- support guest ownership/access or explicitly prohibit guest deferred payment;
- make online settlement, offline payment recording, cancellation, and reminders safe under
  concurrent requests and retries;
- define partial payment, overpayment, write-off, refund, chargeback, and dispute behavior;
- prevent a stale settlement checkout from charging an amount that no longer matches the current
  outstanding balance;
- base outstanding queries on the computed balance as well as lifecycle status, and use
  currency-specific precision in transaction pages, payment inputs, and reminder messages rather
  than the current fixed two-decimal presentation;
- keep transaction status separate from order payment, fulfillment, and refund states.

### G4 — Define order financial documents — M

Receipts are now reusable and intentionally on-demand. They are not persisted invoices, credit
notes, refund documents, or tax-compliance records. Orders must decide whether the first release
needs only receipts or also immutable invoice/credit-note documents, document numbering, billing
address display, refund references, and legal retention. The implementation must still use
`IReceiptService` for the printable receipt path and must not create a second receipt renderer.

### G5 — Define the commerce application boundary — M

The Commerce module currently owns only the shared admin menu. The future Commerce orchestration
feature must be kept separate from the reusable Customers, Products, Carts, Orders, Transactions,
Checkout, Taxation, Receipts, and Reports contracts. Define which commands belong in Orders versus
Commerce orchestration, and keep Storefront/Admin as adapters over those contracts.

## Target module architecture

### Existing reusable support modules

These modules now exist and must be consumed rather than duplicated:

- **Commerce** provides the shared admin menu and feature shell. It may later host composition and
  cross-domain orchestration, but it must not own the order, payment, tax, receipt, or report data
  models.
- **Transactions** owns provider-neutral outstanding obligations, settlement history, reminders,
  and management views. It is optional for orders that are fully settled at checkout and required
  when a payment method leaves a balance to collect.
- **Receipts** owns branded printable receipt construction and rendering. It does not persist
  financial records or replace Orders, Checkout, or the refund ledger.
- **Reports** owns report documents, filters, metrics, tables, charts, and exports. Commerce
  reporting must add consumers over durable order, payment, tax, inventory, and fulfillment data.

### Reusable modules

#### Customers

`CrestApps.OrchardCore.Customers.Abstractions`, `Customers.Core`, and `Customers`.

Owns customer profile and buyer identity, not authentication, products, orders, or providers:

- authenticated user link;
- guest customer token;
- guest-to-user merge;
- person/business classification;
- contact and communication preferences;
- customer status;
- provider-neutral external ids;
- customer tax-profile reference;
- saved address references.

#### Products

Keep the existing Products projects and extend them without payment dependencies:

- product identity and publication;
- variants and attributes;
- unique SKU and variant identity;
- currency-aware prices and effective-date schedules;
- customer/quantity price resolution;
- visibility and availability;
- physical, digital, service, and subscription classification;
- shipping class, weight, dimensions, and virtual/downloadable flags;
- stable `ISellableProduct` snapshot extensions.

Prefer separate attachable parts for shipping, digital delivery, and service fulfillment instead of
turning `ProductPart` into a catch-all. Every new part requires recipe schema coverage.

#### Orders

`CrestApps.OrchardCore.Orders.Abstractions`, `Orders.Core`, and `Orders`.

Owns the durable commercial record:

- order id and human-readable number;
- customer/guest ownership;
- immutable product/variant/SKU line snapshots;
- quantity, unit price, discounts, tax classification, and tax lines;
- billing/shipping address snapshots;
- totals and currency;
- checkout/payment/refund references;
- order and payment state transitions;
- fulfillment references;
- notes, audit, and event history.

Orders must not depend on Storefront, Stripe, Inventory implementation, or subscription routes.

#### Carts

`CrestApps.OrchardCore.Carts.Abstractions`, `Carts.Core`, and `Carts`.

Owns:

- durable cart identity and owner;
- guest token and authenticated owner;
- line/product/variant selection;
- quantity and custom options;
- expiration;
- merge behavior;
- cart validation hooks.

Carts do not call payment providers or directly mutate stock. Inventory contributes an optional
availability/reservation contract.

#### Transactions integration

Do not create a second debt or payment-obligation model in Orders. Orders reference the existing
Transactions ledger for deferred or partially unpaid balances, while Checkout continues to own
payment attempts and refunds. Transactions must remain usable by Subscriptions and other consumers
without taking a dependency on Orders or Commerce presentation.

#### Receipts integration

Orders build receipt requests from immutable order and payment data and pass them to
`IReceiptService`. Receipt rendering remains in Receipts; order numbering, invoice/credit-note
semantics, legal retention, and refund documents remain outside that module.

### Ecommerce capability modules

Use independent features/projects. An umbrella Commerce feature may compose them but must not become
a catch-all domain assembly.

| Module | Owns |
| --- | --- |
| `Commerce` | Composition, shared orchestration, feature profiles, common permissions, and cross-domain policies only. |
| `Commerce.Inventory` | Stock items, reservations, movements, adjustments, backorders, restocking, low-stock tasks, and inventory reports. |
| `Commerce.Shipping` | Zones, shipping classes, methods, rates, packages, shipments, fulfillment, tracking, and shipping reports. |
| `Commerce.Promotions` | Coupons, campaigns, eligibility, discounts, stacking, usage limits, and immutable promotion snapshots. |
| `Commerce.Digital` | Download entitlements, access tokens, license/key delivery, expiry, revocation, and delivery audit. |
| `Commerce.Services` | Service fulfillment status, required customer data, scheduling/resource hooks, completion evidence, and service-specific cancellation rules. |
| `Commerce.Reviews` | Reviews, ratings, moderation, verified-purchase checks, abuse controls, and aggregates. |
| `Commerce.Storefront` | Catalog, cart, checkout, confirmation, account, order, receipt, download, and customer-facing APIs/pages. |
| `Commerce.Admin` | Order, inventory, shipping, promotion, review, refund, and commerce settings administration. |
| `Commerce.Reports` | Commerce `IReport` implementations and exports over the shared Reports framework. |

Payment integrations remain adapters:

- Stripe generic Checkout supports one-time embedded PaymentIntent and refunds.
- Stripe subscription/hosted Checkout remains specialized to Subscriptions until a generic
  multi-obligation provider contract is explicitly designed.
- Pay Later is reusable for one-time and recurring obligations but has no executable processor
  refund; its order refunds enter manual review/settlement.

## Core domain rules

### Product and price snapshot

At cart validation and order creation, resolve the product through `IProductSnapshotResolver` and
persist the complete commercial snapshot. It must include, as applicable:

- content item and version;
- product and variant ids;
- SKU;
- title and display data required for receipts;
- quantity and unit price;
- currency and price-list/schedule identity;
- product type and fulfillment classification;
- tax category/classification/external code;
- shipping class, weight, dimensions, and digital/service metadata;
- applied customer/quantity pricing reason.

After order placement, changing the product, price, tax rule, or content item must not change the
order.

### Totals and taxation

Calculate server-side in a deterministic order:

1. resolve sellable lines and quantity;
2. resolve base prices and price adjustments;
3. apply promotions and allocate discounts;
4. calculate shipping rates;
5. create taxable items for merchandise, shipping, discounts, and fees;
6. resolve customer, merchant, origin, destination, exemption, and nexus context;
7. calculate tax through Taxation;
8. apply inclusive/exclusive tax policy and rounding;
9. persist subtotal, discount, shipping, tax lines, and grand total;
10. create the checkout invoice from the frozen draft-order quote.

Every monetary amount is `decimal` and carries an ISO currency. Provider minor-unit conversion is
performed only by the provider adapter. Orders, payment attempts, refunds, receipts, and reports
must use the same precision policy.

The order quote must also distinguish the amount due now, deferred or partially unpaid amounts
represented by Transactions, provider-confirmed payment amounts, refunds, credits, and chargebacks.
None of these states may be inferred from a single checkout status or receipt status.

### Order and checkout lifecycle

The canonical one-time order flow is:

1. Resolve cart and validate product, price, customer, address, promotion, tax, shipping, and stock.
2. Create a durable draft order and frozen quote before external payment mutation.
3. Create a CheckoutSession referencing the draft order.
4. Reserve stock when the approved reservation policy requires it.
5. Recalculate the authoritative invoice from the draft snapshot.
6. Persist payment attempts before provider calls.
7. Begin payment and persist provider references immediately.
8. Verify payment against the provider API; webhooks are hints and reconciliation triggers.
9. Mark the order paid only after every expected obligation is settled.
10. Create Transactions entries for any deferred balance, with an idempotent order/obligation link.
11. Commit inventory, create fulfillment/digital/service work, issue confirmation, and expose the order.
12. On expiry, cancellation, failure, or compensation failure, retain explicit state and release or
    review reservations; never silently delete the order or payment record.

### Order state model

Define explicit transitions and idempotent commands. At minimum:

`Draft → AwaitingPayment → PaymentPending → Paid → Processing → PartiallyFulfilled → Fulfilled`

with controlled paths to:

`Cancelled`, `PaymentFailed`, `Expired`, `PartiallyRefunded`, `Refunded`, `Disputed`, and
`ManualReview`.

Payment state, fulfillment state, refund state, and order state must be separate dimensions. A
refunded payment does not imply that a shipment was returned, and a failed shipment does not erase a
successful payment.

## Delivery plan

### Phase 0 — Foundation baseline — completed

The foundation work is complete in the current branch. TaxTable management and coverage,
provider-neutral payment events and refund reconciliation, the canonical Order checkout reference,
the immutable address value contract, architecture tests, provider capability checks, and the
foundation decision record are implemented. Transactions and Receipts are also available as
reusable support modules.

Continue to run the foundation regression suite, documentation checks, and clean build as part of
every ecommerce phase, but do not reopen these items as ecommerce design blockers unless a new
consumer exposes a concrete contract defect.

### Phase 1 — Reusable Customers and transaction ownership

**Effort: L. Depends on Phase 0.**

1. Create Customers abstractions/core/module with profile, guest identity, authenticated link,
   merge, status, contact preferences, provider-neutral ids, and tax-profile reference.
2. Add customer address book with defaults and immutable-copy support using Addresses geography.
3. Define guest order and outstanding-Transaction access tokens, PII retention, authorization, and
   merge audit events.
4. Define whether guest Pay Later is supported; if supported, connect guest ownership, reminders,
   and online settlement without relying on `IUserService`.
5. Define concurrency and idempotency rules for online Transaction settlement, offline payment
   recording, cancellation, and stale settlement sessions.
6. Refactor only duplicated subscription customer behavior that clearly belongs in Customers; do
   not make Subscriptions depend on Commerce presentation.

**Exit gate:** A customer can be authenticated or guest, merged safely, associated with saved
addresses, and resolved for tax/order ownership. Any supported guest outstanding balance has a
scoped access and settlement path without exposing another customer's data.

### Phase 2 — Catalog completion

**Effort: XL. Depends on Phase 0; can overlap late Phase 1.**

1. Add variant/attribute model and SKU uniqueness.
2. Add price schedules, sale windows, quantity tiers, customer-group policy, and currency policy.
3. Add visibility/channel/availability rules beyond basic Orchard publication where required.
4. Add shippable metadata as a reusable part: weight, dimensions, shipping class, and virtual flag.
5. Add digital metadata and service metadata as separate optional parts/contracts.
6. Extend the product snapshot resolver and indexes.
7. Add product content definitions, admin editors, recipes, deployment, permissions, and tests.

**Exit gate:** A normal product and a variable product resolve to complete, immutable,
provider-neutral snapshots for physical, digital, service, and subscription use cases.

### Phase 3 — Durable Carts

**Effort: L. Depends on Customers and catalog decisions.**

1. Persist carts in tenant YesSql documents with tenant-safe indexes.
2. Support guest-token and authenticated ownership.
3. Implement add, remove, quantity, variation/options, clear, expiration, and stale-price handling.
4. Define authenticated/guest merge as an idempotent command with conflict rules.
5. Add validation extension points for Products, Taxation, Promotions, Inventory, Shipping, and
   customer restrictions.
6. Add generic cart endpoints and optional display drivers without depending on Subscriptions.
7. Add background cleanup and audit events.

**Exit gate:** Guest and authenticated carts persist, merge, expire, and convert only through the
approved draft-order/checkout path. Carts never accept client totals as authoritative.

### Phase 4 — Orders, financial snapshots, and ledger integration

**Effort: XL. Depends on address contract, product snapshots, Customers, and Carts.**

1. Create Orders abstractions/core/module and the order aggregate.
2. Define order number generation that is tenant-scoped and safe across nodes/retries.
3. Persist immutable line, price, promotion, tax, billing, shipping, and fulfillment snapshots.
4. Add draft quote versioning and canonical `Order` ↔ `CheckoutSession` linkage.
5. Link payment attempts, refunds, Transactions obligations, and provider references without
   duplicating any existing ledger.
6. Add customer and guest ownership indexes, status/payment/fulfillment indexes, provider
   transaction indexes, and audit/event history.
7. Add idempotent commands for draft, quote, cancel, expire, pay, fulfill, refund, and manual
   review transitions.
8. Add administrator order list/detail/notes/permission contracts without Storefront coupling.
9. Build order receipts through `IReceiptService`; decide and implement invoice/credit-note
   persistence only if the approved financial-document policy requires it.

**Exit gate:** A draft order remains historically correct after product/tax changes and can be
queried by customer, guest token, order number, payment reference, and administrator.

### Phase 5 — First one-time purchase vertical slice

**Effort: XL. Depends on Orders, Carts, Checkout, Taxation, and a product snapshot.**

Implement one complete path before expanding every feature:

- one physical product;
- one digital product;
- one service product;
- one variable product;
- Stripe embedded one-time payment;
- Pay Later commitment;
- authenticated and supported guest deferred-payment ownership;
- tax enabled and disabled;
- guest and authenticated customer;
- receipt and order history.

The generic commerce checkout contributes contact, address, shipping-if-needed, review, payment,
and confirmation steps. It must not route through `SubscriptionsController`.

**Exit gate:** A customer can purchase a mixed one-time cart, receive an order and receipt, and
recover the result after browser refresh, timeout, duplicate request, provider retry, or webhook
replay. Any Pay Later balance is visible and settleable through the approved Transactions ownership
path. Existing Subscriptions behavior is unchanged.

### Phase 6 — Inventory and reservations

**Effort: XL. Depends on Orders and approved reservation/topology decisions.**

1. Add SKU stock records with on-hand, reserved, available, reorder threshold, and policy.
2. Add immutable stock movements and auditable adjustments.
3. Add reservation records tied to draft order and CheckoutSession.
4. Enforce atomic/distributed last-unit allocation.
5. Release on cart/order expiry, payment failure, cancellation, and compensation.
6. Commit reservation to stock movement only at the approved order state.
7. Add backorder/preorder, sold-individually, quantity limit, and restock policies.
8. Add low-stock tasks, alerts, import/export, and inventory admin/reporting.
9. Leave a location key in the schema if multi-location is approved; otherwise document the
   single-location boundary explicitly.

**Exit gate:** Concurrent checkout cannot oversell; every reservation has an owner, expiry, release
reason, and resulting stock movement; inventory can be rebuilt from movements.

### Phase 7 — Shipping and fulfillment

**Effort: XL. Depends on Orders, address contract, product shipping metadata, and inventory.**

1. Define `IShippingRateProvider` before concrete methods.
2. Add zones based on Addresses geographic codes, not duplicate country lists.
3. Add shipping classes, flat rate, table rate, free shipping, thresholds, and carrier adapter
   contracts.
4. Add shipping quote and checkout selection with server-side recalculation.
5. Add shipping as an invoice/taxable line and extend Taxation classification as required.
6. Persist packages, shipments, carrier, tracking, label references, and fulfillment status.
7. Support virtual-only/service-only orders without shipping and mixed-cart shipping correctly.
8. Add partial shipment and return/RMA extension points even if v1 fulfillment is simple.
9. Add workflow/notification hooks for shipped, delivered, failed, returned, and exception states.

**Exit gate:** Physical orders calculate and charge shipping correctly, mixed carts behave
correctly, virtual-only orders skip shipping, and administrators can fulfill and track shipments.

### Phase 8 — Promotions and discounts

**Effort: L. Depends on cart, orders, tax, and pricing decisions.**

1. Define coupon/campaign persistence and atomic usage tracking.
2. Support percentage, fixed amount, free shipping, product/category restrictions, date windows,
   minimum/maximum spend, per-customer limits, single-use, and stacking rules.
3. Define whether discounts apply before tax and how allocation works across lines and shipping.
4. Snapshot applied promotion identity, rule version, allocation, and usage on the order.
5. Add administrator management, recipes/deployments, permissions, audit, and reports.
6. Add abuse/rate limiting and idempotent redemption.

**Exit gate:** Repeating the same quote with the same inputs produces the same discount and tax
result; order history does not change when a promotion is edited or disabled.

### Phase 9 — Digital and service fulfillment

**Effort: L. Depends on Orders and the first vertical slice.**

1. Digital: create entitlements after payment, issue non-guessable download tokens, enforce
   authorization/expiry/download limits, support revocation and audit, and never expose storage
   paths directly.
2. Services: capture required customer/service data, expose fulfillment state, add scheduling or
   external resource hooks, define completion/cancellation/refund rules, and support manual
   fulfillment.
3. Add customer account views for downloads, service status, and related orders.

**Exit gate:** A paid digital item is securely deliverable and a paid service has an auditable
fulfillment lifecycle without pretending that payment equals service completion.

### Phase 10 — Storefront, customer accounts, and APIs

**Effort: XL. Depends on Phases 3–9.**

1. Add Storefront catalog list/detail/search/availability/pricing surfaces.
2. Add cart, checkout, confirmation, receipt, download, and service status surfaces.
3. Add authenticated account pages for profile, addresses, orders, subscriptions, refunds, and
   payment-related references.
4. Add guest order lookup using expiring, non-guessable access tokens.
5. Add generic API endpoints with authorization, pagination, rate limits, idempotency, and
   problem-details errors.
6. Keep server-side totals and provider calls behind domain/application services.
7. Add SEO/route integration, localization, accessibility, responsive behavior, cache invalidation,
   and webhook-safe confirmation pages.
8. Keep Storefront optional so Orders, Products, Checkout, and Reports can be used headlessly.

**Exit gate:** The same orders and totals work through the server-rendered and API surfaces, and
customer/admin authorization prevents cross-tenant or cross-customer access.

### Phase 11 — Administration, reports, reviews, and operations

**Effort: L. Can overlap late Storefront work.**

1. Add Commerce Admin order, customer, inventory, shipping, promotion, refund, review, and
   settings screens.
2. Add commerce reports through `IReport`:
   - gross/net sales and order count;
   - average order value and conversion inputs when available;
   - sales by product, variant, category, type, currency, and geography;
   - tax collected by jurisdiction/type;
   - discounts, coupon use, refunds, disputes, and manual-review items;
   - inventory on-hand, reserved, movement, turnover, and low stock;
   - shipment/fulfillment status and delivery time;
   - customer repeat purchase and lifetime value metrics;
   - digital downloads and service completion metrics.
3. Reuse CSV/OpenXml export and the shared date-range/filter model.
4. Add Reviews with verified-purchase rules, moderation, abuse controls, aggregates, and optional
   customer notifications.
5. Add dashboard widgets, scheduled reports, operational alerts, and audit views.

**Exit gate:** Operators can run the store from admin, finance can reconcile payments/refunds/tax,
warehouse users can reconcile stock/shipments, and customer-facing data matches durable records.

### Phase 12 — Release and operational readiness

**Effort: L.**

- Add feature profiles for minimal, digital, physical, service, and full stores.
- Add manifests, targets bundle references, setup recipes, deployment/export coverage, upgrade
  migrations, permissions, and tenant-isolation tests.
- Document Stripe webhook setup, Pay Later manual settlement, tax configuration, address seed data,
  Redis/distributed-lock requirements, backups, retention, and recovery.
- Add structured logs, correlation ids, metrics, health checks, failed-payment/refund/reservation
  alerts, and reconciliation jobs.
- Run clean restore/build/test, asset build, docs build, fresh-tenant setup, import/export, and
  upgrade tests.

## Industry capability checklist

The plan is complete only when these standard ecommerce concerns are addressed explicitly:

### Catalog and merchandising

- products, variants, attributes, SKUs, categories, media, visibility, search, related items;
- regular/sale/scheduled/quantity/customer-group pricing;
- physical, digital, service, subscription, bundle, and variable product behavior;
- publication, availability, purchase limits, sold-individually, backorder/preorder policies.

### Customer management

- guest and authenticated identity;
- profile, business/customer type, contacts, preferences, tax profile, external ids;
- address book and defaults;
- guest merge and order access;
- order history, receipts, downloads, service status, refund/cancellation requests;
- authorization, PII retention, deletion, and audit.

### Checkout and payments

- durable cart and draft order;
- address, shipping, tax, promotions, review, and payment steps;
- authoritative server totals;
- Stripe one-time embedded payment;
- Pay Later/manual commitment;
- future providers through capability and refund contracts;
- idempotency, webhooks, retries, rate limiting, SCA/provider action, reconciliation.

### Tax and finance

- tax-inclusive/exclusive pricing;
- jurisdiction, nexus, customer type, exemption, classification, and effective dates;
- merchandise, shipping, discount, fee, and service taxability;
- immutable tax snapshots and tax-line detail;
- partial/full refund tax allocation;
- receipts, invoices/order numbers, credit/refund records, manual review.

### Inventory and fulfillment

- stock ledger, reservations, concurrency, movements, adjustments, backorders, restocks;
- single/multi-location decision;
- shipping zones, methods, rates, classes, packages, labels, tracking;
- partial shipment, return/RMA boundary, digital entitlement, service fulfillment.

### Promotions and engagement

- coupon and campaign rules, eligibility, stacking, usage limits, allocation, snapshots;
- reviews, ratings, moderation, verified purchase, abuse controls;
- wishlist/saved-for-later as a later optional module.

### Reporting and operations

- sales, orders, AOV, conversion inputs, product/variant/category performance;
- customer retention/repeat purchase/lifetime value;
- tax, refunds, disputes, discounts, inventory, shipping, delivery, digital/service metrics;
- CSV/OpenXml export, filters, date ranges, dashboard, scheduled/alerted operations.

## Acceptance and test strategy

### Contract and unit tests

- No `double` or `float` on financial carriers.
- Zero-, two-, and three-decimal currency calculations and rounding.
- Price schedule, variant, discount, shipping, tax, and refund allocation.
- Order and payment state transition legality and idempotency.
- Guest merge and order access token expiry.
- Transaction creation, guest ownership, concurrent settlement, partial payment, reminders, and
  order/obligation correlation.
- Inventory reservation boundaries and release reasons.
- Shipping rate and taxability rules.
- Digital token authorization and service completion rules.

### Integration tests

- YesSql documents, indexes, migrations, tenant scoping, and concurrent updates.
- Orchard manifests, feature dependencies, permissions, admin routes, display drivers, recipes,
  deployment, and content-part schemas.
- TaxTable and every future user-addable entity import/export.
- Stripe signature verification, duplicate events, failed events, refund events, retries,
  provider references, capability rejection, and compensation.
- Receipt and report generation from durable order records.

### End-to-end scenarios

1. Guest physical product with tax and shipping through Stripe.
2. Authenticated variable product with coupon, reservation, and receipt.
3. Mixed physical/digital cart with one shipping charge and digital entitlement.
4. Service purchase with no shipping and manual fulfillment.
5. Pay Later order with manual settlement and manual-review refund.
6. Partial payment/refund, duplicate request, provider timeout, webhook replay, and crash recovery.
7. Inventory last-unit concurrency across multiple nodes.
8. Customer merge, order history, guest lookup, address update, and tax jurisdiction change.
9. Existing Subscription Payment Elements, hosted Checkout, renewal, Pay Later, tax, receipt, and
   admin regression flows.

## Documentation and completion rules

Every implementation phase must update:

- the module README;
- the relevant Docusaurus module page;
- feature reference and API documentation;
- recipes/deployment guidance;
- the changelog matching `VersionPrefix`;
- migration, configuration, security, and operational notes.

Phase 0 is complete. The first coding milestone is the reusable Customers/guest-ownership contract
and catalog extension work, not a storefront controller or a second payment/refund implementation.
