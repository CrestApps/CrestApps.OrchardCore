# Ecommerce module implementation plan

## Status

Planning only. This document is a verified gap baseline for the current branch; it does not implement code or alter runtime behavior.

This plan evaluates whether the current branch can support a complete ecommerce solution and defines the implementation path. It is based on the current branch state, including the latest Products, Checkout, Payments/Stripe, Taxation, Addresses, Subscriptions, Reports, Recipes, and Orchard Core module changes.

The requested scope is:

- Physical goods, digital/downloadable goods, services, subscriptions, and variable products.
- A single merchant, not a marketplace.
- Inventory, shipping, tax, refunds, promotions, reviews, and analytics.
- Guest and authenticated checkout.
- Full storefront, administration, and headless/API surfaces.
- Provider-agnostic payment contracts, with Stripe as the first real gateway.
- Build on the existing modules; do not replace the payment, tax, or checkout foundations.

“Timeline” in this plan means delivery order, milestones, dependencies, exit gates, and relative effort bands. Calendar estimates should be added after the architecture decisions and acceptance criteria are approved.

Relative effort is intentionally directional:

- **S:** contained feature work with limited cross-module impact.
- **M:** several related services, persistence, UI, and tests.
- **L:** cross-cutting work across multiple modules with migrations and integration tests.
- **XL:** foundational or high-risk work requiring architecture decisions, compatibility work, and extensive failure testing.

## Executive conclusion

The branch has closed several gaps identified by the first version of this plan. It now has a sellable-product snapshot seam, a durable refund ledger, a generic Stripe payment/refund adapter, a content-backed address hierarchy, stronger taxation, and reusable report infrastructure. It still does not have the vertical commerce domain required for an online store.

The existing foundations are reusable and should be preserved:

- Checkout sessions, flow steps, durable payment attempts, reconciliation, idempotency, rate limiting, and distributed coordination.
- Currency-aware payment primitives and Stripe services.
- Tax determination, jurisdiction/rule catalogs, exemptions, merchant registration, immutable tax snapshots, and refund-tax allocation.
- Content-part composition, YesSql indexes, Orchard feature startup, permissions, reports, recipes, deployments, and subscription checkout patterns.

The following capabilities are effectively greenfield:

- A complete product catalog with variants, attributes, price schedules, visibility, and availability.
- Customer carts and cart-to-checkout conversion.
- A durable order aggregate and order lifecycle.
- Inventory, reservations, backorders, and stock movement.
- Shipping methods, zones, rates, fulfillment, and tracking.
- Customer-facing refund workflows, refund-event reconciliation, and order-linked refund history.
- Coupons, discounts, promotions, reviews, storefront pages, account pages, and commerce reports.

The correct strategy is an Orchard Core commerce solution made of independently enableable features, not one large class library. The branch has closed the money representation and catalog ownership gaps, but still needs a foundation-gap closure pass for refund reconciliation, typed order/checkout integration, baseline verification, and dependency enforcement. The solution should then add the Commerce domain above the existing frameworks and keep Subscriptions as a consumer of the shared checkout/payment infrastructure.

## Verified branch baseline

This section supersedes stale statements from the original plan. It is based on the current source tree and latest branch commits, not only on module documentation.

| Capability | Current status | Evidence and consequence |
| --- | --- | --- |
| Product SKU | **Implemented, partial** | `ProductPart` now has `Sku`; contract tests protect the existing namespace. This is a foundation, not a complete catalog. |
| Sellable product snapshot | **Implemented, partial** | `ISellableProduct`, `SellableProduct`, and `DefaultProductSnapshotResolver` provide a checkout-facing seam. It still lacks variant identity, schedule/visibility rules, and full fulfillment metadata. |
| Product ownership/layering | **Implemented** | `ProductPart` is now owned by `Products.Core`; boundary tests prevent Products from referencing Payments or Checkout. The remaining catalog work is variants, attributes, visibility, and fulfillment metadata. |
| Checkout sessions and payment attempts | **Implemented** | Durable sessions, attempts, stores, reconciliation, tax application, background sweep, idempotency, and provider verification are reusable. They are not orders or carts. |
| Generic Stripe checkout provider | **Implemented, partial** | `StripeCheckoutPaymentProvider` is registered and supports embedded one-time payment-intent flows. Hosted checkout and recurring-payment capabilities remain false. |
| Refund ledger and provider contract | **Implemented, partial** | `PaymentRefund`, status, DocumentCatalog-backed stores, `ItemId` identifiers, indexes/migrations, resolver, lock-based orchestration, Stripe refund service, and tax allocation exist. Order-linked UI, refund-event reconciliation, and complete customer/admin workflows do not. |
| Taxation | **Implemented foundation** | Tax types are now user-managed with recipe/deployment coverage, alongside tax categories, jurisdictions, rules, sourcing, exemptions, snapshots, and refund calculation. Commerce still needs to supply order and shipping taxable items. |
| Addresses | **Implemented foundation** | Content-backed country/geographic hierarchy, unified `GeographicAreaIndex`, and `IAddressResolver` exist. Customer address books and immutable order address snapshots do not. |
| Reports and receipts | **Implemented foundation, partial** | Reports grouping, printable subscription receipts, and extensible tax rules exist. Commerce order, inventory, refund, promotion, and product reports do not. |
| Subscriptions | **Implemented consumer** | Subscription checkout, tax, Stripe, invoices, reports, and UI are active examples. Subscription-specific routes must not become the generic commerce API. |
| Customers | **Absent as a dedicated reusable domain** | Users and subscription/customer references exist, but there is no reusable customer profile, guest merge, customer address-book, or provider-neutral customer identity module. |
| Cart | **Absent** | No durable guest/authenticated cart, merge behavior, quantity/variation selection, or cart expiration exists. |
| Orders | **Absent** | No durable customer-facing order aggregate, order number, order history, fulfillment state, or order-linked payment lifecycle exists. |
| Inventory | **Absent** | No SKU stock ledger, reservation, backorder policy, movement history, or concurrency-safe availability service exists. |
| Shipping | **Absent** | No zones, methods, rates, shipping classes, shipments, tracking, or fulfillment persistence exists. |
| Promotions | **Absent** | No coupon, discount, usage-limit, stacking, or promotion snapshot model exists. |
| Reviews | **Absent** | No review, rating, moderation, or verified-purchase model exists. |
| Storefront/API | **Absent for generic commerce** | Current HTTP endpoints are subscription-scoped. There are no generic catalog, cart, checkout, order, account, receipt, or commerce API surfaces. |

## Pre-commerce foundation gap closure

These items should be completed and verified before starting Commerce-specific features. They remove structural ambiguity and prevent the new module from inheriting current inconsistencies.

### Foundation gap F1 — Unify money and amount boundaries — **Closed in code; documentation cleanup remains**

- The current branch now uses the approved decimal-based money boundaries for Products, Checkout, payment attempts, refunds, Stripe limits, and subscription billing.
- Keep the remaining compatibility/documentation cleanup explicit; one stale XML comment in `Subscriptions.Core/Money.cs` still describes the old floating-point representation.
- Keep provider minor-unit conversion inside Stripe/provider adapters.
- Preserve the added zero-, two-, and three-decimal currency, serialization, rounding, and money-type contract tests.
- Keep the Products, Checkout, Payments, Stripe, Taxation, and documentation contracts synchronized.

**Exit gate:** Substantively met. Before Commerce implementation, remove the stale documentation claim and retain the money regression tests.

### Foundation gap F2 — Correct catalog ownership without breaking compatibility — **Closed**

- `ProductPart` now lives in `Products.Core.Models`, and `ISellableProduct`/`IProductSnapshotResolver` are owned by Products.Core.
- Products remains independent from payment-provider implementation details, with boundary tests protecting that direction.
- Preserve the existing content-part storage and tenant data while extending the catalog.
- Make the sellable snapshot contract the only input consumed by checkout and future order pricing.

**Exit gate:** Met in the current branch. Future catalog changes must not reintroduce payment ownership.

### Foundation gap F3 — Finish generic payment/refund plumbing — **Partial**

- Document and test the current Stripe decision: embedded one-time PaymentIntent checkout is the first generic flow; hosted checkout is not assumed unless explicitly added.
- Add provider capability tests so unsupported hosted/recurring operations fail clearly rather than appearing available.
- Dispatch and reconcile Stripe refund events, not only payment/subscription success events. `charge.refunded` is currently not dispatched into the refund ledger and is a no-op in the existing webhook dispatch tests.
- Make tax-refund calculator registration mandatory for refundable taxable payments, or return an explicit failure/manual-review state instead of silently producing zero tax.
- Add provider integration tests beyond currency helpers: create/retrieve/cancel/refund, idempotency, webhook signatures, duplicate events, timeout, refund reconciliation, and retry behavior.

**Exit gate:** Generic one-time payment and refund behavior is truthful, observable, idempotent, and independently testable before Commerce starts using it.

### Foundation gap F4 — Stabilize shared address and checkout contracts — **Partial**

- Keep the new geographic content hierarchy and `IAddressResolver` as the source for selectable country/region data.
- Define serialization and normalization rules for billing/shipping address snapshots.
- Define the typed relationship between a future Order and `CheckoutSession`, including ownership, guest access, and lifecycle references. The current `ReferenceType`, `ReferenceId`, and `ReferenceVersionId` fields are generic strings, not yet an order contract.
- Ensure generic checkout services do not depend on subscription-specific route or view models.

**Exit gate:** Commerce can consume stable address and checkout contracts without reaching into subscription implementations.

### Foundation gap F5 — Complete baseline verification and documentation — **Partial**

- Add regression tests for the current Product snapshot, Stripe provider, refund service, address resolver, taxation, and subscription paths.
- Verify migrations, indexes, recipes/schema definitions, feature dependencies, permissions, provider behavior, and tenant isolation for the changed foundations. The branch now has useful money, product-boundary, schema, and subscription dependency coverage, but checkout/Stripe migration, index, permission, and provider lifecycle coverage remains incomplete.
- Keep module documentation and the changelog aligned with the actual embedded Stripe and refund behavior.

**Exit gate:** The foundation build/test/documentation baseline is green in a network-capable environment and the current branch has no known foundation-level ambiguity.

### Foundation gap F6 — Enforce reusable module boundaries — **Open**

- Approve the dependency graph in this plan as an architecture constraint.
- Create and separate Customers, Orders, Carts, and Commerce from Users, Products, Addresses, Checkout, Payments, Taxation, Reports, and provider projects in project references and feature manifests. No Customers, Orders, Carts, or Commerce projects exist in the current branch.
- Define which contracts are reusable abstractions and which implementations are optional Orchard features.
- Add dependency/build checks that prevent reusable modules from referencing Commerce Storefront/Admin or provider-specific projects. The current audit found no project-reference cycles among existing projects, but the proposed graph is not yet implemented or enforced.
- Require every new module to have its own manifest, startup registration, migrations/indexes, permissions, tests, and documentation.

**Exit gate:** The planned module graph has no cycles, reusable modules can be enabled without Storefront, and the dependency direction is enforced by project references and feature tests.

### Reusable blocks to build before Commerce-specific features

The following are not part of the Commerce-specific module and must be delivered as reusable blocks:

- `Customers`: customer profile, guest/authenticated identity, merge, address-book references, and provider-neutral external IDs.
- `Orders`: generic sales-order lifecycle, immutable snapshots, customer ownership, and audit history.
- `Carts`: cart state, ownership, merge, expiration, and line selection.
- `Products`: catalog and sellable product/variation definitions.

These blocks may be delivered in the same overall program, but their APIs and project dependencies must remain reusable by Subscriptions and other solutions.

### Explicit boundary: gaps that belong to Commerce

The following are intentionally not reusable-foundation work. They are the reason to build Commerce-specific features after F1–F6:

- Inventory reservations and stock movements.
- Shipping and fulfillment.
- Promotions and coupons.
- Generic storefront and account UI/API.
- Commerce receipts and customer refund requests.
- Reviews and commerce analytics.

## Current-state audit

### Reusable foundations

| Area | Existing evidence | Planned use |
| --- | --- | --- |
| Product integration | `src/Modules/CrestApps.OrchardCore.Products`, `Products.Core.Models.ProductPart`, `ISellableProduct`, `DefaultProductSnapshotResolver`, `ProductTaxableItemProvider` | Extend the existing SKU/snapshot seam into a real catalog while preserving attachable content-part behavior. Product ownership is no longer a Payments layering gap. |
| Checkout | `src/Abstractions/CrestApps.OrchardCore.Checkout.Abstractions`, `src/Core/CrestApps.OrchardCore.Checkout.Core`, `CheckoutSessionStore`, `PaymentCheckoutHandler` | Use as the payment-safe checkout orchestration layer. Add commerce flow handlers and storefront endpoints. |
| Payments | `src/Abstractions/CrestApps.OrchardCore.Payments.Abstractions`, `src/Core/CrestApps.OrchardCore.Payments.Core`, `Money`, `CurrencyScale`, `PaymentMethod` | Treat Payments as reusable contract/core infrastructure, not as a standalone enableable Orchard feature. Use it for amount normalization and provider-neutral payment contracts. |
| Stripe | `src/Modules/CrestApps.OrchardCore.Stripe`, `src/Core/CrestApps.OrchardCore.Stripe.Core`, `StripeCheckoutPaymentProvider`, `StripeRefundService` | Reuse the now-registered embedded one-time provider, PaymentIntent, refund, webhook, idempotency, and currency conversion services. Close refund-event and capability gaps before Commerce integration. |
| Taxation | `src/Modules/CrestApps.OrchardCore.Taxation`, `src/Core/CrestApps.OrchardCore.Taxation.Core` | Use for product, shipping, discount, exemption, and order tax determination. Persist its snapshots on orders and refunds. |
| Addresses | `src/Abstractions/CrestApps.OrchardCore.Addresses.Abstractions`, `IAddressResolver`, content-backed geographic hierarchy, `GeographicAreaIndex` | Use the existing normalized geographic data and resolver; add customer address books and immutable order address snapshots in Commerce. |
| Subscriptions | `src/Modules/CrestApps.OrchardCore.Subscriptions`, `src/Core/CrestApps.OrchardCore.Subscriptions.Core` | Use as the reference consumer for recurring billing, flow steps, payment UI, invoices, reports, and account dashboards. |
| Reports | `src/Abstractions/CrestApps.OrchardCore.Reports.Abstractions`, `src/Core/CrestApps.OrchardCore.Reports.Core`, current dashboard/receipt work | Reuse report grouping, dashboard, and rendering patterns; add order, sales, product, inventory, refund, promotion, and tax-liability providers. |
| Orchard infrastructure | Existing manifests, startup classes, migrations, indexes, permissions, recipes, deployments, and admin menu patterns | Use for feature registration, tenant-safe persistence, setup, export/import, and admin UX. |

### Important gaps and design constraints

1. `ProductPart` now contains `Sku` and decimal-based pricing in Products.Core, and a sellable-product snapshot resolver exists. It still has no variants, attributes, stock, product visibility, sale schedule, shipping data, or downloadable asset model.
2. Product ownership is now correctly separated from Payments. The remaining catalog gap is domain completeness, not ownership migration.
3. `CheckoutSession` and `CheckoutInvoice` are durable checkout records, not a customer-facing order-of-record. They do not provide order numbers, fulfillment states, order history, or returns.
4. The Checkout module is still a framework surface. It has no general storefront controller, cart, order UI, or customer account UI. Existing checkout UI and routes remain primarily subscription-specific.
5. A durable refund ledger, resolver, tax allocation, provider contract, and Stripe refund service now exist. Customer/admin refund workflows, order-linked refund history, refund-event reconciliation, and complete refund UI are still absent.
6. `StripeCheckoutPaymentProvider` now implements the generic provider seam, but it is embedded-card and one-time only. Hosted checkout and recurring capabilities are explicitly unsupported and must not be presented as available.
7. Stripe webhook handling covers payment/subscription success paths but does not yet dispatch and reconcile `charge.refunded` into the refund ledger.
8. Inventory, stock reservations, shipping, fulfillment, coupons, discounts, reviews, wishlists, and product order history are absent as commerce domains.
9. Money boundaries are now substantially decimal-based and covered by currency/type tests. A stale XML comment remains, and future order totals must continue the approved representation without reintroducing floating point.
10. The refund service must not silently produce a zero-tax refund when no tax-refund calculator is registered. Refundable taxable payments must fail explicitly or enter manual review instead.
11. Multi-instance safety is already a design requirement. Future inventory reservations, order transitions, payment mutation, webhook processing, and refunds must use durable records, distributed locks where needed, and idempotent commands.
12. The current branch has improved recipe/schema coverage for Products and Subscriptions and user-managed Tax Types with recipe/deployment support. Future Customers, Orders, Carts, Commerce, Inventory, Shipping, Promotions, Reviews, and Storefront features still require equivalent migrations, schemas/recipes, permissions, deployment support, tests, and documentation.
13. The single-merchant decision removes marketplace payouts, seller settlement, and order splitting from the first scope. Vendor/supplier references may remain as future extension points and internal cost metadata, but must not shape the first order model.
14. The current target bundle and test project include existing modules only. Every future reusable and Commerce feature must be added to the targets bundle, startup/package composition, and appropriate test coverage.

## Target architecture

### Modularity principle

The ecommerce outcome must be delivered as a composition of small, independently enableable blocks. `Commerce` is an umbrella/composition feature, not the owner of every customer, product, payment, address, order, or reporting concern.

Each module must have one bounded responsibility, its own manifest/startup/migrations/indexes/tests, and a dependency direction that does not point back from reusable modules to the ecommerce storefront. A module is reusable when its domain is meaningful without an online store; it is Commerce-specific when its behavior exists only to sell or fulfill a store order.

The following rules are mandatory:

- `Users` owns authentication, identities, roles, and user administration.
- `Customers` owns buyer/customer profiles and account identity, but not orders, subscriptions, products, or payment-provider implementations.
- `Addresses` owns geographic reference data and address resolution. Customer address books belong to `Customers`; immutable transaction address snapshots belong to the consuming order/sales module.
- `Products` owns catalog content and sellable product definitions. It must not depend on Commerce storefronts or Stripe.
- `Orders` owns generic sales-order lifecycle and immutable commercial snapshots. It must not own storefront pages, inventory implementation, or a specific payment provider.
- `Carts` owns reusable cart state and merge/expiration behavior. It must not own payment mutation or order fulfillment.
- `Checkout` and `Payments` remain reusable payment orchestration/provider infrastructure.
- `Inventory`, `Shipping`, `Promotions`, and `Reviews` are separate commerce capabilities with explicit contracts.
- `Storefront` and `Admin` are adapters/presentation features. Domain modules must not depend on their controllers, views, or view models.
- `Subscriptions` and `Commerce` consume shared Customers, Products, Orders, Checkout, Payments, Taxation, Addresses, and Reports contracts; neither is the owner of those shared domains.

### Solution shape

Create one ecommerce solution composed from the following reusable and commerce-specific modules:

#### Reusable platform and domain modules

- `CrestApps.OrchardCore.Users` and `CrestApps.OrchardCore.Users.Abstractions/Core`
  - Authentication-facing user administration and identity primitives. Existing module; do not duplicate it in Commerce.
- `CrestApps.OrchardCore.Customers` and `CrestApps.OrchardCore.Customers.Abstractions/Core`
  - New reusable customer domain: customer profile, person/business type, contact data, guest identity, authenticated-user link, customer merge, account status, customer tax-profile reference, customer address-book references, and provider-neutral external identifiers.
  - Must be usable by Subscriptions, Commerce, bookings, service requests, and other future solutions without depending on Orders or Commerce.
- `CrestApps.OrchardCore.Addresses` and `CrestApps.OrchardCore.Addresses.Abstractions`
  - Existing geographic content hierarchy and address resolution. Add only generic address normalization/snapshot contracts; do not place orders or storefront behavior here.
- `CrestApps.OrchardCore.Products` and `CrestApps.OrchardCore.Products.Core`
  - Existing reusable catalog/content module, expanded with variants, attributes, sellable snapshots, pricing metadata, and fulfillment classification.
- `CrestApps.OrchardCore.Orders` and `CrestApps.OrchardCore.Orders.Abstractions/Core`
  - New reusable sales-order module for immutable commercial snapshots, order numbers, lifecycle, customer ownership, addresses, payment references, fulfillment references, notes, and audit history.
  - Subscriptions may consume it for invoices or subscription purchases; it must not depend on Commerce storefronts.
- `CrestApps.OrchardCore.Carts` and `CrestApps.OrchardCore.Carts.Abstractions/Core`
  - New reusable cart module for line identity, quantity, selection metadata, guest ownership, authenticated ownership, merge, expiration, and cart persistence.
  - It may support ecommerce, subscriptions, bookings, quotes, and service bundles, but it must not call payment providers or reserve inventory directly.
- Existing `CrestApps.OrchardCore.Checkout`, `Payments`, `Taxation`, `Reports`, and provider modules
  - Continue as reusable infrastructure. Their contracts must not depend on Commerce.

#### Commerce-specific modules

- `CrestApps.OrchardCore.Commerce`
  - Composition feature only: feature dependencies, shared Commerce registration, migrations that truly belong to Commerce, permissions, recipes, deployments, and cross-domain orchestration. It must not become a catch-all domain assembly.
- `CrestApps.OrchardCore.Commerce.Inventory`
  - Stock items, reservations, backorders, stock movements, allocation rules, adjustments, and inventory reports.
- `CrestApps.OrchardCore.Commerce.Shipping`
  - Shipping zones, methods, rates, packages, shipments, fulfillment, tracking, and shipping reports.
- `CrestApps.OrchardCore.Commerce.Promotions`
  - Coupons, discounts, campaigns, eligibility, usage limits, stacking, and promotion snapshots.
- `CrestApps.OrchardCore.Commerce.Reviews`
  - Product reviews, ratings, moderation, verified-purchase rules, and aggregates.
- `CrestApps.OrchardCore.Commerce.Storefront`
  - Product browsing, cart/checkout presentation, account/order pages, receipt/download endpoints, and customer-facing APIs.
- `CrestApps.OrchardCore.Commerce.Admin`
  - Store administration screens for orders, inventory, shipping, promotions, reviews, and commerce settings. If the repository convention favors feature-local admin views, keep the boundary as a feature even when its files live beside the domain module.

#### Provider and integration adapters

- Existing Stripe checkout/refund features implement the provider contracts. They must remain independent of Orders and Commerce, using generic references and metadata.
- Future payment, tax, shipping, fulfillment, search, analytics, and notification providers must be add-on adapters, not edits to the Commerce core.

The exact project count may be adjusted after dependency analysis, but the bounded responsibilities and dependency direction may not be collapsed into one `Commerce.Core` catch-all.

### Proposed dependency graph

```text
Users ───────────────┐
Addresses ───────────┼──> Customers ──────────────┐
Taxation contracts ──┘                             │
                                                   ├──> Orders ───────┐
Products ──────────────────────────────────────────┘                   │
Carts ────────────────────────────────────────────────────────────────┤
Checkout ──> Payments ──> Stripe/other providers                      │
Taxation ──────────────────────────────────────────────────────────────┤
                                                                        ├──> Commerce orchestration
Inventory ─────────────────────────────────────────────────────────────┤
Shipping ──────────────────────────────────────────────────────────────┤
Promotions ────────────────────────────────────────────────────────────┤
Reviews ───────────────────────────────────────────────────────────────┤
                                                                        ├──> Storefront
                                                                        └──> Admin
Subscriptions ──> Customers, Products, Orders, Checkout, Payments, Taxation, Reports
```

The graph is conceptual. Actual project references must be checked for cycles, and optional features should use Orchard `[RequireFeatures]` or provider interfaces rather than hard project references.

### Domain ownership

- **Customers** owns reusable customer identity/profile behavior.
- **Products** owns sellable catalog content and product composition.
- **Carts** owns cart state, ownership, merge, and expiration.
- **Orders** owns immutable sales-order snapshots, order lifecycle, and order history.
- **Commerce** composes those modules and owns only store-specific orchestration and policies.
- **Checkout** owns payment-safe orchestration and reconciliation.
- **Payments/Stripe** owns gateway communication and provider-specific webhook translation.
- **Taxation** owns tax determination and historical tax snapshots.
- **Inventory** owns availability and reservations as a Commerce-specific feature.
- **Shipping** owns shipping quotes and fulfillment state as a Commerce-specific feature.
- **Promotions** owns discount rules and promotion snapshots as a Commerce-specific feature.
- **Reviews** owns ratings and moderation as a Commerce-specific feature.
- **Subscriptions** remains the recurring-billing consumer and shares common order/payment concepts only through explicit contracts.

### Money and audit model

Before adding order persistence:

1. Define the authoritative commerce money type and currency policy. Prefer decimal-based domain values or a minor-unit value object for persisted order data.
2. Keep provider-specific minor-unit conversion inside gateway adapters.
3. Keep `Money`, `CurrencyScale`, tax snapshots, payment attempts, order totals, refund totals, and receipt totals consistent.
4. Never calculate order totals from current product values after order placement. Store immutable line snapshots containing product/variant identity, SKU, title, quantity, unit price, tax classification, discount allocation, and currency.
5. Use `IClock` for all timestamps and preserve the repository's tenant-scoped YesSql conventions.

### Checkout-to-order lifecycle

The canonical flow should be:

1. Resolve product and variant selections from the cart.
2. Validate product publication, price, quantity, customer restrictions, tax context, shipping destination, promotion eligibility, and stock.
3. Create a durable draft order/quote before any external payment mutation.
4. Create a checkout session referencing the draft order, not an arbitrary product.
5. Reserve inventory with an expiring reservation tied to the draft order and checkout session.
6. Recalculate the authoritative invoice from the order snapshot, including shipping, discounts, and tax.
7. Create durable payment attempts before calling Stripe or another provider.
8. Begin provider payment, persist the provider reference immediately, and reconcile only against the provider's authoritative API.
9. Mark the order paid only after all expected obligations are settled.
10. Convert reservations to stock movements, create fulfillment tasks, send notifications, and expose the order to the customer.
11. On expiry, cancellation, provider failure, or compensation failure, keep explicit state and release or review reservations rather than silently deleting records.

This preserves the existing payment safety guarantees while giving the store a durable order anchor.

## Phased delivery plan

Each phase has a goal, workstreams, dependencies, and an exit gate. The phases are ordered so a thin end-to-end vertical slice can be demonstrated early without weakening the architecture.

### Phase -1 — Foundation gap closure before Commerce

**Relative effort:** XL

**Goal:** Close the remaining F3–F6 gaps so Commerce-specific features start with stable payment/refund, address, checkout, verification, and module-boundary contracts. F1 and F2 are already closed in the current branch.

Work:

- Verify the completed F1 money migration and remove its remaining stale documentation claim.
- Preserve and verify the completed F2 Product ownership migration without breaking existing content or tests.
- Make the embedded one-time Stripe capability explicit and remove any misleading hosted-checkout assumptions.
- Add refund-event reconciliation and explicit missing-tax-calculator behavior.
- Establish the typed order-to-checkout relationship and address snapshot contract.
- Complete the missing migration/index/permission/provider lifecycle coverage, then run the foundation regression suite and update the affected module documentation before introducing Commerce-specific projects.

**Exit gate:** F3–F6 are closed, all foundation tests pass, and the remaining gaps are intentionally reusable-block or Commerce-domain work listed in this plan.

### Phase 0 — Architecture decisions and compatibility baseline

**Relative effort:** M

**Goal:** Freeze the Commerce domain boundaries and protect the now-existing shared foundations and Subscriptions behavior.

Work:

- Create or update an architecture decision record for:
  - Product/content-item identity versus SKU/variant identity.
  - Order aggregate and state machine.
  - Money representation and currency rules.
  - Guest cart ownership and authenticated cart merge.
  - Inventory reservation timing and expiry.
  - Payment/refund provider contracts.
  - Shipping and tax ordering.
- Inventory the current subscription checkout endpoints and identify reusable generic pieces.
- Reference the completed Foundation gap decisions instead of reopening them during Commerce implementation.
- Define feature IDs, package/project boundaries, permissions, indexes, recipes, deployments, and migration versioning.
- Define security, privacy, retention, audit, and tenant-isolation requirements.

**Exit gate:** Approved domain glossary, state diagrams, module dependency graph, migration strategy, and Commerce-specific compatibility plan. No Commerce implementation begins before Phase -1 and this gate.

### Phase 0.5 — Reusable Customers module

**Relative effort:** L

**Goal:** Build the customer block as a reusable domain that Subscriptions and Commerce can consume independently.

Work:

- Create `Customers.Abstractions`, `Customers.Core`, and the Orchard `Customers` feature with separate manifests/startup, migrations, indexes, permissions, and tests.
- Model customer profiles independently from Orchard Users:
  - Authenticated user link.
  - Guest customer identity and later guest-to-user merge.
  - Person/business classification.
  - Contact details and communication preferences.
  - Account status and customer metadata.
  - Provider-neutral external identifiers.
  - Optional tax-profile reference through an integration contract.
- Add customer address-book references using the existing Addresses resolver without moving geographic content into Customers.
- Define customer ownership and authorization contracts that Orders, Subscriptions, and future modules can consume.
- Refactor subscription customer/profile access to use the reusable Customer contracts where it currently owns duplicate behavior.
- Keep Stripe customer IDs in the Stripe adapter or a provider mapping feature; do not couple Customers to Stripe.

**Exit gate:** Subscriptions can use the Customers module without depending on Commerce, guest customers can be linked to authenticated users through an explicit merge operation, and no Customer project references Orders, Carts, Commerce, Storefront, or Stripe.

### Phase 1 — Catalog and product model

**Relative effort:** XL

**Goal:** Extend the existing SKU and sellable-snapshot foundation into a usable catalog without duplicating payment or checkout concerns.

Work:

- Add or evolve product parts for:
  - Base product identity and description.
  - Complete SKU uniqueness and product-code rules on top of the existing `ProductPart.Sku`.
  - Currency-aware regular/sale pricing with sale start/end dates.
  - Product type: physical, digital, service, subscription, variable.
  - Visibility: visible, catalog, search, hidden.
  - Media: primary image and gallery.
  - Taxation classification through the existing Taxation part.
  - Shipping class, weight, dimensions, virtual/downloadable flags.
  - Purchase limits, sold-individually, and backorder policy.
  - Related products and product attributes.
- Model variable products as a parent product plus immutable variation/SKU records with their own price, stock identity, attributes, and downloadable data.
- Keep grouped and external/affiliate products out of the first release but define extension contracts for them.
- Consume the catalog ownership migration completed in Phase -1; do not add new catalog types under Payments.
- Add product content definitions, recipes, deployment support, admin editors, permissions, and indexes.
- Extend `ISellableProduct` and `IProductSnapshotResolver` with variant identity, pricing schedule, visibility, fulfillment metadata, and stable tax/product classification needed by Commerce.

**Exit gate:** A product and a variable-product variation can be created, published, queried, priced for a currency, classified for tax, and resolved into a stable sellable snapshot without any checkout or payment dependency on product editor internals. The existing SKU/snapshot tests remain green.

### Phase 2 — Addresses, pricing, and order foundation

**Relative effort:** XL

**Goal:** Establish the reusable Orders module before implementing store workflows.

Work:

- Consume the existing `IAddressResolver` and content-backed geographic hierarchy for selectable and normalized address data.
- Add reusable billing/shipping address snapshots, customer address-book support for authenticated users, and guest address capture.
- Create `Orders.Abstractions`, `Orders.Core`, and the Orchard `Orders` feature for `Order`, `OrderLine`, `OrderAddress`, `OrderTotals`, `OrderPayment`, `OrderFulfillment`, `OrderStatus`, and audit/event records.
- Add order number generation, external reference, customer ownership, guest access token, and tenant scope.
- Link orders to Customers through stable customer contracts; do not duplicate customer profile data inside Orders.
- Snapshot all line data, product identity, variation/SKU, price, currency, tax classification, discount allocation, and shipping selection.
- Persist tax lines and the immutable `TaxSnapshot` on the order.
- Define valid order and payment transitions with idempotent command handlers.
- Add YesSql indexes for order number, customer, status, date, payment state, fulfillment state, SKU, and provider transaction.
- Add admin permissions, navigation, list filters, detail views, notes, and audit history.

**Exit gate:** A draft order can be created from a sellable snapshot, queried by customer and administrator, and remains historically correct after the product price or tax rule changes. Orders can be consumed without enabling the Commerce storefront.

### Phase 3 — Cart and product checkout vertical slice

**Relative effort:** XL

**Goal:** Deliver the first complete one-time purchase path using the reusable Carts, Customers, Products, Orders, Checkout, Payments, and Taxation modules.

Work:

- Implement the `Carts` module for guest and authenticated carts.
- Store carts durably with an anonymous cookie/token and merge them on login.
- Support add, remove, update quantity, select variation, clear, expiration, and stale-price handling.
- Validate cart lines against product publication, current price policy, purchase limits, downloadable/service constraints, and tax classification.
- Create a draft `Orders` record from the cart and connect it to `CheckoutSession.ReferenceType = Order`.
- Add generic commerce checkout steps for contact, billing address, shipping address where needed, shipping method, review, payment, and confirmation.
- Extract or generalize subscription checkout presentation code without changing subscription behavior.
- Add the first `Commerce.Storefront` pages and endpoints:
  - Product list/detail.
  - Add-to-cart and cart summary.
  - Checkout.
  - Confirmation.
  - Authenticated order history.
  - Guest order lookup with a non-guessable token.
- Add server-side totals and do not trust client-submitted amounts.

**Exit gate:** A guest or authenticated customer can purchase a physical, digital, service, or variable product through a real cart, receive a durable order, and see the order after payment. Existing Subscriptions checkout tests and behavior remain green.

### Phase 4 — Provider-neutral payment completion and refunds

**Relative effort:** XL

**Goal:** Connect orders to the existing provider-safe payment/refund architecture and finish the remaining provider lifecycle work.

Work:

- Integrate the existing `StripeCheckoutPaymentProvider` with order references, deterministic idempotency keys, and webhook correlation. Do not assume hosted checkout or recurring capability.
- Use the existing additive `ICheckoutPaymentRefundProvider` contract, refund resolver, refund store, and lock-based refund service rather than creating a second refund ledger.
- Add full and partial refund commands with:
  - Durable refund records.
  - Idempotency keys.
  - Provider reference and status.
  - Requested, confirmed, failed, and manual-review states.
  - Order, payment attempt, and line allocations.
- Reuse `ITaxRefundCalculator` and the original tax snapshot. Never recalculate refund tax from current rules.
- Complete Stripe refund implementation integration, explicit Pay Later/manual-refund behavior, and refund-event webhook dispatch/reconciliation.
- Add payment dispute/chargeback extension points and webhook processing.
- Add customer refund request and administrator approval paths, while keeping provider mutation authorization server-side.
- Preserve payment-attempt reconciliation, underpayment detection, currency validation, and crash recovery.

**Exit gate:** A product order can be paid and reconciled through the existing Stripe adapter, fully or partially refunded through the existing refund ledger, and reprinted with historically correct totals and tax. Duplicate payment/refund webhooks, duplicate refunds, provider timeout, missing tax calculator, and partial failure tests pass.

### Phase 5 — Inventory and stock operations

**Relative effort:** XL

**Goal:** Prevent overselling and provide operational stock management.

Work:

- Define stock item/SKU, on-hand quantity, reserved quantity, available quantity, reorder threshold, backorder policy, and stock movement.
- Support the requested controls:
  - Manage stock.
  - In stock/out of stock.
  - Allow, disallow, or allow-with-notification backorders.
  - Sold individually.
  - Maximum items per order.
- Reserve inventory against a draft order/checkout session with an expiry.
- Use distributed locking and atomic durable updates for last-unit contention.
- Release reservations on abandoned checkout, timeout, failed payment, cancellation, and compensation failure according to explicit state rules.
- Convert reservations into stock movements only after payment/order completion.
- Restock for approved returns/refunds according to product policy.
- Add administrator stock adjustment, import/export, low-stock notifications, and inventory reports.
- Add background tasks for expired reservation cleanup and reconciliation.

**Exit gate:** Concurrent checkout tests prove that two buyers cannot reserve the same last unit. Every reservation has an auditable owner, expiry, release reason, and resulting stock movement.

### Phase 6 — Shipping and fulfillment

**Relative effort:** L

**Goal:** Support physical product fulfillment without affecting virtual/downloadable/service checkout.

Work:

- Extend address validation and customer address storage.
- Define shipping zones, countries/regions/postal rules, shipping classes, methods, rates, free-shipping thresholds, and provider interfaces.
- Add built-in flat-rate, table-rate, and free-shipping methods.
- Add an extension seam for carrier/rate providers and tracking.
- Add a shipping checkout step that is skipped for entirely virtual/downloadable/service orders.
- Add shipping as an invoice line with tax classification and `AppliesToShipping` integration.
- Persist selected method, rate, package, shipment, label/tracking reference, and fulfillment status on the order.
- Add split shipment extension points even though the first release is single-merchant.
- Add administrator fulfillment screens and customer shipment tracking.

**Exit gate:** Mixed physical/digital carts calculate shipping correctly, physical orders can be fulfilled and tracked, and virtual-only orders never require shipping data.

### Phase 7 — Promotions, discounts, and customer purchase history

**Relative effort:** L

**Goal:** Provide common store promotion behavior and complete account workflows.

Work:

- Define coupon/promotion rules:
  - Fixed amount and percentage discounts.
  - Product/category restrictions.
  - Validity windows.
  - Minimum/maximum spend.
  - Usage limits per coupon and customer.
  - Single-use and stacking rules.
  - Free shipping.
  - Exclusions for sale items or subscription products where needed.
- Apply discounts before tax using explicit line/order allocation and snapshot the result on the order.
- Add promotion administration, import/export, and audit history.
- Add customer account screens for:
  - Order history.
  - Order detail.
  - Printable/downloadable receipt.
  - Addresses.
  - Downloads.
  - Cancellation/refund request.
  - Subscription links where applicable.
- Add invoice/receipt numbering and a stable rendering model. Use existing report/document infrastructure where suitable; do not make receipt generation depend on a transient checkout session.

**Exit gate:** Promotions are deterministic and auditable, order totals remain immutable after purchase, and customers can retrieve every completed order and receipt.

### Phase 8 — Reviews, analytics, and administration hardening

**Relative effort:** L

**Goal:** Complete the common store experience and operational visibility.

Work:

- Add review/rating content tied to products and optionally verified purchases.
- Add moderation, abuse controls, display rules, rating aggregates, and review permissions.
- Add sales and commerce reports:
  - Gross/net sales.
  - Orders by status.
  - Product and variation performance.
  - Tax collected.
  - Refunds and disputes.
  - Inventory movement and low stock.
  - Coupon usage.
  - Customer purchase history.
- Add CSV/OpenXml exports through the existing Reports framework.
- Add dashboard widgets and admin filters.
- Add API authorization, pagination, rate limiting, idempotency, and problem-details error responses.
- Add accessibility, localization, responsive storefront, SEO/route integration, and cache invalidation.

**Exit gate:** Store operators can manage catalog, orders, stock, shipping, promotions, refunds, reviews, and reports from the admin UI, while customers can complete the full purchase lifecycle from the storefront or API.

### Phase 9 — Release, deployment, and operational readiness

**Relative effort:** L

**Goal:** Package the solution for real tenants and protect existing consumers.

Work:

- Add manifests, feature dependencies, permissions, setup recipes, deployment steps, and sample content definitions.
- Add upgrade migrations from the current `ProductPart` shape and preserve existing subscription product behavior.
- Add feature profiles for a minimal store, digital store, physical store, and full store.
- Add structured logging, correlation IDs, audit events, health checks, metrics, and failed-payment/refund/reservation alerts.
- Document tenant isolation, Redis/distributed-lock requirements, webhook setup, payment secrets, tax setup, shipping setup, and backup/restore behavior.
- Update module READMEs, Docusaurus module pages, feature reference, changelog, API documentation, and migration notes.
- Validate package discoverability through the targets project and startup web application.

**Exit gate:** Clean restore/build/test in a network-capable environment, asset and docs builds pass, deployment import/export works on a fresh tenant, upgrade tests pass, and existing Products, Checkout, Payments, Taxation, and Subscriptions behavior remains compatible.

## Cross-cutting acceptance criteria

### Modularity and reuse

- Customers, Products, Addresses, Orders, Carts, Checkout, Payments, Taxation, Reports, and Users can be enabled and consumed without enabling the Commerce Storefront.
- Subscriptions consumes Customers, Products, Orders, Checkout, Payments, Taxation, and Reports through contracts; it does not reference Commerce controllers, views, inventory, shipping, promotions, or reviews.
- Commerce orchestration depends on reusable domain contracts and commerce capability modules; reusable modules never depend on Commerce.
- No module contains a second copy of customer identity, address geography, product pricing, payment provider logic, tax calculation, or report infrastructure.
- Each feature has an explicit manifest dependency list, tenant-safe migrations/indexes, permissions, tests, and documentation.
- A module can be reused by a non-store solution without importing storefront routes or ecommerce-only persistence.
- Optional integrations use provider contracts and Orchard feature gating rather than circular project references.

### Financial correctness

- All order, payment, tax, discount, refund, and receipt calculations use the approved money policy.
- Currency precision is tested for zero-, two-, and three-decimal currencies.
- Provider amounts are converted only at the gateway boundary.
- Orders and refunds are immutable audit records with explicit state transitions.
- Tax snapshots and refund allocation use historical data only.

### Payment correctness

- Every provider mutation is idempotent.
- Payment attempts are durable before provider calls.
- Provider references are persisted immediately.
- Completion requires authoritative verification.
- Webhooks are signature-verified, deduplicated, locked, and retryable.
- Refund events are reconciled into the existing `PaymentRefund` ledger.
- Unsupported hosted or recurring capabilities are rejected explicitly.
- Underpayment, wrong currency, missing transaction, provider failure, timeout, and crash recovery are tested.

### Foundation readiness

- Product models are owned by Products rather than Payments, or the compatibility façade and deprecation path are documented and tested.
- New commerce contracts do not use floating-point money.
- The sellable snapshot contains enough immutable information for pricing, tax, inventory, shipping, digital delivery, and order history.
- Billing and shipping addresses can be normalized from the Addresses resolver and persisted as immutable order snapshots.
- Missing tax-refund infrastructure produces an explicit failure or manual-review state; it never silently creates a zero-tax refund.

### Inventory correctness

- A reservation cannot exceed available stock unless the product permits backorders.
- Last-unit concurrent checkout is serialized.
- Expiration, cancellation, failed payment, refund, and restock are auditable.
- Inventory state can be rebuilt from stock movements.

### Security and privacy

- Guest order lookup uses expiring, non-guessable access tokens.
- Customer and administrator permissions are checked on every read and mutation.
- Payment secrets and personal data are not logged.
- Webhooks validate signatures before deserialization or mutation.
- Sensitive customer data has retention and deletion rules.
- All tenant keys, cache keys, locks, indexes, and background work are tenant-scoped.

### Test strategy

- Unit tests for money, pricing, tax allocation, promotion rules, state transitions, and provider adapters.
- Integration tests for YesSql stores, migrations, indexes, recipes, deployment, and feature dependencies.
- End-to-end tests for guest checkout, authenticated checkout, variable products, mixed carts, digital delivery, shipping, refunds, and order history.
- Concurrency tests for inventory reservation and order transitions.
- Failure-injection tests for payment crashes, webhook retries, refund retries, reservation expiry, and distributed lock contention.
- Regression tests for all existing Subscription checkout and payment flows.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Breaking Subscriptions while extracting generic checkout UI | Extract by contract, keep subscription-specific steps in Subscriptions, and run subscription regression tests at every phase. |
| Money precision mismatch | Approve one domain representation before order persistence; add conversion tests before migrating fields. |
| Stripe provider capabilities are overstated | Treat the current provider as embedded one-time PaymentIntent checkout; add hosted or recurring support only through explicit capability and test changes. |
| Refund ledger exists but is not fully reconciled | Add refund-event webhook dispatch, order linkage, UI, and retry tests before exposing refunds to customers. |
| Module boundaries are only conceptual | Add Customers, Orders, Carts, and Commerce as separate projects/features, include them in the targets bundle, and enforce the dependency graph with boundary tests. |
| Refund tax can silently become zero | Require a registered tax-refund calculator or transition the refund to explicit manual review. |
| Charging without a durable fulfillment record | Create a draft order and frozen quote before payment attempts. |
| Overselling under multiple nodes | Durable reservations, distributed locks, atomic transitions, and concurrency tests. |
| Tax changes altering historical receipts | Persist tax snapshots and order line classifications; refund only from snapshots. |
| Overbuilding a marketplace | Keep single-merchant boundaries; defer vendor settlement and commissions. |
| A monolithic module becoming unmaintainable | Keep independent abstractions, core services, and Orchard features even when initially packaged together. |
| Scope becoming too broad for one release | Use the phase gates and require acceptance criteria before enabling the next feature group. |

## Independent design audit and current code verification

Three independent Claude Opus 4.8 reviews were previously run against the repository. A new two-track source audit was then run against the current branch after the refund, SKU/snapshot, Stripe-provider, address, report, and receipt changes. The current source audit supersedes stale claims from the original plan.

### Consensus

The original design reviews agreed that:

1. The payment, checkout, tax, currency, webhook, distributed-safety, and Orchard module foundations are valuable and should be reused.
2. A WooCommerce-like store is not present today; cart, order, inventory, shipping, promotions, storefront, and customer-facing refund workflows are missing or only represented by partial contracts.
3. A durable Order aggregate must be introduced before order history, fulfillment, refunds, inventory decrement, and commerce reports can be reliable.
4. Inventory reservation concurrency and money representation are the highest technical risks.
5. Tax snapshots and the existing payment reconciliation invariants must remain authoritative.
6. The solution should be modular and feature-gated, with Subscriptions remaining a consumer rather than becoming the owner of generic commerce.

### Current branch findings incorporated

- Refund infrastructure is no longer absent. `PaymentRefund`, `RefundStatus`, refund abstractions, YesSql persistence/indexes/migrations, resolver, lock-based orchestration, Stripe refund service, and tax allocation are implemented.
- Generic Stripe checkout is no longer absent. `StripeCheckoutPaymentProvider` is registered and supports embedded one-time PaymentIntent checkout, but hosted and recurring capabilities are explicitly unsupported.
- Product ownership, SKU, decimal pricing, and the sellable snapshot seam are now implemented. `ProductPart` is owned by Products.Core, and `ISellableProduct`, `SellableProduct`, and `DefaultProductSnapshotResolver` are present.
- Addresses now include a content-backed geographic hierarchy, `GeographicAreaIndex`, and `IAddressResolver`. Customer address books and immutable order snapshots remain Commerce work.
- The remaining greenfield domains are cart, order, inventory, shipping, promotions, reviews, generic storefront/API, and commerce-specific reports.
- F1 money unification and F2 catalog ownership are closed. The remaining foundation gaps are refund-event reconciliation, explicit refund-tax failure behavior, the typed Order-to-Checkout contract, incomplete baseline verification, and enforcement of the reusable module graph.
- Payment and refund stores now use the shared DocumentCatalog persistence convention and business `ItemId` naming. This is reusable payment infrastructure, not an Order ledger; Orders still needs its own durable aggregate.
- Product and Subscription recipe/schema coverage and user-managed Tax Type recipe/deployment support have improved, but every future module still needs equivalent setup, migration, deployment, and test coverage.

- The architecture review identified the mature horizontal safety engine and required durable, tenant-scoped inventory and order mutations.
- The domain review ranked catalog, addresses, orders, cart, refunds, receipts, inventory, variants, shipping, promotions, reports, reviews, and marketplace concerns by dependency. The single-merchant decision removes marketplace work from the first scope.
- The commerce audit identified the need to integrate the now-existing generic Stripe/refund adapters with Commerce and to extract subscription-shaped checkout UI into reusable contracts without changing subscription behavior.

### Resolution of review tensions

- **Payment-first versus catalog-first:** The plan defines architecture and money/order contracts first, then delivers a thin catalog-to-order-to-payment vertical slice before expanding inventory, shipping, and promotions.
- **One module versus many modules:** The plan presents one ecommerce solution but preserves independent feature boundaries so tenants can enable only the capabilities they need.
- **Immediate refund interface change versus compatibility:** The plan now uses the existing additive refund contract and ledger, and limits remaining work to order linkage, event reconciliation, UI, and operational tests.

The reviewers and current source audit support the same final direction: close the foundation gaps first, preserve the existing horizontal foundations, build the missing commerce domain around a durable order, and deliver the store in gated vertical phases.

## Final readiness checklist before implementation

- [x] Close Foundation gap F1 in code: unify money and amount boundaries.
- [ ] Complete the remaining F1 documentation cleanup in `Subscriptions.Core/Money.cs`.
- [x] Close Foundation gap F2: correct catalog ownership and preserve compatibility.
- [ ] Close Foundation gap F3: finish generic Stripe/refund lifecycle and capability tests.
- [ ] Close Foundation gap F4: stabilize address snapshots and the order-to-checkout contract.
- [ ] Close Foundation gap F5: complete baseline verification and documentation.
- [ ] Close Foundation gap F6: enforce reusable module boundaries and dependency direction.
- [x] Confirm the approved money representation and migration strategy.
- [ ] Approve the Order and checkout state machines.
- [ ] Approve the feature/project dependency graph.
- [ ] Approve guest cart ownership, merge, and order lookup rules.
- [ ] Approve inventory reservation timing, expiry, and backorder policy.
- [x] Confirm the additive refund-provider contract.
- [ ] Approve the single-merchant boundary and defer marketplace features.
- [ ] Approve the storefront/API boundary and supported delivery channels.
- [ ] Approve the phase gates and acceptance tests.

The plan is not ready to begin Commerce-specific implementation until the remaining F3–F6 work is closed. Customers, Orders, and Carts do not exist yet and may be implemented as reusable blocks only after their boundaries and contracts are approved; Products is the only one of these reusable blocks currently present.
