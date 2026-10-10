---
sidebar_label: Overview
sidebar_position: 13
title: Commerce
description: An overview of the commerce modules (checkout, payments, transactions, receipts, subscriptions, installment plans, coupons and taxes), how a purchase flows through them, and the shared Commerce feature that hosts their admin menu, list UI and financial-document policy.
user_manual:
  - user-manual/commerce/index
---

The commerce modules let an Orchard Core site take money: sell one-time purchases and recurring plans, sell whole sites, set up payment plans, track what customers owe, and send receipts and invoices. They are separate modules with clear boundaries, so a site enables only what it needs.

For the admin screens, see the User Manual's [Commerce](../user-manual/commerce/index.md) section.

## The modules

| Module | Feature | What it owns | Page |
| --- | --- | --- | --- |
| Commerce | Commerce | The **Commerce** admin menu, the shared admin list UI, and the default financial-document policy. Enabled by dependency only. | This page |
| Products | Products | The `ProductPart`, prices, and the managed **Currencies** catalog. | [Products](products) |
| Checkout | Checkout | The checkout engine: sessions, steps, the durable payment and refund ledgers, coupons, and the public checkout. | [Checkout](checkout) |
| Stripe | Stripe | The Stripe payment provider: cards, saved cards, Apple Pay and Google Pay, Connect, webhooks. | [Payments](payments) |
| Pay Later | Pay Later | An offline "pay later" payment option that records the balance as a transaction. | [Pay Later](pay-later) |
| Transactions | Transactions, Transaction Reminders, Payment Receipts | The ledger of what customers owe and paid, reminders, receipts, invoices and pay links, and the **Payments** and **Refunds** consoles. | [Transactions](transactions) |
| Receipts | Receipts | The reusable printable receipt and its branding settings. | [Receipts](receipts) |
| Subscriptions | Subscriptions, Subscriptions - Sites, Subscriptions - Installment Plans | Recurring plans and their agreements, selling sites, and administrator-made payment plans. | [Subscriptions](subscriptions), [Installment Plans](installment-plans) |
| Taxation | Taxation | Tax categories, jurisdictions, rules and calculation. | [Taxation](taxation) |

## How a purchase flows

Every way of taking money goes through the same path, so there is one ledger to reconcile against the gateway:

1. **A seller starts a checkout.** A subscription plan, a site, an installment plan's down payment, or an outstanding transaction starts a checkout session through `ICheckoutEngine`. The session references what is being paid for (`ReferenceType` and `ReferenceId`).
2. **The buyer completes the steps.** The checkout collects what the steps need, prices the order (products, coupons, taxes), and opens the payment step.
3. **A payment provider takes the money.** The payment is begun through the provider (for example Stripe) and recorded as a durable payment attempt before anything else happens.
4. **The checkout settles.** A confirmed attempt completes the session, which applies the payment to its transaction (`ITransactionSettlementService`).
5. **Handlers react.** Checkout handlers (`CheckoutHandlerBase`) let the seller finish the purchase, for example Subscriptions activates or renews an agreement and queues a site. When the payment settles a transaction, `ITransactionPaymentHandler` tells every interested module once per payment: Installment Plans moves its schedule, and Payment Receipts numbers and sends the receipt.

Refunds follow the same pattern through the durable refund ledger, and `IPaymentRefundHandler` is raised once per refund, however it finished.

## The Commerce feature

| | |
| --- | --- |
| **Feature Name** | Commerce |
| **Feature ID** | `CrestApps.OrchardCore.Commerce` |
| **Category** | Commerce |
| **Enabled** | By dependency only |

The **Commerce** feature has no screens of its own. You do not enable it directly: it is enabled when a module that depends on it is enabled, such as Checkout, Transactions or Taxation.

### Contributing to the Commerce menu

The feature owns the top-level **Commerce** admin menu node, its identifier (`commerce`) and its icon, so the menu renders consistently whenever any commerce module is enabled. To add screens under it from another module:

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

2. In an `AdminNavigationProvider`, add children under the existing `S["Commerce"]` node:

   ```csharp
   builder
       .Add(S["Commerce"], S["Commerce"].PrefixPosition(), commerce => commerce
           .AddClass("commerce")
           .Id("commerce")
           .Add(S["My Screen"], S["My Screen"].PrefixPosition(), item => item
               .Action("Index", "Admin", "My.Module")
               .Permission(MyPermissions.ManageThings)
               .LocalNav()
           )
       );
   ```

Keep `.AddClass("commerce").Id("commerce")` on the node: the icon is attached to that identifier, and a node without it would not merge with the others.

`CommerceConstants.Features.Area` is exposed by `CrestApps.OrchardCore.Abstractions`, which commerce modules already reference.

### The shared admin list UI

The commerce lists (installment plans, subscriptions, transactions, payments, refunds, coupons and others) share one layout: search in the action bar, filters in the list header, each row's details as badges, and Orchard Core's pager with a page size selector. The Commerce feature provides two pieces that a module's list can reuse:

- **The `commerce-admin-list` script.** A `<select>` with the `data-commerce-list-filter` attribute submits its filter form when it changes, so header filters work without a button. Header selects use the HTML `form` attribute to post with the filter form outside the list. Require it with `<script asp-name="commerce-admin-list" at="Foot"></script>`.
- **A page size selector that renders on every admin theme.** The feature adds the `Pager_PageSizeSelector__Properties` alternate, which reads the page sizes from the shape's properties. It works around an Orchard Core issue where the admin theme's selector template read an empty list, fixed upstream in [OrchardCMS/OrchardCore#20007](https://github.com/OrchardCMS/OrchardCore/pull/20007).

Build the pager with `new Pager(pagerParameters, pagerOptions.Value)` and carry `pageSize` through the filter post, so the selected page size survives a filter change.

### The financial-document policy

Money events (a settled payment, a partial payment, a refund, a chargeback or a write-off) may need different financial documents depending on the business: a receipt, or a persisted, numbered invoice or credit note. The modules that move money must not hard-code that decision, so the contracts live in `CrestApps.OrchardCore.Transactions.Abstractions` (namespace `CrestApps.OrchardCore.Transactions.FinancialDocuments`):

- **`IFinancialDocumentPolicy`** decides, for a `FinancialDocumentContext` (the documented record's reference, the currency, and the `FinancialDocumentReason`), which documents to issue, and whether each is persisted as an immutable copy and needs a formal number. It returns an immutable `FinancialDocumentPolicyResult`.
- **`IFinancialDocumentNumberGenerator`** issues a `FinancialDocumentNumber` for a `FinancialDocumentNumberRequest` (a `FinancialDocumentKind` and an optional series).

The defaults are:

| Service | Default | Registered by |
| --- | --- | --- |
| `IFinancialDocumentPolicy` | `ReceiptsOnlyFinancialDocumentPolicy`: a receipt only, never persisted as a legal document. | Commerce |
| `IFinancialDocumentNumberGenerator` | `SequentialFinancialDocumentNumberGenerator`: short sequential numbers per site and series, starting at 1001 (`R-1001` for receipts, `INV-1001` for invoices). | Transactions |

The sequential generator keeps the last number of each series in one document, read and saved in the caller's own unit of work, under a distributed lock held until that unit of work is saved. So two nodes or two requests never issue the same number. A number taken by work that then fails is not reused, so a failed payment can leave a gap. Replace the service to use your own numbering, for example a series per year.

### Architectural boundary

Commerce is a composition shell, not a domain:

- It defines no persistence: no YesSql indexes, index providers, data migrations or stores.
- It owns none of the order, payment, tax, receipt or report data models. Those belong to their domain modules.
- Reusable domain commands live in their owning module; Commerce only composes them.

## Related

- [Checkout](checkout), [Payments](payments) and [Pay Later](pay-later): taking money.
- [Transactions](transactions) and [Receipts](receipts): recording it, and telling the customer.
- [Subscriptions](subscriptions) and [Installment Plans](installment-plans): what is sold.
- [Products](products) and [Taxation](taxation): prices, currencies and taxes.
