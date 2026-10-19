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

This video walks through the commerce modules, their screens, configuration and extension points:

<YouTube video="commerce" title="Narrated video overview of the CrestApps Commerce modules" />

## The modules

| Module | Feature | What it owns | Page |
| --- | --- | --- | --- |
| Commerce | Commerce | The **Commerce** admin menu, the shared admin list UI, and the default financial-document policy. Enabled by dependency only. | This page |
| Products | Products, Product Listing | The `ProductPart`, prices, the managed **Currencies** catalog, whether products are sold online or only listed, and front-end listing pages with filters, sorting and product pages. | [Products](products) |
| Checkout | Checkout | The checkout engine: sessions, steps, the durable payment and refund ledgers, coupons, and the public checkout. | [Checkout](checkout) |
| Stripe | Stripe | The Stripe payment provider: cards, saved cards, Apple Pay and Google Pay, Connect, webhooks. | [Payments](payments) |
| Pay Later | Pay Later | An offline "pay later" payment option that records the balance as a transaction. | [Pay Later](pay-later) |
| Inventory | Inventory | How many units of each product and variant are on hand, with a ledger of every change and a Commerce > Inventory screen. | [Inventory](inventory) |
| Shipping | Shipping | Shipping zones by country and region, their methods with flat, table and free rates, and product weights and shipping classes. | [Shipping](shipping) |
| Customers | Customers | A record for everyone who buys, a person or a business, with an account or as a guest, linked to their Orchard Core user. | [Customers](customers) |
| Transactions | Transactions, Transaction Reminders, Send Payment Receipts | The ledger of what customers owe and paid, reminders, receipts, invoices and pay links, and the **Payments** and **Refunds** consoles. | [Transactions](transactions) |
| Receipts | Receipts | The reusable printable receipt and its branding settings. | [Receipts](receipts) |
| Subscriptions | Subscriptions, Subscriptions - Sites, Subscriptions - Installment Plans | Recurring plans and their agreements, selling sites, and administrator-made payment plans. | [Subscriptions](subscriptions), [Installment Plans](installment-plans) |
| Taxation | Taxation | Tax categories, jurisdictions, rules and calculation. | [Taxation](taxation) |

## How a purchase flows

Every way of taking money goes through the same path, so there is one ledger to reconcile against the gateway:

1. **A seller starts a checkout.** A subscription plan, a site, an installment plan's down payment, or an outstanding transaction starts a checkout session through `ICheckoutEngine`. The session references what is being paid for (`ReferenceType` and `ReferenceId`).
2. **The buyer completes the steps.** The checkout collects what the steps need, prices the order (products, coupons, taxes), and opens the payment step.
3. **A payment provider takes the money.** The payment is begun through the provider (for example Stripe) and recorded as a durable payment attempt before anything else happens.
4. **The checkout settles.** A confirmed attempt completes the session, which applies the payment to its transaction (`ITransactionSettlementService`).
5. **Handlers react.** Checkout handlers (`CheckoutHandlerBase`) let the seller finish the purchase, for example Subscriptions activates or renews an agreement and queues a site. When the payment settles a transaction, `ITransactionPaymentHandler` tells every interested module once per payment: Installment Plans moves its schedule, and Send Payment Receipts numbers and sends the receipt.

Refunds follow the same pattern through the durable refund ledger, and `IPaymentRefundHandler` is raised once per refund, however it finished.

## Take payments for your own module

The payment modules aren't tied to subscriptions or installment plans. Any module can take a payment, and gets every payment provider, coupons, taxes, receipts, the durable ledger and refunds without writing payment code. There are two ways:

| You want to | Use | How |
| --- | --- | --- |
| Sell something in a checkout the buyer goes through now | `ICheckoutEngine` and an `ICheckoutHandler` | Start a checkout with a `ReferenceType` and `ReferenceId` for what is being bought, and redirect to the `CheckoutStep` route. Your handler adds the billing items when the checkout starts, and finishes the purchase in `CompletedAsync` once the payment is confirmed. See [Extending checkout](checkout#extending-checkout). |
| Bill a customer for an amount they pay later | A transaction | Create a transaction with your own source through `ITransactionManager`. The customer sees it in My Transactions, gets reminders and an invoice with a pay link, and pays it through the same checkout. React to the payment with `ITransactionPaymentHandler`. See [Registering a transaction source](transactions#registering-a-transaction-source). |

Subscriptions and Installment Plans are built exactly this way, so they are working examples of both.

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

### Storefront account

The Commerce feature serves the buyer's account at `account` (route `CommerceAccount`, signed-in users only) and adds **My account** to Orchard Core's user menu. The account is the `commerce-account` navigation menu (`CommerceConstants.Account.MenuName`): each module adds its pages with an `INavigationProvider` that builds only for that name, and the `CommerceAccountNavigation` shape renders the menu beside each account page, marking the page whose link the current path starts with.

```csharp
public sealed class MyAccountMenu : INavigationProvider
{
    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (name == CommerceConstants.Account.MenuName)
        {
            builder.Add(S["Gift cards"], "60", item => item
                .Id("account-gift-cards")
                .AddClass("fa-solid fa-gift")
                .Action("Index", "AccountGiftCards", new { area = "My.Module" }));
        }

        return ValueTask.CompletedTask;
    }
}
```

An account page renders `await DisplayAsync(await New.CommerceAccountNavigation())` in its side column. Orders, Digital Downloads, Subscriptions, Transactions and Customers add **Orders**, **Downloads**, **Subscriptions**, **Balances and invoices**, **Addresses** and **Account details**.

### The shared admin list UI

The commerce lists (installment plans, subscriptions, transactions, payments, refunds, coupons and others) share one layout: search in the action bar, filters in the list header, each row's details as badges, and Orchard Core's pager with a page size selector. A module's list can reuse the Commerce feature's **`commerce-admin-list` script**: a `<select>` with the `data-commerce-list-filter` attribute submits its filter form when it changes, so header filters work without a button. Header selects use the HTML `form` attribute to post with the filter form outside the list. Require it with `<script asp-name="commerce-admin-list" at="Foot"></script>`.

The page size selector is Orchard Core's own `Pager_PageSizeSelector` shape. It appears when **Allow users to change the page size** is on in the pager settings.

Build the pager with `new Pager(pagerParameters, pagerOptions.Value)` and carry `pageSize` through the filter post, so the selected page size survives a filter change.

### Customer pages in the admin

Some admin pages are for customers rather than staff: **My Plans**, **My Sites**, **My Transactions** and the printable invoices and receipts they link to. Customers who buy through the checkout have no admin access, so Orchard Core's admin filter would answer 403 on those pages.

A controller (or action) marked with `[CustomerAdminPage]` (`CrestApps.OrchardCore.Commerce.CustomerAdminPageAttribute`, in `CrestApps.OrchardCore.Abstractions`) can be opened by any signed-in user. The Commerce feature's `CustomerAdminPageAuthorizationHandler` grants `AdminPermissions.AccessAdminPanel` for the request, but only while the endpoint carries the marker. The dashboard and every other admin page keep requiring the permission, and anonymous visitors are still sent to sign in.

The marker opens the page, not the data. Every marked action must check the customer's own permission, such as `ViewOwnTransactions` or `ManageOwnSubscriptions`, and return only that customer's records:

```csharp
[Admin("my-subscriptions/{action}/{itemId?}", "MySubscriptions{action}")]
[CustomerAdminPage]
public sealed class MySubscriptionsController : Controller
{
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            return Forbid();
        }

        // Only the current user's subscriptions.
    }
}
```

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

### Personal data requests

Access and erasure requests (GDPR, CCPA) need everything commerce keeps about one person, from every module that keeps some. The Commerce feature registers **`ICommercePersonalDataService`** (`CrestApps.OrchardCore.Customers.Abstractions`, namespace `CrestApps.OrchardCore.Customers.PersonalData`), which composes what the enabled modules contribute:

```csharp
public interface ICommercePersonalDataService
{
    Task<CommercePersonalDataExport> ExportAsync(CustomerOwner owner, string email, CancellationToken cancellationToken = default);

    Task<CommercePersonalDataAnonymizeResult> AnonymizeAsync(CustomerOwner owner, string email, string replacementText, CancellationToken cancellationToken = default);
}
```

The person is named by their owner (`CustomerOwner.ForUser(userId)`, or `CustomerOwner.ForGuest(guestOwnerId)` for a guest id an application derives itself), by their email, or by both; one of the two is required. The email is matched without case sensitivity.

- **`ExportAsync`** returns a `CommercePersonalDataExport` with typed records: `Transactions` (with the invoice number and the receipt numbers of their payments), `Payments`, `Refunds`, `InstallmentPlans`, `Subscriptions`, `CheckoutSessions` (with the contact, IP address, user agent and saved steps), `CouponRedemptions` and `ProviderCustomers`, plus `Warnings` for anything that could not be read.
- **`AnonymizeAsync`** removes names, emails, phone numbers, addresses (all but the billing country and region), IP addresses, user agents, saved checkout steps, internal plan notes and card descriptions, and replaces the person's emails and names wherever they appear in titles, history messages and failure or refund reasons. A removed name becomes `replacementText`, or is cleared when it is `null`. Amounts, dates, statuses, invoice and receipt numbers and the payment provider's references stay, because the business must keep them, so ledger totals do not change and an open plan or subscription can still be collected. It returns a `CommercePersonalDataAnonymizeResult` with how many records of each kind changed and any `Warnings`.

The Customers feature uses the service from its own privacy actions: `CommercePersonalDataCustomerPrivacyHandler` is an `ICustomerPrivacyHandler`, so erasing a customer anonymizes their commerce data and exporting a customer adds a `commerce` section to the download. See [Customers: Privacy](customers#privacy).

#### How a request finds the person

Each module contributes through **`ICommercePersonalDataContributor`**, registered in the startup of the feature that owns the records, so a feature that is not enabled is skipped:

| Member | Called |
| --- | --- |
| `IdentifyAsync(CommercePersonalDataSubject subject)` | In rounds, before anything is exported or changed. A contributor adds what its records reveal: guest owners, checkout sessions, names and payment-provider customers. The rounds stop when one adds nothing (at most five). |
| `ExportAsync(CommercePersonalDataExportContext context)` | Adds the module's records to `context.Export`. |
| `AnonymizeAsync(CommercePersonalDataAnonymizeContext context)` | Changes the module's records and counts them on `context.Result`. `context.ReplaceName(name)` and `context.Scrub(text)` apply the replacement text. |

Identification comes first because a guest checkout is owned by an id derived from its session: one person who checked out three times as a guest has three guest owners, and only their email ties them together. Anonymizing in the same pass would remove the very email the next module looks for. A user is never added as an owner because their checkout carries the person's email, and an email found on a record is not followed, so buying for someone else never sweeps up the other person's records.

| Feature | Contributor | What it adds |
| --- | --- | --- |
| Checkout | `CheckoutPersonalDataContributor` | Finds checkout sessions by owner and email (`ICheckoutSessionStore.GetByCustomerAsync`), adds a guest owner per guest session, exports the sessions and the payments and refunds taken in them, and anonymizes the sessions with `CheckoutSession.Anonymize`: owner, contact, IP address, user agent and saved steps, then the person's details anywhere else in the session's property bag. The modification time is left alone, so the abandoned-checkout sweep is unaffected. |
| Checkout | `CouponPersonalDataContributor` | A coupon keeps only a count, so a redemption is a coupon discount line of a completed checkout's invoice. Exports those and scrubs them. |
| Transactions | `TransactionPersonalDataContributor` | Transactions of every owner of the person: the guest contact, the auto-collection card description, and the person's details in the title and history. Adds the creating and online-settling checkouts. |
| Transactions (with Checkout) | `TransactionSettlementPersonalDataContributor` | Adds every checkout that paid toward the person's transactions, so a balance paid over several pay links is covered. |
| Subscriptions | `SubscriptionPersonalDataContributor` | Subscriptions of every owner: the guest contact, and the person's details in the title, cancellation reason and history. Adds the provider customer each one bills. |
| Subscriptions - Installment Plans | `InstallmentPlanPersonalDataContributor` | Plans by owner and by the email recorded on the plan: the customer's name and email, the note, the saved card's brand, last four digits and expiry, and the person's details in the title, history and failure messages. Adds the plan's checkouts and the provider customer of its saved card. |
| Stripe | `StripePersonalDataContributor` | The Stripe customers of the person's subscriptions and plans, and the customers the store's checkout created for their email (marked with `checkout_session_id` in their metadata; a customer another system created in the same account is left alone). Redaction calls the Stripe customer update API: the name becomes the replacement text and the email and phone are removed. The customer, its payment methods and its history stay at Stripe. Nothing is called when Stripe has no secret key, and a Stripe failure becomes a warning rather than stopping the request. |

To take part from your own module, implement `ICommercePersonalDataContributor`, register it with `services.AddScoped<ICommercePersonalDataContributor, MyContributor>()` in your feature's startup, and keep `IdentifyAsync` safe to call more than once. Records owned by a guest id that is not a checkout session (for example one your application derives from an email) are found when the caller passes that owner; the shipped modules cannot find them by email alone.

### Architectural boundary

Commerce is a composition shell, not a domain:

- It defines no persistence: no YesSql indexes, index providers, data migrations or stores.
- It owns none of the order, payment, tax, receipt or report data models. Those belong to their domain modules.
- Reusable domain commands live in their owning module; Commerce only composes them.

## Theming the storefront

Every storefront page renders through a named shape, so a theme changes any of them with a Razor (`.cshtml`) or Liquid (`.liquid`) template of that name in its `Views` folder, or from the admin with Orchard Core's **Templates** feature, without replacing a controller view. The controller views are thin frames: the breadcrumb, the page heading and, on account pages, the account menu, around the page's shape. A test fails the build when a storefront controller renders a page outside a shape.

| Page | Shapes |
| --- | --- |
| Listings and product pages | The listing and card shapes in [Products](products#theming); on a product's actions, `AddToCart`, `GroupedProducts`, `ExternalProductAction` and `ProductCompareToggle`; on its page, `ProductBundleContents`, `ProductUpsells`, `ProductRelated` and `ProductRecentlyViewed`; card strips, `ProductCardStrip` |
| Comparison | `ProductCompare` |
| Cart | `Cart`, `MiniCart`, `CartRecommendations` |
| Checkout | `CheckoutFlow` and each step's editor, such as `CheckoutCoupon` and `CheckoutOrderBump`; `CheckoutThankYou` and `OrderConfirmation` on the confirmation, with `OrderConfirmationOffers`; `CheckoutEnded` |
| Account | `AccountOverview`, `AccountDetails`, `AccountAddresses`, `AccountAddressEdit`, `AccountOrders`, `AccountOrder`, `AccountDownloads`, `AccountPaymentMethods`, `AccountSubscriptions`, `AccountTransactions` |
| Guests and links | `OrderLookup`, `OrderLookupSent`, `GuestOrder`, `GuestOrderInvalid`, `DownloadPage`, `TransactionPay`, `TransactionPayInvalid`, `SubscriptionUnavailable` |

The emails (order confirmations, abandoned checkout and shipment emails) are written by the modules and aren't templates yet.

## Related

- [Checkout](checkout), [Payments](payments) and [Pay Later](pay-later): taking money.
- [Transactions](transactions) and [Receipts](receipts): recording it, and telling the customer.
- [Subscriptions](subscriptions) and [Installment Plans](installment-plans): what is sold.
- [Products](products) and [Taxation](taxation): prices, currencies and taxes.
