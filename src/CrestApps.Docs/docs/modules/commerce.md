---
sidebar_label: Commerce
sidebar_position: 13
title: Commerce
description: Registers the shared Commerce admin menu and its icon so every commerce-related module contributes its screens under a single, consistently branded top-level menu.
---

| | |
| --- | --- |
| **Feature Name** | Commerce |
| **Feature ID** | `CrestApps.OrchardCore.Commerce` |
| **Category** | Commerce |
| **Enabled** | By dependency only |

The **Commerce** module registers the shared **Commerce** top-level admin menu and its icon. Commerce-related modules — such as [Products](products), [Transactions](transactions), and [Taxation](taxation) — contribute their own screens under this single menu instead of each declaring their own copy of it.

## Why this module exists

Several modules add entries under a top-level **Commerce** menu. When each module declared the menu on its own, the menu icon appeared only when the specific module that carried it happened to be enabled, and it disappeared when that module was disabled. Multiple contributors to the same node also produced an inconsistent parent.

This module owns the top-level node, its identifier (`commerce`), and its icon, so the menu always renders consistently whenever any commerce-related feature is enabled.

## The feature

- **Commerce** (`CrestApps.OrchardCore.Commerce`) — Registers the Commerce admin menu and icon. The feature is **enabled by dependency only**; it offers no standalone functionality, so it is activated automatically when a module that depends on it is enabled and cannot be enabled on its own.

## Contributing to the Commerce menu

To add screens under the Commerce menu from another module:

1. Add a dependency on the `CrestApps.OrchardCore.Commerce` feature in the module manifest.

   ```csharp
   [assembly: Feature(
       Id = "My.Module",
       Category = "Commerce",
       Dependencies =
       [
           CommerceConstants.Features.Area,
       ]
   )]
   ```

2. In an `AdminNavigationProvider`, add children under the existing `S["Commerce"]` node. Do not set the node identifier or icon again; the Commerce module owns them.

   ```csharp
   builder
       .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
           .Add(S["My Screen"], S["My Screen"].PrefixPosition(), item => item
               .Action("Index", "Admin", "My.Module")
               .Permission(MyPermissions.ManageThings)
               .LocalNav()
           )
       );
   ```

`CommerceConstants.Features.Area` is exposed by `CrestApps.OrchardCore.Abstractions`, which commerce modules already reference.

## Architectural boundary — Commerce is a thin orchestrator

Commerce is a **composition and orchestration shell**, not a domain. It owns the shared admin menu and may later host cross-domain orchestration (feature profiles, shared policies, and commands that span several domains), but it must never own domain data:

- Commerce **must not** define persistence: no YesSql indexes, index providers, data migrations, or domain stores.
- Commerce **must not** own the order, cart, customer, payment, tax, receipt, or report data models. Those belong to their reusable domain modules (Orders, Carts, Customers, Transactions, Taxation, Checkout, Receipts, Reports).
- Reusable domain commands live in their owning module. Commerce only **composes** them; Storefront and Admin surfaces are adapters over those contracts, never a place to put domain logic.

This boundary is enforced by `CommerceModuleBoundaryTests`, which fail the build if the Commerce assembly references a domain persistence assembly (or YesSql) or defines a migration, index, or index provider.

## The financial-document policy seam

Money events (a settled payment, a partial payment, a refund, a chargeback, or a write-off) may need to issue different financial documents depending on the business: a simple **receipt** today, or a persisted, numbered **invoice**, **credit note**, or **refund document** once an ordering domain exists. So the modules that move money (Checkout, Payments, Transactions) must not hard-code that decision.

The provider-neutral **`CrestApps.OrchardCore.Commerce.Abstractions`** project owns this seam. It contains only contracts and takes no package or project references:

- **`IFinancialDocumentPolicy`** decides, for a `FinancialDocumentContext` (the documented record's reference, the currency, and the `FinancialDocumentReason`), which documents to issue and whether each is persisted as an immutable copy and requires a formal number — returned as an immutable `FinancialDocumentPolicyResult`.
- **`IFinancialDocumentNumberGenerator`** issues a `FinancialDocumentNumber` that pairs a tenant-scoped monotonic sequence (for internal ordering and gap detection) with a non-sequential public token (safe to show a customer without leaking document volume).

The shipped default, **`ReceiptsOnlyFinancialDocumentPolicy`**, issues a receipt only — it never persists an immutable copy and never requires a formal number — so behavior is unchanged and the existing [Receipts](receipts) service stays the only runtime dependency.

`IFinancialDocumentNumberGenerator` ships with **no default implementation on purpose**. Correct, node-safe legal numbering needs durable persistence that only exists once an Orders domain owns it, and a speculative generator would risk duplicate or reused numbers. Because the receipts-only default never requires a number, nothing resolves the generator at runtime. A future Orders domain can register a replacement policy (and a real generator) that persists numbered invoices and credit notes **without changing any caller**. `CommerceModuleBoundaryTests` also asserts that `Commerce.Abstractions` references no domain persistence or providers, keeping it a pure cross-domain contract.

## Enabling the feature

You do not enable **Commerce** directly. Enable a module that depends on it — for example [Transactions](transactions) or [Taxation](taxation) — and Commerce is enabled automatically.

## Related modules

- [Products](products) — contributes the shared **Currencies** catalog under Commerce and reuses it for product and subscription pricing.
- [Transactions](transactions) — a provider-agnostic ledger of outstanding obligations that contributes to the Commerce menu.
- [Taxation](taxation) — a provider-agnostic taxation framework that contributes to the Commerce menu.
