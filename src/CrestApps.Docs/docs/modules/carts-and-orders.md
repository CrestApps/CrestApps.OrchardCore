---
sidebar_label: Carts and Orders
title: Carts and Orders
description: Shopping carts for products sold online, thin orders that check a cart out through the Checkout framework and record what was bought, orders the store creates for buyers to pay, printable invoices and packing slips, returns, and gift cards.
user_manual:
  - user-manual/commerce/carts-and-orders
  - user-manual/commerce/gift-cards
  - user-manual/commerce/headless-api
---

| | |
| --- | --- |
| **Features** | `CrestApps.OrchardCore.Carts`, `CrestApps.OrchardCore.Orders`, `CrestApps.OrchardCore.Orders.Returns` (Returns), `CrestApps.OrchardCore.Orders.GiftCards` (Gift Cards), `CrestApps.OrchardCore.Carts.Api` (Cart API), `CrestApps.OrchardCore.Orders.Api` (Orders API) |
| **Core** | `CrestApps.OrchardCore.Carts.Core`, `CrestApps.OrchardCore.Orders.Core` |
| **Category** | Commerce |

**Carts** lets visitors put products sold online in a cart from a listing card or a product page. **Orders** checks the cart out through the [Checkout](checkout) framework and records what was bought. Together they sell goods the way [Subscriptions](subscriptions) sells plans: through the same checkout, payment providers, coupons and taxes.

## Carts

### What can go in a cart

A product can be added when it can be bought online (see [sales mode](products#product-types)) at a one-time price. The cart refuses:

- a product that is only listed, or has no price;
- a subscription plan, which is signed up for on its own, and any recurring price;
- a price that asks the buyer for an amount;
- a product priced in another currency than the cart's that can't be converted into it (with [Markets](markets.md#carts) on, a shopper's cart is in their market's currency, and prices are converted into it);
- a product not sold to the shopper, such as one left out of their market (`IProductSaleRestriction`, see [Markets](markets.md#not-sold-in-a-market));
- a quantity the price does not allow, such as a second unit of a price bought only once.

A cart never holds a price the browser sent. It stores what was chosen (the product, its price and the quantity) and prices every line again through `IProductSnapshotResolver` and `IPriceResolver` whenever it is shown. A line whose product was unpublished, taken off sale or repriced is pointed out, and the cart cannot be checked out until it is changed or removed.

### Who a cart belongs to

| Visitor | Cart found by |
| --- | --- |
| Signed in | Their user id. |
| Guest | A `cart_owner` cookie holding a protected token. Only the token's hash is stored on the cart. The cookie is issued when the guest first adds something, so browsing sets no cookie. |

Signing in merges the guest cart into the user's cart: lines of the same product and price add up, and lines priced in another currency than the user's cart are dropped. A cart expires after 30 days without changes for a guest and 90 days for a user. An hourly background task deletes expired carts.

### Pages and shapes

| Shape | Shows | Where |
| --- | --- | --- |
| `AddToCart` | The quantity, the price choice when a product has several one-time prices, and **Add to cart** | The **Actions** zone of the product's `ProductAction` display, so on listing cards and product pages |
| `Cart` | The cart page at `/cart`, with each line, its price, a quantity box, a remove button and the subtotal | `CartController` |
| `CartLink` | A cart link with the item count | The `Navbar` shape, beside the user menu, in themes that render it (TheTheme does; TheAgencyTheme and TheBlogTheme don't) |

Each shape can be overridden in Razor or Liquid. A theme hides the cart link with an empty `CartLink` template.

`CartLink` also renders the **mini cart**, a Bootstrap offcanvas loaded from `GET cart/mini` (the `MiniCart` shape, route name `CartMini`). With Bootstrap's offcanvas and `fetch` available, it posts the `form.product-add-to-cart` forms on the page and its own remove forms with `Accept: application/json`, updates the count and opens the panel; without them the link and the forms work as plain links and posts.

The forms are ordinary POST forms with antiforgery tokens, so they work without JavaScript and redirect back with a notification. When adding redirects back to the page the buyer was on, the notification links to the cart, so a theme without a cart link still leads there. A request whose `Accept` header asks for `application/json` gets `{ succeeded, message, itemCount }` instead. The endpoints are `POST cart/add`, `POST cart/update` and `POST cart/remove`, in the `Cart` rate limit group of the Orchard Core Rate Limits module.

### Options

`CartOptions` sets the guest and user cart lifetimes, the most lines a cart holds (50), the most units of a line (99), and `ExcludedParts`, the content parts whose products are never sold through a cart (`SubscriptionPart` by default).

```csharp
services.Configure<CartOptions>(options =>
{
    options.GuestCartLifetime = TimeSpan.FromDays(14);
    options.MaxQuantity = 20;
});
```

### Carts Report Builder data source

When **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) is enabled, as it is with the [Report Builder](report-builder/index.md), the **Carts** feature registers the **Carts** data source (`Carts`), so reports and data pipelines can read the shoppers' open carts and, with the [Wishlists](#wishlists) feature on, their wishlists. A cart holds what is not bought yet: what an order buys leaves its cart, and empty and expired carts are deleted. Every data set needs **Manage orders** (`ManageOrders`), the permission of the order screens, as Carts has no screens of its own; a person reads the carts only when they may read the orders the carts become.

| Data set | One row per | Feature |
| --- | --- | --- |
| **Carts** (`Carts`) | Open cart | Carts |
| **Cart lines** (`CartLines`) | Product line of an open cart | Carts |
| **Wishlists** (`Wishlists`) | Signed-in shopper's wishlist | Wishlists |
| **Wishlist items** (`WishlistItems`) | Product saved to a wishlist | Wishlists |

The carts and wishlists indexes hold no dates, so these data sets have no main date: they read their newest records first by their index rows, and a date range a report puts on them is applied to the rows read. The keys of a join narrow the read in the database. The token that finds a guest's cart, and what a shopper typed for add-ons and line details, are never exposed.

| Data set | Field | Display name | Notes |
| --- | --- | --- | --- |
| Carts | `ItemId` | Cart ID | An order's cart ID joins it. |
| Carts | `UserId`, `IsGuest` | User ID, Guest | `UserId` joins **Users**. Empty for a guest. |
| Carts | `LineCount`, `ItemCount` | Lines, Units | |
| Carts | `Currency`, `Value` | Currency, Value when added | The units at the prices they had when they were added, in the cart's currency. It is never charged. |
| Carts | `CreatedUtc`, `ModifiedUtc`, `ExpiresUtc` | Started, Last changed, Expires | |
| Cart lines | `CartId` | Cart ID | Joins **Carts**. |
| Cart lines | `UserId`, `Currency`, `CartModifiedUtc` | User ID, Currency, Cart last changed | From the cart. `UserId` joins **Users**. |
| Cart lines | `LineId`, `Position` | Line ID, Line number | |
| Cart lines | `ProductId`, `VariantId`, `PriceId` | Product ID, Variant ID, Price ID | `ProductId` joins **Products** and `VariantId` joins **Product variants**. |
| Cart lines | `Quantity`, `AddedUnitPrice`, `Value` | Quantity, Unit price when added, Value when added | In the cart's currency. |
| Cart lines | `AddedUtc` | Added | |
| Wishlists | `ItemId`, `UserId` | Wishlist ID, User ID | `UserId` joins **Users**. |
| Wishlists | `ItemCount` | Products saved | |
| Wishlists | `ModifiedUtc` | Last changed | |
| Wishlist items | `WishlistId` | Wishlist ID | Joins **Wishlists**. |
| Wishlist items | `UserId`, `Position`, `ProductId` | User ID, Position, Product ID | `ProductId` joins **Products**. Position 1 is the newest. |
| Wishlist items | `AddedUtc` | Saved | |

## Orders

This video shows the order screens in the admin:

<YouTube video="orders" title="Video overview of orders" />

### Checking out

The cart page shows **Checkout** when the Orders feature is enabled. Checking out:

1. prices the cart again and refuses it if a line cannot be bought;
2. freezes it into a **draft order**: each line's product version, title, SKU, price, quantity, unit price and tax details, and the subtotal;
3. cancels earlier unpaid orders made from the same cart, so the customer pays for what the cart holds now;
4. starts a checkout session whose reference is the order (`CheckoutReferenceTypes.Order`) and sends the customer to it.

`OrderCheckoutHandler` bills one line per order line, with its quantity, SKU, tax category, tax classification and product type, so each line is taxed as what it is, and with its product and the categories it was in when ordered (`OrderLine.CategoryIds`, every taxonomy term on the product), so a coupon limited to some products or categories finds it. A canceled order gives back the coupon uses of its checkout (`ICouponRedemptionService.ReturnAsync`). It refuses to complete a checkout that bills nothing. When the checkout completes, the order is marked **Paid** with the total and tax that were charged and the discounts it was given (`Order.Discounts`, each an `OrderDiscount` with its code, description, amount, provider and `Target`, the `DiscountTarget` it came off: the goods bought now, the first cycle or shipping; and `Order.DiscountTotal`), so its lines, discounts, shipping and tax add up to what was paid. What balances such as a gift card, store credit or points paid toward the total (`CheckoutInvoice.Tenders`, see [Balances that pay after tax](checkout#balances-that-pay-after-tax)) is copied to `Order.Tenders`, one `OrderTender` each (`ProviderKey`, `Reference`, `Description`, `Amount`); `Order.Total` stays the full total, and the rest was charged by the payment provider. The order's admin and customer pages, its invoice and its confirmation email show each as *Paid with* and its description. What was bought is taken out of the cart, matching each line's product, price and variant; anything added to the cart after checking out stays in it. An order replaced by a newer one is still marked paid if its own checkout completes, because that payment bought it.

The checkout's confirmation page shows the order number.

**Guest access.** `IOrderLinkService` makes a signed, expiring link to one order (data protection with a time limit of 60 days, purpose `CrestApps.OrchardCore.Orders.OrderLink`; the site's base URL first, then the current request). `GET orders/view/{token}` (`OrderViewController`) opens it read-only, with a pay link when a Pay Later transaction for the order is still outstanding; a bad or expired token shows nothing about any order. The confirmation page and a guest's confirmation email carry the link.

**Find my order.** `GET/POST orders/find` takes an email and emails a link to each paid or awaiting-payment order placed with it in the last year (up to 10), finding them through the indexed `OrderIndex.CustomerEmail` (`OrderQuery.CustomerEmail`). The answer is the same whether or not orders exist. Posts are limited per IP and per email through `IPaymentAttemptLimiter`, and carry the `find-order` rate-limit group for the Rate Limits feature.

**Order confirmations.** Other modules add sections to the confirmation email through `IOrderEmailSectionProvider`, such as the download links of [Digital Downloads](downloads). Once a checkout completes and is committed, `OrderConfirmationCheckoutHandler` emails the paid order's confirmation to `Order.CustomerEmail` through `IEmailService`, built by `OrderConfirmationEmailBuilder` (order number, lines, discounts, shipping, tax, total, addresses; every value HTML-encoded). It sends once (`Order.ConfirmationSentUtc`), only when `OrderSettings.SendConfirmations` is on (the default, under **Settings > Commerce > Orders**, with **Manage orders**) and an email service is registered. A failed send is logged and leaves the order unconfirmed; it never undoes the paid order.

Who the order is for comes from the checkout's `CheckoutContactInfo`: its name, email and phone replace the signed-in account's when the buyer entered them, and its `BillingAddress` is copied onto `Order.BillingAddress` (an immutable `Address`) when the order is paid. With the [Customers](customers) feature on, the checkout's **Contact** step fills it in, for guests and signed-in buyers alike, and the order is linked to its customer (`Order.CustomerId`).

### Taxes and culture on the order

- **`Order.TaxLines`.** When the checkout completes, `OrderCheckoutHandler` copies the invoice's `TaxLine`s onto the order, one per item, tax and rate, with the jurisdiction, rate, taxable amount, amount and `IncludedInPrice`. Reports such as the [VAT one-stop shop](taxation.md#taxes-on-orders-and-the-one-stop-shop) add them up. Orders paid before this have none.
- **`Order.Culture`.** `IOrderService.CreateFromCartAsync` keeps the request's UI culture (`OrderCulture.Current()`, such as `de-DE`) when the cart becomes an order, which for a cart is when the buyer starts checking out. The order confirmation (`OrderConfirmationSender`), the shipped email (`ShippedEmailShipmentHandler`), the return emails (`OrderReturnEmailSender`), the gift card email (`GiftCardEmailSender`, from the card's order) and the checkout recovery email (`CheckoutRecoveryService`) are built inside `OrderCulture.Use(order.Culture)`, a `CultureScope` that sets the culture and UI culture, so their localized strings, dates and amounts are in it. An order without a culture, or with one the runtime doesn't know, is written in the current culture. Every order is made there, so an order staff create, or a register sale, keeps the culture of the staff member's request.

With [Markets](markets.md), picking a market with a language switches the storefront's culture, so the order is in the market's language.

### Accounts, recovery and the storefront account

- **Account from the confirmation.** `OrderConfirmationDisplayDriver` gives a guest's confirmation an `OrderAccountClaims` token (data protection, two hours, one order) when registration is open and the order has an email. `POST orders/create-account` (`OrderCreateAccount`) makes the user from the order's email, honouring `RegistrationOptions.UsersMustValidateEmail` and `UsersAreModerated`, sets `Order.UserId`, signs in and redirects to the account's order page. It is rate limited and refuses an email that already has an account.
- **Recovery emails.** `CheckoutRecoveryBackgroundTask` (every 15 minutes) runs `CheckoutRecoveryService.SendDueAsync` when `OrderSettings.SendRecoveryEmails` is on. An order is due when it awaits payment untouched, or was canceled because the buyer left (`CheckoutCanceled`, `CheckoutExpired`, `Abandoned` or `PaymentFailed`), longer ago than `RecoveryDelayHours` and within a week, has lines, has no `RecoveryEmailSentUtc`, and no paid order with the same email came after it. The email comes from the order, or from the checkout's `CheckoutContactInfo` for an order not yet paid. `CheckoutRecoveryLinks` signs the link (`orders/resume/{token}`, route `OrderResume`, fourteen days) from the site's base URL; the link resumes a pending checkout or rebuilds the cart.
- **Account pages.** `account/orders` (`AccountOrders`) and `account/orders/{orderId}` (`AccountOrder`) list and show the signed-in user's orders through the same order shapes as the guest view (`OrderConstants.DisplayTypes.Detail`). An order of another user is not found.
- **Track your order.** `GET/POST orders/track` (`OrdersTrack`, `OrderViewController.Track`) takes an order number and the email it was placed with, and redirects to the order's signed link (`IOrderLinkService.CreateUrlAsync`) when both match a paid order. Posts carry the `find-order` rate-limit group and are limited per IP through `IPaymentAttemptLimiter` (`track-order`); anything else shows the same *couldn't find* answer. The Find my order page links to it.

### Invoices and packing slips

`OrderDocumentBuilder` builds an `OrderDocumentViewModel` for the `OrderInvoice` and `OrderPackingSlip` shapes, both overridable in Razor or Liquid. The header (name, logo, address, email, phone, website, footer) is read from the `ReceiptSettings` site settings as JSON, so the Receipts module needn't be on; without them it's the site name. Addresses are formatted with `IAddressFormatter` when the Addresses feature provides one.

- **Invoice** (`BuildInvoiceAsync`): billed to, ships to (or *Pick up at*), each line with SKU, quantity, unit price and amount, the discounts, shipping, tax, total, refunded and amount due, and a status (*Paid*, *Payment due*, *Refunded*, *Canceled*).
- **Packing slip** (`BuildPackingSlipAsync(order, shipped, shipmentNumber)`): the order's goods (downloads and services left out), or the lines named by `shipped` (line id to quantity) with the shipment's number; no prices.

| Route | Name | Who |
| --- | --- | --- |
| `Admin/orders/{orderId}/invoice` | `OrdersInvoice` | `ManageOrders` |
| `Admin/orders/{orderId}/packing-slip?lines=lineId:qty,…&shipment=N` | `OrdersPackingSlip` | `ManageOrders`; the Shipping module links each shipment's slip |
| `account/orders/{orderId}/invoice` | `AccountOrderInvoice` | The order's own user, with `ViewOwnOrders` |
| `orders/view/{token}/invoice` | `GuestOrderInvoice` | Anyone with the order's signed link |

Drafts aren't printed; canceled orders print for the store only. The page around the document prints the `.order-document-print-area` alone.

### Orders the store creates

`DraftOrderService` puts an order together for a buyer, such as from a phone call. It prices the chosen products as a cart for the buyer's account when their email has one (so their own prices apply), creates the order through `IOrderService.CreateFromCartAsync`, and keeps it as a `Draft` with an `Order.Invoice` (`OrderInvoice`: `CreatedBy`, `Note`, `DueUtc`, `SentUtc`, and `AddsToOrderId`/`AddsToOrderNumber` for items added to a paid order). Shipping and tax come at checkout.

- **Admin** (`DraftOrdersAdminController`, `ManageOrders`): `Admin/orders/drafts/create` (`OrdersCreate`; `?addsToOrderId=` from a paid order's **Add items**), `Admin/orders/{orderId}/edit` (`OrdersEdit`, while it's a draft) and `Admin/orders/{orderId}/send-invoice` (`OrdersSendInvoice`).
- **Editing.** `IOrderService.ReplaceLinesAsync(order, pricing)` replaces a draft's lines with a priced cart's and calls each `IOrderEventHandler.LinesChangedAsync`; a handler that refuses (Inventory, short of stock) leaves the old lines and returns the problems. An order awaiting payment can't be changed.
- **Invoice email.** `SendInvoiceAsync` emails *Your order … is ready to pay* with the lines, subtotal, note, due date and a **Review and pay** link to `orders/view/{token}/pay`, sets `SentUtc` and adds a note. It needs an `IEmailService` and the order's signed link.
- **Paying.** `OrderPayController` serves `orders/view/{token}/pay` (`GuestOrderPay`) and `account/orders/{orderId}/pay` (`AccountOrderPay`): it opens the order's pending checkout, or starts one (`CheckoutReferenceTypes.Order`). `OrderCheckoutHandler` fills in the checkout's `CheckoutContactInfo` from the order, and puts an invoiced order whose checkout failed, was canceled or expired back to `Draft`, so its link pays it again. `AbandonedOrderSweeper` leaves invoiced orders alone. The guest and account order pages show a sent draft with its note, due date and **Pay now**.

### Quotes

A signed-in buyer asks for a quote from their cart when `OrderSettings.AllowQuoteRequests` is on (**Settings > Commerce > Orders**, **Take quote requests**).

- **The cart.** Carts.Core's `ICartActionProvider` (`GetActionsAsync(pricing)` returns `CartAction(Label, Url, Description)`) adds buttons under **Checkout**; `QuoteCartActionProvider` adds **Request a quote**, linking to `cart/quote` (`QuoteRequest`).
- **The request.** `QuoteService.RequestAsync(user, message, purchaseOrderNumber)` creates a `Draft` order from the priced cart (with no `CartId`, so a later checkout of the cart doesn't replace it), an `OrderInvoice` created by the buyer, the purchase order number, the buyer's customer, and `Order.Quote` (`OrderQuote`: `RequestedUtc`, `Message` up to 2,000 characters, `Status`, `SentUtc`, `ExpiresUtc`, `RespondedUtc`), then deletes the cart. The page is the `QuoteRequest` shape.
- **Pricing it.** The draft editor's **Price each** sets `OrderLine.QuotedUnitPrice`, which replaces the line's unit price and total (`DraftOrderService.ApplyPrices`). The field is read as invariant text, so a decimal-comma admin language doesn't misread it. A saved draft is re-priced in its own currency. `OrderCheckoutHandler` marks a quoted line `BillingItem.ExcludeFromDiscounts`, which `CheckoutDiscountBase.IsDiscountable` honors, so promotions don't come off an agreed price.
- **Sending.** `SendInvoiceAsync` on a quote emails *Your quote … is ready* with **Review and accept**, sets `Quote.Status` to `Sent`, `SentUtc`, and `ExpiresUtc` to the invoice's `DueUtc`.
- **Accepting.** `QuoteService.CanPay(order, now)` is true for an order without a quote, or one sent and not expired (`OrderQuote.CanAccept`), and false while an `Order.Approval` is pending or rejected. `OrderPayController`, the pay links on the order pages and the API follow it. Paying a sent quote sets it `Accepted`.
- **Declining.** `QuoteService.DeclineAsync` (`account/orders/{orderId}/decline-quote`, `AccountQuoteDecline`) cancels a requested or sent quote not being paid, with `OrderCancelReason.Declined`.
- **The account.** The account's orders list shows quotes and orders sent to pay, not paid or turned down, above the placed orders, and the order page titles a quote *Quote*.
- **API.** `POST me/quotes` (`{ message, purchaseOrderNumber }`, 201 with the order, 400 for an empty cart) and `POST me/orders/{orderId}/decline-quote` (409 when it can't be). `ApiOrder.Quote` is an `ApiOrderQuote`.

### Commerce emails and their templates

Every email the commerce modules send a customer goes through `ICommerceEmailSender` (Orders.Core, registered with `services.AddCommerceEmailSender()`): the order confirmation, the order to pay and the quote, the shipped email, the return emails, the gift card, the recovery email, Find my order, the booking emails, donation receipts, listing emails and the point of sale receipt. A sender passes a `CommerceEmail` (`Kind`, `To`, `Order`, `Culture`, `Reference`) and a callback that builds the `CommerceEmailContent` (`Subject`, `Text`, `Html` and the template's `Values`). `ICommerceEmailSender` then does the following:

- It sends nothing, and returns `false`, when the Email feature is off, the email has no recipient, or the callback returns `null`.
- It builds the email in `Culture`, such as the language the buyer shopped in (`Order.Culture`).
- It passes an email with a `Kind` through `IOrderEmailTemplates`, the store's template of that kind and its layout. An email without a kind goes as built.
- It sends the email, and logs a send that fails or throws and returns `false`. A failed send never undoes what the email is about.

The kinds the store can write templates of are registered by the feature that sends them, with `services.AddCommerceEmailKind(name, title, values)`. The title and the description of the email's own values are `LocalizationSource`s, and **Settings > Commerce > Emails** lists the kinds in the order they were registered. Orders registers `order-confirmation`, `order-invoice`, `order-quote` and `checkout-recovery` (`OrderEmailKinds`), Returns `order-return`, Gift Cards `gift-card`, Shipping `order-shipped` and Bookings `booking`. Saving the page keeps the templates of kinds that no enabled feature registers.

```csharp
services.AddCommerceEmailSender()
    .AddCommerceEmailKind("giftwrap-sent", LocalizationSource.Create<Startup>("Gift wrapped"), LocalizationSource.Create<Startup>("{{ Message }}: the gift message."));
```

`LiquidOrderEmailTemplates` reads `OrderEmailTemplateSettings` (**Settings > Commerce > Emails**, group `order-emails`):

- A `Layout` around every email, with `{{ Content }}`.
- `Templates` by kind, each with `IsEnabled`, a `Subject` and a `Body` in Liquid.

The Liquid has `Content` (the built HTML, unencoded), `Subject`, `StoreName`, `Order` (the order as plain values, statuses by name) and the email's own values (`OrderUrl`, `PayUrl`, `TrackingUrl`, `Carrier`, `ShipmentNumber`, `ReturnNumber`, `ReturnStatus`, `Code`, `Amount`, `RecipientName`, `Message`, `RecoveryUrl`). A template that's used rewrites the text part from its HTML. Liquid that doesn't parse is refused when saved. A template that throws when rendered sends the built email and logs why.

### Sales channels

Every order records where it was made, in `Order.Channel` (`OrderChannels`):

| Channel | Set by |
| --- | --- |
| `online` | A buyer checking out a cart, on the storefront, a headless storefront or an app. Orders made before channels were recorded read as online (`OrderChannels.Of`). |
| `pos` | A sale rung up at a register by [Point of Sale](point-of-sale). It also has `LocationId` (the register's inventory location), `RegisterId`, `RegisterName`, `ShiftId`, `StaffUserId` and `StaffName`. `OrderChannels.IsCounterSale` is true for it. |
| `draft` | `DraftOrderService`: an order staff make and send to the buyer to pay. |

`OrderIndex` has `Channel`, `LocationId` and `ShiftId`, and `OrderQuery` filters by each. **Commerce > Orders** has a channel filter (*Every channel*, *Online*, *Point of sale*, *Drafts sent by staff*) in its list toolbar, which the CSV export follows, and a register sale shows its register as a badge. The staff orders API takes `channel` and `locationId`, and `ApiOrder` and `ApiOrderSummary` carry `channel` and `locationId`, with `register` on `ApiOrder`.

`LocationId` is set only for register sales. A pickup order's location is its shipping's (`Order.Shipping.LocationId`).

### Returns

The **Returns** feature (`CrestApps.OrchardCore.Orders.Returns`) adds buyer-requested returns. An `OrderReturn` (in the `Orders` collection, `OrderReturnIndex` on number, order, user, status and date) has a `Number` (such as `R-261019-K3QZ`), the order, the buyer, `Lines` (`OrderReturnLine`: order `LineId`, title, SKU, quantity, the unit price paid, `Reason`, `Restocked`), a `Resolution` (`Refund` or `Exchange`, with `ExchangeFor`), the buyer's `Comment`, the store's `StoreMessage`, `RefundedAmount`, `RestockLocationId`, and a `Status`: `Requested`, `Approved`, `Received`, `Completed`, `Rejected` or `Canceled` (withdrawn by the buyer).

- **Policy.** `OrderReturnPolicy.GetReturnable(order, returns)` gives the units of each good (`ProductType.Good` or undefined) not covered by an active return; `IsOpen(order, settings, utcNow)` is true for a paid order within `ReturnSettings.WindowDays` of `PaidUtc`. `UnitPriceOf(line)` is the line total over its quantity, add-ons and line discounts included.
- **Buyer.** `account/orders/{orderId}/return` (`AccountOrderReturn`) and `orders/view/{token}/return` (`GuestOrderReturn`) show the `OrderReturnRequest` shape and post the request; `…/returns/{returnId}/withdraw` withdraws one still `Requested`. `OrderReturnsDisplayDriver` adds `OrderReturns_Detail` (the buyer's returns, with **Return items** and the return instructions once approved) to a paid order's `Detail` display, and `OrderReturns_DetailAdmin` to the store's.
- **Store.** `ReturnsAdminController` (`Admin/returns`, `ManageOrders`) lists returns through an `AdminList` with a status filter and search by return or order number, and acts through `OrderReturnService`: `approve`, `reject` (with a message), `receive` (the lines to restock and an optional location), `refund` (an amount, through `OrderOperationsService.RefundAsync` without restocking, then `Completed`) and `exchanged`. Each step adds an order note, and `OrderReturnEmailSender` emails the buyer (requested, approved, declined, refunded, exchanged); a failed send is logged and never undoes the step.
- **Restocking.** Receiving calls each `IOrderEventHandler.ReturnedAsync` with an `OrderReturnedContext` (the order, the return, `Lines` of `OrderReturnedLine(Line, Quantity, Restock)`, `LocationId`, actor) in the same unit of work. Inventory puts the restocked lines back with `IInventoryService.RestockAsync(orderId, lines, locationId, …)`.
- **Settings.** `ReturnSettings` (**Settings > Commerce > Returns**, group `returns`, `ManageOrders`): `WindowDays` (30, 1 to 365), `Reasons` (up to 20), `AllowExchanges` (true) and `Instructions`.

Exchanges are closed by the store once it has sent the replacement; no replacement order is created. Returns start with the buyer; the store's own refunds and restocking stay on the order's page.

### Gift cards

The **Gift Cards** feature (`CrestApps.OrchardCore.Orders.GiftCards`, depends on Orders and `OrchardCore.Title`) sells gift cards as products, issues them by hand, and lets them pay toward checkouts through the checkout's [tender seam](checkout#balances-that-pay-after-tax), after tax.

**The card.** A `GiftCard` (`Orders.Core`, a `CatalogItem` in the `Orders` collection) has a `Code`, `InitialValue`, `Balance`, `Currency`, `IsEnabled`, `ExpiresUtc`, `RecipientName`, `RecipientEmail`, a staff-only `Note`, the `OrderId` and `OrderNumber` that bought it, and `Activity`: one `GiftCardActivity` per change (`CreatedUtc`, `Amount`, `Balance` after, `Reason`, `SessionId`, `Actor`). `ChangeBalance(amount, reason, utcNow, sessionId, actor)` is the only way the balance moves; it never goes below zero and always writes the activity. `CanPay(utcNow, currency)` is true for an enabled card with money on it, not expired, in that currency. `LastCharacters` is the last four characters of the code, which is all a buyer or the list sees.

**Codes.** `GiftCardCodes.Generate()` makes 16 random characters (`RandomNumberGenerator`) from an alphabet without 0, O, 1 or I, in groups of four (`Format`). `Normalize` keeps letters and digits in capitals, which is how codes are stored in the index and looked up, so dashes, spaces and case don't matter. A code typed by staff is 8 to 32 letters and digits.

**Storage.** `IGiftCardStore` (`GiftCardStore`, with concurrency checks) adds `GetByCodeAsync`, `ListByOrderAsync` and `PageAsync(GiftCardQuery, skip, take)`. `GiftCardIndex` (in the `Orders` collection, `GiftCardMigrations`) holds the normalized code, the last characters, the recipient's email in lower case, the balance, currency, enabled flag, expiry, order and creation time, with indexes on the code, the item id, the last characters and the order. `GiftCardQuery.Search` matches a full 16-character code exactly, else the last characters or part of the email; `Status` is a `GiftCardStatusFilter` (`All`, `Active`, `UsedUp`, `Expired`, `Disabled`).

**Issuing and adjusting.** `GiftCardService.IssueAsync(value, currency, reason, configure, code, actor)` makes a card, with a new code unless one is given (and returns `null` when a given code is taken). `AdjustAsync(itemId, amount, reason, actor)` changes the balance under `CheckoutTenderLocks.GetKey(OrderConstants.GiftCardTenderKey, itemId)`, the lock the checkout's ledger takes, and returns `null` when the lock is busy.

**Paying with a card.** `GiftCardTenderProvider` is the `ICheckoutTenderProvider` with the key `gift-card` (`OrderConstants.GiftCardTenderKey`). Its lines are the chosen cards that `CanPay` in the invoice's currency, described as *Gift card ending 4F2K* (`Describe`). `DebitAsync` writes *Spent at checkout* with the checkout's session id, even for a card disabled since it was held; `CreditAsync` writes the refund's reason, or *Refunded*. `GiftCardCheckoutFlowDisplayDriver` renders the `CheckoutGiftCard` shape at `Aside:6` of the payment step, except on a checkout that settles an existing balance or saves a payment method, and when nothing is left to pay and no card is applied. On `apply-tender` it looks the code up, rate limited per address through `IPaymentAttemptLimiter` (scope `gift-card-code`), checks it (*That gift card code is not recognized.*, *That gift card can't be used.*, *That gift card has expired.*, *That gift card has nothing left on it.*, *That gift card is in EUR, and this order is in USD.*), adds the card's item id to the `CheckoutTenderSelection` and rebuilds the invoice; a card the rebuilt invoice doesn't use gets *That gift card is being used at another checkout right now, or there's nothing left to pay.* `remove-tender:gift-card:` and the item id takes it off.

**Selling cards.** The migration attaches `GiftCardPart` (an empty marker part, **Gift card**) and creates a `GiftCard` content type (`OrderConstants.GiftCardContentType`): `TitlePart`, `ProductPart` with `ProductType.Digital` and the checkout currency as its default currency, and `GiftCardPart`. Any product type with the part sells cards. `GiftCardOrderEventHandler.PaidAsync` issues one card per unit of each line whose content type has the part and a price above zero, worth the line's `UnitPrice` in the order's currency, to the order's `CustomerName` and `CustomerEmail`, and emails each one through `GiftCardEmailSender`. It never issues twice for an order (`ListByOrderAsync`). For an order `OrderOperationsService.IsOwedAsync` says is still owed, such as a bank transfer, the cards are issued disabled with a note to turn them on once paid, and not emailed. Each card adds an event note to the order.

**Cancels and refunds.** `CanceledAsync`, and `RefundedAsync` once `RefundStatus` is `Refunded`, turn off each card the order bought that is enabled and still holds its `InitialValue`, and note why on the card. A card someone has spent from is left for staff.

**Email.** `GiftCardEmailSender.SendAsync(card, message)` sends the card to `RecipientEmail` through `IEmailService`, when one is registered: the value (the current balance), the code, the optional personal message, how to use it, the expiry and a link to the store, all HTML-encoded. It returns whether the email went.

**Admin.** `GiftCardsAdminController` (`Admin/gift-cards`, permission `ManageGiftCards`, menu **Commerce > Gift cards**) lists cards through an `AdminList` (`GiftCardDisplayDriver` renders the row in both `SummaryAdmin` and `SummaryAdminGrid`) with a search in the action bar and the status filter in the toolbar (the shared `CommerceAdminListFilters` shape). `create` issues a card and can email it; `edit/{itemId}` shows the whole code, the balance, the order that bought it and the activity, and saves the recipient, expiry, note and `IsEnabled`; `adjust/{itemId}` posts an `amount` and a required `reason` to `AdjustAsync`; `email/{itemId}` sends the card again.

`GiftCardPermissionsProvider` declares **Manage gift cards** (`OrderPermissions.ManageGiftCards`), given to Administrator by default.

### Order statuses

| Status | Meaning |
| --- | --- |
| Draft | Made from a cart, not yet sent to the checkout, or put together by the store for a buyer to pay (`Order.Invoice` set). |
| Awaiting payment | Its checkout started and has not completed. |
| Paid | Its checkout completed. |
| Canceled | Ended, with `CanceledUtc` and a `CancelReason`: `Replaced` (a newer order from the same cart), `PaymentFailed`, `CheckoutCanceled`, `CheckoutExpired`, `Abandoned` (no checkout was left that could pay it), `Unavailable` (an order event handler refused it, such as for want of stock) or `CanceledByStore` (from the order's page, with the store's `CancelNote`). |

Money given back is tracked beside the status, so everything that checks for a paid order keeps working: `Order.Refunds` (one `OrderRefund` per refund that went through), `RefundedAmount`, and `RefundStatus`, which is `PartiallyRefunded` or `Refunded` once refunds reach the total. The order list and page show it as a badge.

**Order events.** `IOrderEventHandler` (or `OrderEventHandlerBase`) hears about each order:

- `CreatedAsync` runs right after `IOrderService.CreateFromCartAsync` makes the order; a handler that can't provide what it needs calls `context.Refuse(message)`. The order is then canceled as `Unavailable`, and `OrderCreationResult.Problems` carries the messages, which the cart shows.
- `PaidAsync` runs in `OrderCheckoutHandler.CompletingAsync`, in the unit of work that marks the order paid, so a handler that throws rolls the completion back for the checkout to retry.
- `CanceledAsync` runs when an order is canceled. `context.Restock` says whether what it sold goes back into stock.
- `RefundedAsync` (a default interface method) runs after a refund is recorded on the order; `context.Restock` is true only when the store asked for it and everything is now refunded.
- `LinesChangedAsync` (a default interface method) runs when a draft's lines are replaced (`IOrderService.ReplaceLinesAsync`), with `OrderLinesChangedContext.PreviousLines`; `context.Refuse(problem)` keeps the old lines.
- `ReturnedAsync` (a default interface method) runs when a return's items arrive, with `OrderReturnedContext`; see [Returns](#returns).

`OrderEventHandlerBase` has virtual no-op versions of each. Inventory uses them to hold, sell, release and restock stock.

**Where an order is fulfilled from.** `OrderShipping.LocationId` names the location an order is fulfilled from, such as the shop it's picked up at (`OrderShipping.IsPickup`, provider `Pickup`), copied from `CheckoutShippingInfo.LocationId`; `null` lets inventory choose. `IFulfillmentLocationProvider` (Orders.Core) lists the places orders can be fulfilled from as `FulfillmentLocation(LocationId, Name)`; Inventory implements it with its stock locations, which Shipping offers as a pickup location's **Stock comes from**.

**Pre-orders.** `OrderLine.ShipsFromUtc` keeps the release date of a pre-ordered line as it was when the order was placed (from `CartPricing` lines), shown on the order pages. The cart's checkout commits the new order before the checkout starts, and shows *Someone else is buying the same items right now* when that commit hits a concurrency conflict.

**Canceling.** Every cancellation goes through `IOrderService.CancelAsync(order, reason)`, which only cancels a draft or awaiting-payment order (a paid one only with `CanceledByStore`), records the reason and time, and calls every `IOrderEventHandler.CanceledAsync` in the same unit of work, so a module can let go of what the order held. `OrderCheckoutHandler` cancels the order when its checkout fails, is canceled or expires (the checkout's `Failed`, `Canceled` and `Expired` handler stages), unless the order has since moved to another checkout. `AbandonedOrdersBackgroundTask` runs every 15 minutes: `AbandonedOrderSweeper` looks at draft and awaiting-payment orders older than an hour and cancels those whose checkout ended, is gone or never started, up to 500 per run. A completed checkout whose order isn't paid yet is left to the checkout reconciliation sweep.

An order holds lines, not money. The payment, its tax, the payment attempts and any refund belong to the checkout session the order names; the order keeps a record of each refund for its page.

### Managing an order

A user with `ManageOrders` manages an order from its page, through `OrderOperationsService`:

- **Refund** gives back an amount, or everything still refundable. Money paid online at checkout is refunded through `ICheckoutRefundService`, newest payment first; money paid later is refunded through `ITransactionCommandService.RefundAsync` on the order's Pay Later transaction. What a gift card, store credit or points paid is refunded last (`RefundTendersAsync`), through `ICheckoutTenderLedger.ReturnAsync`, the balance used last first, and recorded as an `OrderRefund` whose `RefundId` starts with `tender:`, with a note naming the balances. `GetMoneyAsync` counts what the checkout's redeemed holds can still return (`Amount - ReturnedAmount`) as refundable. A refund waiting to be settled by hand blocks another one.
- **Refund as store credit.** `RefundAsync(order, amount, reason, restock, actorName, asStoreCredit, cancellationToken)` with `asStoreCredit` gives the whole amount to the buyer through `IOrderStoreCreditRefunder` (in `Orders.Core`, implemented by the [Store Credit](customers#store-credit) feature) instead of to how they paid: nothing is refunded at a gateway, the amount is recorded as an `OrderRefund` whose `RefundId` starts with `store-credit:` (so `RefundedAmount` and `RefundStatus` follow, and `OrderRefundedContext` reaches the order event handlers), and a note says *Gave … back as store credit.* `GetMoneyAsync` takes those refunds off what is refundable, and sets `OrderMoney.CanRefundAsStoreCredit` when a refunder is registered and `CanRefundAsync` says the buyer has an account; the order page's refund form then shows **Give it as store credit** (`OrderRefundInputModel.AsStoreCredit`). When the credit can't be given, nothing is refunded and the form says why.
- **Cancel** refunds everything when asked, cancels what Pay Later still has to collect (`ITransactionCommandService.CancelAsync`), cancels the order as `CanceledByStore`, lets go of an unpaid order's checkout (so its coupon use and stock go back), and with **Put the items back in stock** returns what a paid order sold (`IInventoryService.RestockAsync`).
- **Send the confirmation again** re-sends the order confirmation (`OrderConfirmationSender`).
- **Notes** (`Order.Notes`) are what the store writes and what happened: each refund, cancellation and resend adds an event note with who did it.

Refunds are recorded on the order by `OrderRefundRecorder`, an `IPaymentRefundHandler`, wherever they were started: the order's page, the payments page or the Pay Later transaction. It finds the order from the refund's checkout (an order's, or a pay link's for the order's transaction) and records each refund once. `OrderOperationScope` carries who is acting and whether stock goes back to it.

### Disputes and fraud rating

`OrderDisputeRecorder`, an `IPaymentEvent`, finds the order a gateway's dispute is about (the attempt with that `ProviderReference`, through `PaymentAttemptQuery.ProviderReference`, then its checkout) and keeps each dispute on `Order.Disputes` (`OrderDispute`: reference, gateway, amount, reason, status, `IsClosed`), with an event note for each change; a repeated webhook changes nothing. `Order.HasOpenDispute` drives the **Disputed** badge in the orders list, and `OrderDisputesDisplayDriver` shows each dispute on the store's order page.

`OrderPaymentRiskCheckoutHandler` keeps the riskiest rating of a paid order's payments on `Order.PaymentRisk` (`OrderPaymentRisk`: level, score, gateway); `IsElevated` drives a warning on the order's page and a **Risky** badge in the list (`OrderPaymentRiskDisplayDriver`).

### Order bumps and offers after the purchase

`OrderSettings.OrderBumpProductId`, `OrderBumpPercentOff` and `OrderBumpHeadline` set the order bump. `OrderBumpCheckoutFlowDisplayDriver` renders a `CheckoutOrderBump` shape in the payment step's aside for an order that doesn't have the product, in the order's currency, while it's in stock. Ticking it posts the checkout's `update` form action (`CheckoutConstants.FormActions.Update`), which the steps layout now saves and shows again like applying a code. The bump goes on the session (`CheckoutOrderBump`) and as a billing line on the order's step, and `OrderCheckoutHandler` adds it to the order as a line when the checkout completes. The confirmation renders an `OrderConfirmationOffers` shape with the cross-sells of what was bought.

### Exporting orders

`OrdersAdminController.Export` (`Admin/orders/export`) writes the orders the list's search and status find with `OrderCsv`: a row per line, the order's totals on its first line only, times in the site's time zone. `OrderOperationsService.IsOwedAsync` tells an order paid offline and still owed, which the export, the lists and the order page show as *Payment due*.

### Reports

With the [Reports](reports) feature on, the Orders feature adds these reports under **Reports > Commerce** (permission `ManageOrders`). They read the orders paid in the chosen period (`OrderReportReader`, by `PaidUtc`, page by page), including orders canceled since, and keep every amount in its own currency. The arithmetic is in `OrderReportAggregator`.

| Report | Shows |
| --- | --- |
| Sales | Orders, items, average order value (total charged over orders), items per order, gross sales (the lines), discounts, shipping, tax, total charged, refunded and net, per currency, by day in the site's time zone (by month for periods over 62 days), with a chart. |
| Sales by product | Orders, units and sales (line totals before discounts) per product and variant, with the top ten by units, and the same by kind of product (goods, digital, services). |
| Sales by category | Orders, units and sales per category: the taxonomy terms in `OrderLine.CategoryIds`, named from the site's published taxonomies (`ProductCategoryNames`). A line in two categories counts in both; one in none counts under *No category*. |
| Customers | Buyers in the period, per currency: first-time (no paid order before the period) and returning, the share who have paid for more than one order, the average lifetime spend (net of refunds, up to the period's end), and the top 25 buyers. A buyer is known by their customer record, else their account, else their email. |
| Cohorts and retention | Buyers by the month of their first paid order in the period (`OrderCohortsReport.Build`), and for each of the six months after, the share who paid for another order in it; a month that hasn't ended is left blank. |
| Checkouts | Order checkouts (`CheckoutSessionIndex`, reference type `Order`) started in the period by how they ended: paid, payment failed, left or expired, or still open, with the paid rate, by day or month. |
| Sales by country | Orders, tax and total per country the orders ship to, or are billed to. |
| Tax on orders | Tax charged and what it was charged on, by day or month. |
| VAT one-stop shop (OSS) | VAT on orders to other EU countries by destination country and rate, per currency, refunds taken off in proportion (`OrderVatOssReport`; see [Taxation](taxation.md#taxes-on-orders-and-the-one-stop-shop)). |
| Discounts on orders | Orders that used each promotion code and what it took off. |
| Refunds | Each refund given in the period, for orders paid up to 180 days before it. |
| Disputes | The disputes opened in the period, open ones first, with why, their status and the amount, for orders paid up to 180 days before it (`OrderDisputesReport`). |
| Orders to ship | Paid orders with a shipping method not shipped in full, oldest first, whatever the period. |

Refunded in **Sales** is what was given back for the period's orders, whenever it was; **Refunds** lists refunds by the day they were given.

Other commerce features add their own reports under **Reports > Commerce** when Reports is on:

| Report | Feature | Permission | Shows |
| --- | --- | --- | --- |
| Shipping times | Shipping | `FulfillOrders` | Shipments sent in the period: average and median time from the order's payment to shipping, the share shipped within 2 days, delivery time for shipments marked delivered, returns, the same by carrier, and the 15 slowest. |
| Stock on hand | Inventory | `ViewInventory` | Every counted item now, out of stock and low first. |
| Stock movements | Inventory | `ViewInventory` | Per item, the units received, sold, returned and corrected (adjustments and counts) in the period, what is on hand now, and the days it lasts at the period's rate of sale. |
| Downloads | Digital Downloads | `ManageDigitalDownloads` | Download links given in the period, per product and file: used, never used, downloads made, used up, expired and taken back. |
| Product reviews | Product Reviews | `ManageProductReviews` | Reviews written in the period: waiting, approved and rejected, the stars given, verified purchases, and each product's average from its approved reviews. |

### Report Builder data source

The Orders feature publishes the **Orders** data source (`Orders`) for the [Report Builder](report-builder/index.md) and Orchard Core's data pipelines. It is registered when Orchard Core's Data Pipelines Core feature (`OrchardCore.DataPipelines.Core`) is on, which the Report Builder turns on. Each data set reads the newest orders first, a page at a time, through the order index, and narrows the read to the period a report's filters put on its main date, to the orders a join needs, and to the statuses and sales channels a filter allows. A person who may not use a data set does not see it, and a report that uses it gets no rows.

| Data set | Rows | Feature | Permission |
| --- | --- | --- | --- |
| **Orders** (`Orders`) | One per order, drafts and quotes included | Orders | `ManageOrders` |
| **Order lines** (`OrderLines`) | One per product line of an order | Orders | `ManageOrders` |
| **Order refunds** (`OrderRefunds`) | One per refund that went through | Orders | `ManageOrders` |
| **Order disputes** (`OrderDisputes`) | One per dispute, or chargeback, against an order's payment | Orders | `ManageOrders` |
| **Order discounts** (`OrderDiscounts`) | One per discount an order was given | Orders | `ManageOrders` |
| **Order taxes** (`OrderTaxes`) | One per tax an order was charged, by jurisdiction | Orders | `ManageOrders` |
| **Returns** (`Returns`) | One per return a buyer asked for | Returns | `ManageOrders` |
| **Gift cards** (`GiftCards`) | One per gift card | Gift Cards | `ManageGiftCards` |

Money is in the order's own currency, which the `Currency` field of the same row names, and each money field says so in its description. Personal details are those the order page shows (the customer's name and email, and the city, region, postal code and country of the addresses), and an anonymized order has none. The street lines, phone number, checkout fields, notes and gift card codes are never exposed: a gift card shows only the last characters of its code.

| Data set | Fields |
| --- | --- |
| **Orders** | `ItemId` (identifier), `OrderNumber`, `Status`, `Channel`, `Culture`, `PurchaseOrderNumber`, `LineCount`, `ItemCount` (units), `CartId` (references `Carts.Carts`), `CheckoutSessionId` (references `Checkout.CheckoutSessions`); money: `Currency`, `Subtotal`, `DiscountTotal`, `ShippingAmount`, `TaxAmount`, `Total`, `TenderedAmount` (paid from gift cards and store credit), `RefundedAmount`, `NetAmount` (total less refunds), `RefundStatus`, `HasOpenDispute`, `PaymentRisk`; dates: `CreatedUtc` (placed, the main date), `PaidUtc`, `CanceledUtc`, `CancelReason`, `ModifiedUtc`, `IsAnonymized`, `RecoveryEmailSentUtc` (when the buyer was emailed a link back to the unpaid checkout), `IsRecovered` (paid after that email); customer: `CustomerId` (references `Customers.Customers`), `UserId` (references `Users`), `IsGuest`, `CustomerName`, `CustomerEmail`, `CompanyId` (references `Companies.Companies`); addresses: `BillingCountry`, `BillingRegion`, `BillingCity`, `BillingPostalCode`, `ShippingCountry`, `ShippingRegion`, `ShippingCity`, `ShippingPostalCode`; fulfillment: `FulfillmentStatus`, `ShippingMethod`, `ShippingProvider`, `IsPickup`; store: `LocationId` (references `Inventory.Locations`), `RegisterId` (references `PointOfSale.Registers`), `RegisterName`, `ShiftId` (references `PointOfSale.RegisterShifts`), `StaffUserId` (references `Users`), `StaffName`; quote and approval: `QuoteStatus`, `QuoteRequestedUtc`, `QuoteSentUtc`, `ApprovalStatus`, `IsInvoice`, `InvoiceDueUtc` |
| Every order entry data set | `OrderId` (references `Orders`), `OrderNumber`, `OrderStatus`, `OrderChannel`, `OrderCreatedUtc` (the main date), `OrderPaidUtc`, `Currency`, `CustomerId` (references `Customers.Customers`), then the fields below |
| **Order lines** | `LineId` (identifier), `Position`, `Quantity`, `UnitPrice`, `LineTotal`, `QuotedUnitPrice`, `PriceName`, `AddOns`, `AddOnAmount`, `ExcludeFromDiscounts`, `ShipsFromUtc`, `ProductId` (references `Products.Products`), `ProductVersionId`, `ContentType`, `Title`, `Sku`, `VariantId` (references `Products.ProductVariants`), `VariantTitle`, `ProductType`, `Categories` (category ids, comma separated), `IsBundle`, `TaxCategoryCode` |
| **Order refunds** | `RefundId` (identifier), `Amount`, `Reason`, `RefundedUtc` |
| **Order disputes** | `DisputeReference` (identifier), `Gateway`, `Amount`, `DisputeCurrency`, `Reason`, `Status`, `IsClosed`, `OpenedUtc`, `UpdatedUtc` |
| **Order discounts** | `Code`, `Description`, `Amount`, `DiscountProvider`, `Target` |
| **Order taxes** | `JurisdictionId`, `JurisdictionName`, `TaxCode`, `TaxName`, `TaxType`, `Rate`, `TaxableAmount`, `TaxAmount`, `Treatment`, `IncludedInPrice`, `IsCompound`, `BillingCountry`, `BillingRegion` |
| **Returns** | `ItemId` (identifier), `Number`, `OrderId` (references `Orders`), `OrderNumber`, `UserId` (references `Users`), `Status`, `Resolution`, `LineCount`, `Quantity`, `RestockedUnits`, `Currency`, `ReturnedAmount`, `RefundedAmount`, `RestockLocationId` (references `Inventory.Locations`), `CreatedUtc` (the main date), `ApprovedUtc`, `ReceivedUtc`, `ClosedUtc` |
| **Gift cards** | `ItemId` (identifier), `LastCharacters`, `Currency`, `InitialValue`, `Balance`, `SpentAmount`, `IsEnabled`, `ExpiresUtc`, `RecipientName`, `RecipientEmail`, `OrderId` (references `Orders`), `OrderNumber`, `UseCount`, `LastUsedUtc`, `CreatedUtc` (the main date), `UpdatedUtc` |

The main date of the orders and their entries is when the order was placed, since the order index holds it; to report by payment, filter or group on `PaidUtc` or `OrderPaidUtc`. A report on **Orders** alone that counts or adds up fields the index holds, such as orders by status, sales channel or day placed, is grouped in the database. The orders read by date through the `IDX_OrderIndex_Report` database index, returns through `IDX_OrderReturnIndex_Report`, and gift cards through `IDX_GiftCardIndex_Report`, added by a migration on upgrade.

### Store reports to adapt

The **Reports: store reports to adapt** recipe (`CommerceReportBuilderReports`) turns on the Report Builder and adds reports built from these data sets, in the **Store** report group. They show what the built-in reports above show, so you can open one in the builder, change it, and save it as your own. The person who runs the recipe owns them, and they read with that person's access.

| Report | Built from | Matches the built-in report |
| --- | --- | --- |
| Sales by day | **Orders**, paid in the last 30 days, grouped by day and currency | Sales |
| Sales by product | **Order lines** of orders paid in the last 30 days, grouped by product and variant | Sales by product |
| Sales by country | **Orders**, grouped by the country they ship to | Sales by country |
| Tax by jurisdiction | **Order taxes**, grouped by jurisdiction, tax and rate | Tax on orders |
| Discounts on orders | **Order discounts**, grouped by code | Discounts on orders |
| Refunds | **Order refunds** given in the last 30 days | Refunds |
| Disputes | **Order disputes** opened in the last 90 days, open ones first | Disputes |
| Orders to ship | Paid **Orders** with a shipping method that are not shipped in full | Orders to ship |

The customers, cohorts, checkouts, sales by category and VAT one-stop-shop reports work out figures across orders and periods, such as a buyer's first order, that a report built in the builder does not, so they stay built-in only.

### Workflow events

With Workflows on, **Order Paid**, **Order Canceled** and **Order Refunded** events start workflows. Their input has `Order`, `OrderId`, `OrderNumber`, `Total`, `Currency` and `CustomerEmail`; a cancel adds `Reason` and `Note`, and a refund adds `Amount`, `Reason`, `RefundedAmount` and `FullyRefunded`. An order replaced by a newer one from the same cart doesn't start Order Canceled.

### Screens and permissions

| Screen | Permission | Default roles |
| --- | --- | --- |
| **Commerce > Orders**: every order, searchable by order number or customer email, with a status filter | `ManageOrders` | Administrator |
| **Commerce > My Orders**: the signed-in customer's own orders | `ViewOwnOrders` | Authenticated |
| **Create order**, an order's **Invoice**, **Packing slip** and **Add items** | `ManageOrders` | Administrator |
| **Commerce > Returns** and **Settings > Commerce > Returns** (Returns feature) | `ManageOrders` | Administrator |
| **Commerce > Gift cards** (Gift Cards feature) | `ManageGiftCards` | Administrator |

### Sales Summary widget

With Orchard Core's Admin Dashboard on, the **Sales Summary** dashboard widget (`SalesSummaryWidget`, `SalesSummaryPart`) shows the sales net of refunds in the store's currency today and over two windows of days, with the number of orders, and the orders to ship (`OrderReportReader.ReadUnfulfilledAsync`). Orders in other currencies count in the number of orders only.

The widget has these options: the **first window** (7 days) and **second window** (30 days) in days, the **orders counted** (all channels, online orders, point of sale, or orders made by staff; an order with no channel counts as online), and **Keep for (minutes)**, how long it keeps its figures before it counts the orders again (30 by default, 0 for every visit). For more insight, the [report widgets](reports#dashboard-widgets) put the sales, top products, orders to ship, refunds, disputes and more on the dashboard, each with its own period.

### Automation recipes

Orders adds the recipes **Automation: thank-you email** (on) and **Automation: canceled order alert** (added off), and Inventory adds **Automation: low stock alert** (added off). Each turns on Workflows and Email and adds a `WorkflowType` with stable ids: `OrderPaidEvent`, `OrderCanceledEvent` or `LowStockEvent` leading to an `EmailTask` in Liquid over `Workflow.Input`.

## Wishlists

The **Wishlists** feature (`CrestApps.OrchardCore.Carts.Wishlists`) keeps a `Wishlist` (`WishlistIndex` by `UserId`, collection `Carts`) of a signed-in user's saved products, newest first, up to 100.

- `WishlistButtonDisplayDriver` adds the `WishlistButton` shape to a product's `Detail` display (`Content:before:6`). It posts to `wishlist/{contentItemId}/add` (`WishlistAdd`) or `.../remove` (`WishlistRemove`), or links to sign in.
- `account/wishlist` (`AccountWishlist`, the `AccountWishlist` shape) lists the saved products that are still published, each with its `ProductAction` display (its add to cart), and adds **Wishlist** to the account menu.
- With Data Pipelines Core on, it adds the **Wishlists** and **Wishlist items** data sets to the [Carts data source](#carts-report-builder-data-source).

## API

Two features put carts and orders on the commerce JSON API at `api/commerce/v1`:

- **Cart API** (`CrestApps.OrchardCore.Carts.Api`, it needs Carts) maps `cart` and `cart/lines` for guests and signed-in buyers through `ICartService`, and `cart/merge` for a buyer who signed in after shopping as a guest. A guest's cart travels in the `X-Cart-Token` header, issued by the answer that creates the cart.
- **Orders API** (`CrestApps.OrchardCore.Orders.Api`, it needs Orders, the Cart API and the Checkout API) maps `POST checkout`, which checks the caller's cart out through `CartCheckoutService` (placing the order and holding its stock) and returns the guest's `X-Checkout-Token`; the buyer's `me/orders`; and the staff `orders` endpoints, which list, get, refund and cancel through `OrderOperationsService` with `ManageOrders`. With **Returns** on it adds the buyer's returns under `me/orders/{orderId}/returns`, and with **Gift Cards** on, `checkout/{id}/gift-cards`.

An order (`ApiOrder`, under `me/orders/{orderId}` and `orders/{orderId}`) has its `culture` (`Order.Culture`) and its `taxes`: `Order.TaxLines` grouped by name (the tax name, else the type), type, jurisdiction, rate and whether it was included in the price, each an `ApiOrderTax` with `name`, `type`, `jurisdiction`, `rate` (a fraction, such as `0.19`), `taxable` and `amount` (`ApiMoney`, summed) and `includedInPrice`.

See [Commerce API](commerce-api.md) for the endpoints, the conventions they share, and how apps sign in, and [Headless commerce API](../user-manual/commerce/headless-api.md) in the User Manual for setting it up in the admin.

## Stock

Products' `IProductAvailabilityProvider` says what can be bought; Inventory provides one, and without it every product can always be bought.

- **Cart.** `CartService` asks for each line with the cart's id. An out-of-stock product that doesn't take backorders is refused (*Mug is out of stock.*), and a line's `MaxQuantity` is capped at what's available. Adding or updating beyond that says *Only 3 of Mug left.*, and a priced line over it gets the problem *Only 3 left. Lower the quantity to check out.*, which stops the checkout.
- **Add to cart.** `AddToCartDisplayDriver` shows the product's or each variant's availability: *In stock*, *Only N left* at or below the threshold, *Out of stock* (which turns the button off), *Available on backorder*, or *Pre-order: ships* with the date (`ProductAvailabilityStatus.Preorder`, a backorder with a `ReleaseDateUtc` to come), where the button reads **Pre-order**. The quantity is capped at what's available.
- **Pre-order dates.** `CartService` copies a pre-order's `ReleaseDateUtc` onto the priced line (`CartPricing` line `ShipsFromUtc`), which the cart, the mini cart and the order show.
- **Variant picker.** The `product-variant-picker` script reads each variant's `data-availability`, `data-availability-tone` and `data-buyable`, and updates the line and the button as options change.

## Line details and line handlers

A cart line carries details beside its product, such as a booking's date: `CartLine.Details`, by key, which a module names after itself (`booking.date`). Modules register an `ICartLineHandler`:

- `PrepareAsync` keeps the details it deals with from what the buyer sent (`PostedDetails`), checked, or refuses the line. What no handler keeps is dropped.
- `EvaluateAsync` prices the line as it stands. It can change `UnitPrice`, lower `MaxQuantity`, set `PeoplePerUnit` for add-ons priced per person, describe the details (`CartLineDetail`) or set a `Problem`. `ConvertAsync` converts an amount from the product's currency.

Handlers run by `Order`, lowest first. Product add-ons are the first handler (order 100) and always run. Two lines of a product with other details stay apart. `ICartService.AddAsync` takes the details as a default interface overload; the *Add to cart* form posts them as `details[{key}]`, and the cart API as `details`.

`OrderLines.FromPricing` copies the details onto `OrderLine.Details` and their descriptions onto `OrderLine.DetailLines`, which the order pages, emails, invoices and `OrderLineText.Describe` show. Reordering, and re-pricing a draft or a quote from its order, keep them. `CartOptions.ChooseOnProductPageParts` names the parts whose products need something chosen on their page first: a card links there instead of adding them.

## Checkout line details

Carts are the first checkout source with several products, so a `BillingItem` can carry a `Quantity` (its `Amount` is then the unit amount), a `Sku`, a `TaxCategoryCode`, a `TaxClassificationCode` and a `TaxableKind`. The invoice bills every unit, and the tax calculation uses a line's own codes and kind when it has them and the checkout's defaults otherwise. Handlers that set none of these bill and tax exactly as before.

## Not included

Saved carts, carts with recurring prices, and changing a paid order's lines: items are added to a paid order as a new order for the buyer to pay (**Add items**), and taken off through a return or a refund. Customer records and address books come with the [Customers](customers) feature, stock with [Inventory](inventory), and shipping charges with [Shipping](shipping), which fills in `Order.ShippingAddress`, `Order.Shipping` and `Order.FulfillmentStatus`. Other modules add sections to the order pages through `IDisplayManager<Order>`, in the display types `OrderConstants.DisplayTypes.DetailAdmin` and `Detail`.

## Getting started

Enable **Carts** to show **Add to cart** and the cart page. Enable **Orders** and a [payment provider](payments) to check carts out. The **Product listing: online store sample** recipe enables Carts, Orders and [Customers](customers); add a payment provider to take payments.
