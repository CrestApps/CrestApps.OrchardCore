---
sidebar_label: Carts and Orders
title: Carts and Orders
description: Shopping carts for products sold online, thin orders that check a cart out through the Checkout framework and record what was bought, orders the store creates for buyers to pay, printable invoices and packing slips, and returns.
user_manual: user-manual/commerce/carts-and-orders
---

| | |
| --- | --- |
| **Features** | `CrestApps.OrchardCore.Carts`, `CrestApps.OrchardCore.Orders`, `CrestApps.OrchardCore.Orders.Returns` (Returns) |
| **Core** | `CrestApps.OrchardCore.Carts.Core`, `CrestApps.OrchardCore.Orders.Core` |
| **Category** | Commerce |

**Carts** lets visitors put products sold online in a cart from a listing card or a product page. **Orders** checks the cart out through the [Checkout](checkout) framework and records what was bought. Together they sell goods the way [Subscriptions](subscriptions) sells plans: through the same checkout, payment providers, coupons and taxes.

## Carts

### What can go in a cart

A product can be added when it can be bought online (see [sales mode](products#product-types)) at a one-time price. The cart refuses:

- a product that is only listed, or has no price;
- a subscription plan, which is signed up for on its own, and any recurring price;
- a price that asks the buyer for an amount;
- a product priced in another currency than the cart's, because prices are not converted;
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

## Orders

### Checking out

The cart page shows **Checkout** when the Orders feature is enabled. Checking out:

1. prices the cart again and refuses it if a line cannot be bought;
2. freezes it into a **draft order**: each line's product version, title, SKU, price, quantity, unit price and tax details, and the subtotal;
3. cancels earlier unpaid orders made from the same cart, so the customer pays for what the cart holds now;
4. starts a checkout session whose reference is the order (`CheckoutReferenceTypes.Order`) and sends the customer to it.

`OrderCheckoutHandler` bills one line per order line, with its quantity, SKU, tax category, tax classification and product type, so each line is taxed as what it is, and with its product and the categories it was in when ordered (`OrderLine.CategoryIds`, every taxonomy term on the product), so a coupon limited to some products or categories finds it. A canceled order gives back the coupon uses of its checkout (`ICouponRedemptionService.ReturnAsync`). It refuses to complete a checkout that bills nothing. When the checkout completes, the order is marked **Paid** with the total and tax that were charged and the discounts it was given (`Order.Discounts`, each with its code, description, amount and provider, and `Order.DiscountTotal`), so its lines, discounts, shipping and tax add up to what was paid. What was bought is taken out of the cart, matching each line's product, price and variant; anything added to the cart after checking out stays in it. An order replaced by a newer one is still marked paid if its own checkout completes, because that payment bought it.

The checkout's confirmation page shows the order number.

**Guest access.** `IOrderLinkService` makes a signed, expiring link to one order (data protection with a time limit of 60 days, purpose `CrestApps.OrchardCore.Orders.OrderLink`; the site's base URL first, then the current request). `GET orders/view/{token}` (`OrderViewController`) opens it read-only, with a pay link when a Pay Later transaction for the order is still outstanding; a bad or expired token shows nothing about any order. The confirmation page and a guest's confirmation email carry the link.

**Find my order.** `GET/POST orders/find` takes an email and emails a link to each paid or awaiting-payment order placed with it in the last year (up to 10), finding them through the indexed `OrderIndex.CustomerEmail` (`OrderQuery.CustomerEmail`). The answer is the same whether or not orders exist. Posts are limited per IP and per email through `IPaymentAttemptLimiter`, and carry the `find-order` rate-limit group for the Rate Limits feature.

**Order confirmations.** Other modules add sections to the confirmation email through `IOrderEmailSectionProvider`, such as the download links of [Digital Downloads](downloads). Once a checkout completes and is committed, `OrderConfirmationCheckoutHandler` emails the paid order's confirmation to `Order.CustomerEmail` through `IEmailService`, built by `OrderConfirmationEmailBuilder` (order number, lines, discounts, shipping, tax, total, addresses; every value HTML-encoded). It sends once (`Order.ConfirmationSentUtc`), only when `OrderSettings.SendConfirmations` is on (the default, under **Settings > Commerce > Orders**, with **Manage orders**) and an email service is registered. A failed send is logged and leaves the order unconfirmed; it never undoes the paid order.

Who the order is for comes from the checkout's `CheckoutContactInfo`: its name, email and phone replace the signed-in account's when the buyer entered them, and its `BillingAddress` is copied onto `Order.BillingAddress` (an immutable `Address`) when the order is paid. With the [Customers](customers) feature on, the checkout's **Contact** step fills it in, for guests and signed-in buyers alike, and the order is linked to its customer (`Order.CustomerId`).

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

### Returns

The **Returns** feature (`CrestApps.OrchardCore.Orders.Returns`) adds buyer-requested returns. An `OrderReturn` (in the `Orders` collection, `OrderReturnIndex` on number, order, user, status and date) has a `Number` (such as `R-261019-K3QZ`), the order, the buyer, `Lines` (`OrderReturnLine`: order `LineId`, title, SKU, quantity, the unit price paid, `Reason`, `Restocked`), a `Resolution` (`Refund` or `Exchange`, with `ExchangeFor`), the buyer's `Comment`, the store's `StoreMessage`, `RefundedAmount`, `RestockLocationId`, and a `Status`: `Requested`, `Approved`, `Received`, `Completed`, `Rejected` or `Canceled` (withdrawn by the buyer).

- **Policy.** `OrderReturnPolicy.GetReturnable(order, returns)` gives the units of each good (`ProductType.Good` or undefined) not covered by an active return; `IsOpen(order, settings, utcNow)` is true for a paid order within `ReturnSettings.WindowDays` of `PaidUtc`. `UnitPriceOf(line)` is the line total over its quantity, add-ons and line discounts included.
- **Buyer.** `account/orders/{orderId}/return` (`AccountOrderReturn`) and `orders/view/{token}/return` (`GuestOrderReturn`) show the `OrderReturnRequest` shape and post the request; `…/returns/{returnId}/withdraw` withdraws one still `Requested`. `OrderReturnsDisplayDriver` adds `OrderReturns_Detail` (the buyer's returns, with **Return items** and the return instructions once approved) to a paid order's `Detail` display, and `OrderReturns_DetailAdmin` to the store's.
- **Store.** `ReturnsAdminController` (`Admin/returns`, `ManageOrders`) lists returns through an `AdminList` with a status filter and search by return or order number, and acts through `OrderReturnService`: `approve`, `reject` (with a message), `receive` (the lines to restock and an optional location), `refund` (an amount, through `OrderOperationsService.RefundAsync` without restocking, then `Completed`) and `exchanged`. Each step adds an order note, and `OrderReturnEmailSender` emails the buyer (requested, approved, declined, refunded, exchanged); a failed send is logged and never undoes the step.
- **Restocking.** Receiving calls each `IOrderEventHandler.ReturnedAsync` with an `OrderReturnedContext` (the order, the return, `Lines` of `OrderReturnedLine(Line, Quantity, Restock)`, `LocationId`, actor) in the same unit of work. Inventory puts the restocked lines back with `IInventoryService.RestockAsync(orderId, lines, locationId, …)`.
- **Settings.** `ReturnSettings` (**Settings > Commerce > Returns**, group `returns`, `ManageOrders`): `WindowDays` (30, 1 to 365), `Reasons` (up to 20), `AllowExchanges` (true) and `Instructions`.

Exchanges are closed by the store once it has sent the replacement; no replacement order is created. Returns start with the buyer; the store's own refunds and restocking stay on the order's page.

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

- **Refund** gives back an amount, or everything still refundable. Money paid online at checkout is refunded through `ICheckoutRefundService`, newest payment first; money paid later is refunded through `ITransactionCommandService.RefundAsync` on the order's Pay Later transaction. A refund waiting to be settled by hand blocks another one.
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
| Checkouts | Order checkouts (`CheckoutSessionIndex`, reference type `Order`) started in the period by how they ended: paid, payment failed, left or expired, or still open, with the paid rate, by day or month. |
| Sales by country | Orders, tax and total per country the orders ship to, or are billed to. |
| Tax on orders | Tax charged and what it was charged on, by day or month. |
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

### Workflow events

With Workflows on, **Order Paid**, **Order Canceled** and **Order Refunded** events start workflows. Their input has `Order`, `OrderId`, `OrderNumber`, `Total`, `Currency` and `CustomerEmail`; a cancel adds `Reason` and `Note`, and a refund adds `Amount`, `Reason`, `RefundedAmount` and `FullyRefunded`. An order replaced by a newer one from the same cart doesn't start Order Canceled.

### Screens and permissions

| Screen | Permission | Default roles |
| --- | --- | --- |
| **Commerce > Orders**: every order, searchable by order number or customer email, with a status filter | `ManageOrders` | Administrator |
| **Commerce > My Orders**: the signed-in customer's own orders | `ViewOwnOrders` | Authenticated |
| **Create order**, an order's **Invoice**, **Packing slip** and **Add items** | `ManageOrders` | Administrator |
| **Commerce > Returns** and **Settings > Commerce > Returns** (Returns feature) | `ManageOrders` | Administrator |

## Stock

Products' `IProductAvailabilityProvider` says what can be bought; Inventory provides one, and without it every product can always be bought.

- **Cart.** `CartService` asks for each line with the cart's id. An out-of-stock product that doesn't take backorders is refused (*Mug is out of stock.*), and a line's `MaxQuantity` is capped at what's available. Adding or updating beyond that says *Only 3 of Mug left.*, and a priced line over it gets the problem *Only 3 left. Lower the quantity to check out.*, which stops the checkout.
- **Add to cart.** `AddToCartDisplayDriver` shows the product's or each variant's availability: *In stock*, *Only N left* at or below the threshold, *Out of stock* (which turns the button off), *Available on backorder*, or *Pre-order: ships* with the date (`ProductAvailabilityStatus.Preorder`, a backorder with a `ReleaseDateUtc` to come), where the button reads **Pre-order**. The quantity is capped at what's available.
- **Pre-order dates.** `CartService` copies a pre-order's `ReleaseDateUtc` onto the priced line (`CartPricing` line `ShipsFromUtc`), which the cart, the mini cart and the order show.
- **Variant picker.** The `product-variant-picker` script reads each variant's `data-availability`, `data-availability-tone` and `data-buyable`, and updates the line and the button as options change.

## Checkout line details

Carts are the first checkout source with several products, so a `BillingItem` can carry a `Quantity` (its `Amount` is then the unit amount), a `Sku`, a `TaxCategoryCode`, a `TaxClassificationCode` and a `TaxableKind`. The invoice bills every unit, and the tax calculation uses a line's own codes and kind when it has them and the checkout's defaults otherwise. Handlers that set none of these bill and tax exactly as before.

## Not included

Saved carts, carts with recurring prices, and changing a paid order's lines: items are added to a paid order as a new order for the buyer to pay (**Add items**), and taken off through a return or a refund. Customer records and address books come with the [Customers](customers) feature, stock with [Inventory](inventory), and shipping charges with [Shipping](shipping), which fills in `Order.ShippingAddress`, `Order.Shipping` and `Order.FulfillmentStatus`. Other modules add sections to the order pages through `IDisplayManager<Order>`, in the display types `OrderConstants.DisplayTypes.DetailAdmin` and `Detail`.

## Getting started

Enable **Carts** to show **Add to cart** and the cart page. Enable **Orders** and a [payment provider](payments) to check carts out. The **Product listing: online store sample** recipe enables Carts, Orders and [Customers](customers); add a payment provider to take payments.
