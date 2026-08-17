# Ecommerce modules reconstruction plan

## Status and purpose

**Planning only.** This document defines the ecommerce architecture and delivery sequence. It does
not implement runtime behavior.

This revision replaces the previous ecommerce roadmap after a source-level review of the current
branch and working tree. It keeps gaps that still exist, removes gaps already closed, and records
the difference between:

- reusable infrastructure that already exists;
- ecommerce domain capabilities that are still absent;
- work currently present in the working tree but not yet verified as a released baseline; and
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

The current branch has a strong financial and Orchard foundation, but it is **not yet an ecommerce
platform**. The missing center is the durable commercial domain:

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
- Receipts and Reports remain reusable presentation/infrastructure modules.

## Current branch inventory

The following table is the implementation baseline verified from source. “Present” means the code
and contracts exist; it does not mean that the capability is complete for ecommerce.

| Area | Current implementation | Ecommerce meaning |
| --- | --- | --- |
| Products | `src/Core/CrestApps.OrchardCore.Products.Core` owns `ProductPart`, `ProductType`, `ISellableProduct`, `SellableProduct`, `ProductSnapshotContext`, and `IProductSnapshotResolver`. The Products module provides the editor, schema, migration, snapshot resolver, and Taxation bridge. | Reusable catalog seam exists. Full catalog, variants, schedules, availability, digital delivery, and shipping metadata do not. |
| Checkout | `CheckoutSession`, flow/step contracts, durable session store, payment attempts, payment refunds, reconciliation, tax integration, rate limiting, distributed coordination, and a reconciliation background task exist in `Checkout.Abstractions` and `Checkout.Core`. | Reusable payment checkout exists. A checkout session is not an order, cart, quote, or fulfillment record. |
| Generic Stripe checkout | `StripeCheckoutPaymentProvider` is registered when Checkout is enabled. It supports one-time embedded PaymentIntent payments and Stripe refunds. Its capabilities explicitly report no recurring or hosted-checkout support. | Reusable one-time card payment exists. Orders must use this adapter, not subscription-specific endpoints. |
| Subscription Stripe flows | Stripe services also support products, prices, customers, setup intents, subscriptions, and Checkout Sessions. Subscriptions has its own Payment Elements and hosted Checkout paths, with eligibility rules and return validation. | Hosted and recurring Stripe behavior exists for Subscriptions only. It must not be described as generic ecommerce Checkout capability without a separate design. |
| Pay Later | `PayLaterCheckoutPaymentProvider` implements the generic checkout provider. It supports one-time, recurring, and combined obligations but no processor refund operation. | Reusable deferred/offline payment exists. Manual settlement and refund workflows are required for orders using it. |
| Taxation | Tax categories, types, jurisdictions, rules, calculation methods, sourcing strategies, exemptions, merchant registrations, snapshots, refund calculation, TaxationPart, product taxable-item integration, recipes, and deployments exist. Calculation methods include percentage, fixed, per-unit, weight, volume, progressive, threshold, and table-driven calculation. | Strong reusable tax foundation exists. Commerce must provide order-line, shipping, discount, fee, and customer/address tax inputs and persist order snapshots. |
| Tax tables | `TaxTable`/`TaxTableRow` and table-driven calculation exist. The current working tree also contains TaxTable admin controller, display driver, handler, menu, and views wired from Taxation startup. | Treat TaxTable management as **in-progress working-tree capability**, not a closed released baseline, until build, migration, deployment/recipe, UI, and tests are verified. |
| Addresses | Content-backed Country → Region → County → City → District hierarchy, `GeographicAreaIndex`, `IAddressResolver`, and canonical country fallback exist. | Geographic resolution is reusable. The flat `Address` abstraction currently carries geography and postal code but does not preserve all street/contact fields needed by orders and shipping. |
| Recipes | The Recipes module and `Recipes.Core` provide JSON import/deployment rendering and content-part schema registration patterns used by Products, Subscriptions, and Taxation. | Reuse the existing recipe/deployment/schema conventions for every future ecommerce entity and content part; do not invent ecommerce-specific transport formats. |
| Subscriptions | Subscription content parts, recurring invoice/tax behavior, payment flow, Stripe synchronization, hosted Checkout, Pay Later, admin management, tenant onboarding, receipts, indexes, and subscription reports exist. | Provides a reference consumer and regression surface. It is not a generic order/cart/storefront implementation. |
| Receipts | `IReceiptService`, `ReceiptRequest`, `ReceiptDocument`, tax lines, branding settings, permissions, and a reusable printable view exist. Receipts are generated from consumer records and are not persisted by the module. | Orders can produce receipts without duplicating branding or rendering. Receipt numbering and order-driven data remain to be designed. |
| Reports | `IReport`, report filters/date ranges, metric/table/chart documents, CSV export, optional OpenXml export, admin navigation, and report rendering exist. | Commerce reports should be separate `IReport` implementations over order/inventory/shipping/refund data. |
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
   methods, and deployment/recipe patterns exist. TaxTable is now present in the current source
   and working tree, subject to the verification gate below.
9. **Reusable receipts and reports.** Future order consumers should use the existing services and
   report document/export contracts instead of creating parallel implementations.

## Foundation gaps before ecommerce design

These are prerequisites for the ecommerce domain. They are not the same as building the missing
commerce modules.

### F0 — Establish a clean verified baseline — XL

The working tree contains TaxTable changes that are not all tracked. Before treating TaxTable as
complete:

- build the affected Taxation project and the full test project;
- verify `TaxTable` registration, migration behavior, admin create/edit/delete, validation,
  duplicate-name handling, and route/view coverage;
- add or verify deployment and recipe steps/schema for this user-addable catalog entity;
- add tests for serialization, versioning, effective dates, row boundaries, permissions, and
  tenant isolation;
- update Taxation documentation and the changelog only after the capability is verified.

The clean baseline must also include the completed money/product/refund changes and no undocumented
foundation behavior.

### F1 — Complete Stripe event truthfulness — XL

The Stripe webhook endpoint verifies signatures, locks by event id, deduplicates processed events,
and commits handler changes with the processed-event marker. However, its supported dispatcher list
currently covers subscription/payment success events and does not provide a generic order refund
reconciliation path for `charge.refunded`.

Before order refunds are exposed:

- define the provider-neutral event contract for payment failure, cancellation, refund, dispute,
  and chargeback notifications;
- dispatch `charge.refunded` (and relevant refund failure/update events) to the existing refund
  ledger reconciliation path;
- correlate remote refunds to `PaymentRefund` using provider reference, original transaction,
  idempotency key, and metadata;
- define behavior when a remote refund exists without a local refund request;
- preserve duplicate-event, retry, lock-contention, and partial-write behavior;
- add integration tests for signature failure, duplicate delivery, provider timeout, refund
  success/failure, and event replay.

Do not fabricate success from a webhook. The provider API remains authoritative when a webhook and
local state disagree.

### F2 — Define the order-to-checkout contract — XL

`CheckoutSession.ReferenceType`, `ReferenceId`, and `ReferenceVersionId` are intentionally generic,
but the ecommerce system needs one canonical relationship:

- `ReferenceType = "Order"` for ecommerce orders;
- `ReferenceId = Order.ItemId`;
- `ReferenceVersionId` identifies the draft/quote version only when versioning is required;
- the order stores the checkout session id for reverse lookup;
- payment attempts and refunds remain owned by Checkout and link through the session and provider
  transaction references;
- an order cannot be marked paid from a session flag alone.

Define the ownership, guest access, expiry, cancellation, retry, and recovery rules before Orders
or Storefront code is written.

### F3 — Complete the address value contract — XL

The Addresses module correctly owns geographic reference data, but the current resolved `Address`
model contains only Country, Region, County, City, District, and PostalCode. The AddressPart also
captures street lines, while the resolver does not preserve them in the flat contract.

Before customer addresses, shipping, or order snapshots:

- extend the reusable address contract to preserve street lines and the required recipient/company,
  phone, and normalization fields;
- define required versus optional fields by address purpose;
- normalize country and geographic codes through `IAddressResolver`;
- distinguish customer-editable address records from immutable order billing/shipping snapshots;
- define PII access, retention, deletion, and guest-token rules;
- add country/region/postal validation without hard-coding a second geography source.

### F4 — Enforce the reusable module graph — L

Create architecture tests and project-reference rules for the following direction:

```text
Users + Addresses ──> Customers
Products + Customers + Addresses + Taxation ──> Orders
Carts ──> Checkout-facing contracts (never providers)
Checkout ──> Payments abstractions
Stripe/PayLater ──> Checkout provider contracts
Orders + Checkout + Taxation + optional Inventory/Shipping/Promotions ──> Commerce orchestration
Commerce domain ──> Storefront/Admin/Reports adapters
Subscriptions ──> shared reusable contracts, never Commerce presentation
```

Reusable modules must not reference Storefront, Admin, Stripe implementation types, or
subscription controllers. Every new module requires a manifest, feature dependencies, permissions,
migrations/indexes, recipes/deployments where applicable, tests, and documentation.

### F5 — Decide the non-negotiable domain choices — M

Record and approve these decisions before implementation:

| Decision | Required answer |
| --- | --- |
| Product identity | How content item, product, variant, SKU, and external provider ids relate. |
| Variant storage | Child content items, a structured part, or a separate catalog document. |
| Price policy | Currency, tax-inclusive/exclusive behavior, sale windows, quantity tiers, customer groups, and effective-date resolution. |
| Cart persistence | Tenant YesSql document, ownership token, authenticated merge, expiry, and concurrency model. |
| Order number | Tenant-scoped sequence or non-sequential identifier, with retry and multi-node behavior. |
| Inventory topology | Single location for v1 or a location-aware schema from the start. |
| Reservation timing | Reserve at cart, checkout, or order creation; expiry; release; backorder/preorder. |
| Shipping provider | Contract for flat/table/free/carrier rates, zones, packages, and tax inputs. |
| Storefront mode | Server-rendered first, headless/API first, or both behind shared contracts. |
| Data retention | Customer PII, guest orders, payment references, downloads, audit history, and deletion rules. |

## Target module architecture

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
10. Commit inventory, create fulfillment/digital/service work, issue confirmation, and expose the order.
11. On expiry, cancellation, failure, or compensation failure, retain explicit state and release or
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

### Phase 0 — Baseline, decisions, and foundation closure

**Effort: XL. Blocks all ecommerce domain work.**

1. Verify the current TaxTable working-tree implementation and complete its migration,
   deployment/recipe/schema, permission, UI, and test surface.
2. Complete Stripe refund/event reconciliation and provider failure/dispute event contracts.
3. Approve the Order ↔ CheckoutSession relationship and address snapshot contract.
4. Approve product variant, price, cart, order number, inventory, shipping, storefront, and data
   retention decisions listed in F5.
5. Add architecture tests for project references, feature dependencies, tenant-scoped indexes,
   provider capability truthfulness, and money field types.
6. Update stale module documentation so generic Stripe one-time capability is not confused with
   subscription-only hosted/recurring flows.

**Exit gate:** Build/test baseline is green; no unresolved foundation ambiguity remains; all schema
decisions are recorded; existing Subscription checkout and payment regression tests pass.

### Phase 1 — Reusable Customers and address contracts

**Effort: L. Depends on Phase 0.**

1. Create Customers abstractions/core/module with profile, guest identity, authenticated link,
   merge, status, contact preferences, provider-neutral ids, and tax-profile reference.
2. Add customer address book with defaults and immutable-copy support using Addresses geography.
3. Extend the address value contract for street lines, recipient/company, phone, and normalization.
4. Define guest order access tokens, PII retention, authorization, and merge audit events.
5. Refactor only duplicated subscription customer behavior that clearly belongs in Customers; do
   not make Subscriptions depend on Commerce presentation.

**Exit gate:** A customer can be authenticated or guest, merged safely, associated with saved
addresses, and resolved for tax/order ownership without any Orders or Storefront dependency.

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

### Phase 4 — Orders and commercial snapshots

**Effort: XL. Depends on address contract, product snapshots, Customers, and Carts.**

1. Create Orders abstractions/core/module and the order aggregate.
2. Define order number generation that is tenant-scoped and safe across nodes/retries.
3. Persist immutable line, price, promotion, tax, billing, shipping, and fulfillment snapshots.
4. Add draft quote versioning and canonical `Order` ↔ `CheckoutSession` linkage.
5. Add customer and guest ownership indexes, status/payment/fulfillment indexes, provider
   transaction indexes, and audit/event history.
6. Add idempotent commands for draft, quote, cancel, expire, pay, fulfill, refund, and manual
   review transitions.
7. Add administrator order list/detail/notes/permission contracts without Storefront coupling.
8. Build order receipts through `IReceiptService` from order data.

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
- tax enabled and disabled;
- guest and authenticated customer;
- receipt and order history.

The generic commerce checkout contributes contact, address, shipping-if-needed, review, payment,
and confirmation steps. It must not route through `SubscriptionsController`.

**Exit gate:** A customer can purchase a mixed one-time cart, receive an order and receipt, and
recover the result after browser refresh, timeout, duplicate request, provider retry, or webhook
replay. Existing Subscriptions behavior is unchanged.

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

The ecommerce implementation must not start until Phase 0 exits successfully. The first coding
milestone after that gate is the reusable Customers/address contract and catalog extension work,
not a storefront controller or a second payment/refund implementation.
