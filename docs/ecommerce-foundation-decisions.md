# E-commerce foundation decision record

This document records the binding foundation decisions that close the gaps listed in
`ecommerce-plan.md` (F0–F5). It exists so a planning engineer can re-evaluate the plan and confirm
that the reusable foundation is complete and internally consistent before any e-commerce domain
module (Customers, Orders, Carts, Storefront, Commerce) is written.

Two kinds of decisions appear below:

- **Enforced now** — the reusable foundation code and/or architecture tests already guarantee the
  decision. Changing it later is a breaking change and is intentionally locked in while breaking
  changes are still cheap.
- **Approved default** — a concrete recommended answer for an open product decision (F5). The
  foundation does not yet contain domain code for these, but the recommended answer is chosen so it
  can be built on the current contracts without a future breaking change.

## F0 — Verified taxation baseline

- **Enforced now.** A `TaxTable` is an effective-dated catalog document. `TaxService` never applies
  a table outside its `EffectiveFromUtc`/`EffectiveToUtc` window for the transaction date; a rule
  that requires a table but has no effective table on that date is skipped rather than silently
  taxed at zero.
- **Enforced now.** `TaxTable` rows are validated on write: minimums cannot be negative, at most one
  open-ended (no-maximum) row is allowed, bounded ranges must be ordered and non-overlapping, and an
  open-ended row must start at or above the highest bounded maximum. This prevents the
  progressive-bracket double-counting class of bug.
- **Enforced now.** A `TaxTable` that is still referenced by a `TaxRule` cannot be deleted from the
  admin UI.
- **Enforced now.** Recipe import validates a candidate table (clone + populate + validate) before
  it overwrites an existing entry, so an invalid recipe cannot corrupt a stored table.
- **Money is `decimal`.** All monetary amounts in the foundation are `decimal`; provider minor-unit
  integers are converted at the provider boundary using per-currency scale, never floating point.

## F1 — Stripe event truthfulness and refund reconciliation

- **Enforced now.** The Stripe webhook dispatcher recognizes `charge.refunded`,
  `charge.refund.updated`, `refund.updated`, and `refund.failed` and routes them to the
  provider-neutral refund reconciliation path.
- **Enforced now.** Remote refunds correlate to a local `PaymentRefund` in this order: provider
  refund reference, then context idempotency key, then metadata idempotency key
  (`checkout_refund_idempotency_key`), then a deterministic aggregate key, and finally an
  amount-and-currency match against an open request. Amount matching also requires a currency match.
- **Enforced now.** `OriginalTransactionId` falls back to the charge id when no payment intent id is
  present, so legacy charge-only refunds still correlate.
- **Enforced now — gateway is authoritative.** Success is never fabricated from a webhook. A blank
  provider status maps to `Pending`, not `Succeeded`. A terminal local status is never regressed by a
  stale or out-of-order event.
- **Enforced now — no orphaned refunds.** A remote refund with no local request is quarantined
  idempotently (failure code `remote_refund_without_local_request`) for manual review instead of
  being dropped or auto-accepted.
- **Approved default — order paid state.** An order is marked paid only from the durable payment
  attempt ledger, never from a checkout session flag. See F2.

## F2 — Order-to-checkout contract

- **Enforced now (contract).** `CheckoutReferenceTypes.Order` is the canonical `ReferenceType` for
  e-commerce orders. `ReferenceId` is the order item id. `ReferenceVersionId` is set only when the
  order requires draft/quote versioning.
- **Enforced now (ownership).** The order owns the authoritative reverse link by storing the
  checkout session id. `ICheckoutSessionStore.GetByReferenceAsync` is a recovery/reconciliation
  path, not the source of truth.
- **Enforced now (settlement authority).** Payment attempts and refunds are owned by Checkout and
  link through the session and provider transaction references. An order must never be marked paid
  from a session status alone.
- **Approved default — lifecycle rules.** A guest session is currently owned by the captured client
  IP address and user agent (`CheckoutSession.IPAddress` / `AgentInfo`), not a token; sessions
  expire on the configured checkout expiry; cancellation and retry create new payment attempts
  against the same order/session pair; recovery uses the order-owned session id. As a future
  hardening, a high-entropy, hashed guest ownership token may be added to `CheckoutSession` to
  replace IP/user-agent ownership; until then the IP/user-agent model and its weaker guarantees are
  the baseline. These rules are enforced in the Orders/Checkout orchestration layer on top of the
  contract above.

## F3 — Address value contract

- **Enforced now.** The reusable resolved `Address` is an immutable snapshot (`init`-only) carrying
  recipient name, company, two street lines, country, region, county, city, district, postal code,
  and phone. `Clone()` returns an independent snapshot.
- **Enforced now.** `IAddressResolver` normalizes country codes (values of three characters or fewer
  are upper-cased; longer display-name fallbacks are preserved).
- **Approved default — requiredness by purpose.** Billing requires recipient name, at least one
  street line, city, postal code, and country. Shipping additionally requires phone when a carrier
  rate is used. Region is required only for countries with administrative subdivisions. These
  purpose rules live in the Customers/Orders layer that consumes the snapshot.
- **Approved default — editable vs. snapshot.** Customer-editable address records are mutable
  catalog data owned by Customers; an order captures an immutable `Address` snapshot at placement
  time. The immutable snapshot type is already provided by the foundation.
- **Approved default — PII access and guest tokens.** Address PII is readable only by the owning
  authenticated customer and by operators holding the customer-management permission; it is never
  exposed on an anonymous endpoint. A guest may retrieve only the addresses attached to their own
  in-flight checkout session, authorized by the same guest-session ownership guard used for the
  session (today IP/user agent; a hashed high-entropy guest token when that hardening lands). Guest
  tokens, once introduced, are single-scope (their own session/order), stored hashed, expire with
  the session, and are revoked on session completion or cancellation.
- **Approved default — PII/retention.** Address PII follows the same retention and deletion rules as
  customer records (see F5 data retention); order snapshots are retained for the order's legal
  retention period even after the source customer address is deleted.

## F4 — Reusable module graph

- **Enforced now.** Architecture tests assert the allowed reference direction:
  - Abstraction projects reference only other abstraction projects.
  - Foundation projects (Payments, Checkout, Taxation, Addresses) do not reference provider or
    presentation modules (Stripe, PayLater, Subscriptions, Storefront, Admin, Commerce).
- **Enforced now.** The payment-provider capability audit discovers every concrete provider in all
  loaded `CrestApps.OrchardCore.*` module assemblies, so a provider added in any module is checked
  automatically: `SupportsRefunds` must match `ICheckoutPaymentRefundProvider` implementation.
- **Approved default — new modules.** Every new module requires a manifest, feature dependencies,
  permissions, migrations/indexes, recipes/deployments where applicable, tests, and documentation.

## F5 — Non-negotiable domain choices

The foundation does not build these domains, but each has an approved default answer chosen to fit
the current contracts.

| Decision | Approved default answer |
| --- | --- |
| Product identity | A product is an Orchard content item. Variants are child items or structured entries; SKU is the stable merchant identity; external provider ids are stored as correlation metadata, never as the primary key. |
| Variant storage | A structured part on the product for small fixed variant sets; a separate catalog document only when variants need independent lifecycle or large cardinality. Start with the structured part. |
| Price policy | Prices are `decimal` in an explicit currency, stored tax-exclusive with a tax-inclusive display resolved by Taxation. Sale windows and quantity/customer-group tiers resolve by effective date using the same effective-dating approach as tax tables. |
| Cart persistence | Tenant YesSql document keyed by an ownership token for guests and by user id when authenticated; authenticated login merges the guest cart; carts expire on a configured window; last-write-wins with an item-level version guard. |
| Order number | Tenant-scoped monotonic sequence with a non-sequential public token; sequence allocation is retry-safe and single-allocation per order across nodes. |
| Inventory topology | Location-aware schema from the start with a single default location for v1, so multi-location does not require a later breaking migration. |
| Reservation timing | Reserve at order creation (not cart), with a configurable hold expiry and explicit release; backorder/preorder is a per-product flag. |
| Shipping provider | A provider-neutral rate contract (flat/table/free/carrier) taking zones, packages, and destination address, and returning taxable rate lines that feed Taxation. |
| Storefront mode | Server-rendered first, built on shared contracts so a headless/API surface can be added later without changing the domain. |
| Data retention | Concrete configurable defaults: customer PII retained while the account is active and for 24 months after last activity, then anonymized; guest-order PII anonymized 12 months after fulfillment; payment references (provider transaction/refund ids and last-four/brand only — never full card numbers) retained 7 years for financial/audit compliance; downloadable-entitlement grants retained for the entitlement's validity plus 12 months; audit history retained 24 months. A deletion request anonymizes customer-identifying fields but preserves immutable order snapshots and payment references under legal hold until their own window expires. Every window is a named configurable policy, not a hardcoded constant. |

## Change control

The **Enforced now** decisions are locked by code and tests. Any change to them is a deliberate
breaking change and must update the corresponding contract, tests, and this record together. The
**Approved default** decisions may be revised by the planning engineer before the owning domain
module is implemented, but revisions must remain expressible on the current foundation contracts.
