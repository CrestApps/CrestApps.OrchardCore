---
sidebar_label: Carts and Orders
title: Carts and Orders
description: Shopping carts for products sold online, and thin orders that check a cart out through the Checkout framework and record what was bought.
user_manual: user-manual/commerce/carts-and-orders
---

| | |
| --- | --- |
| **Features** | `CrestApps.OrchardCore.Carts`, `CrestApps.OrchardCore.Orders` |
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
| `CartLink` | A cart link with the item count | The site's navigation bar, beside the user menu |

Each shape can be overridden in Razor or Liquid. A theme hides the cart link with an empty `CartLink` template.

The forms are ordinary POST forms with antiforgery tokens, so they work without JavaScript and redirect back with a notification. A request whose `Accept` header asks for `application/json` gets `{ succeeded, message, itemCount }` instead. The endpoints are `POST cart/add`, `POST cart/update` and `POST cart/remove`, in the `Cart` rate limit group of the Orchard Core Rate Limits module.

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

`OrderCheckoutHandler` bills one line per order line, with its quantity, SKU, tax category, tax classification and product type, so each line is taxed as what it is. It refuses to complete a checkout that bills nothing. When the checkout completes, the order is marked **Paid** with the total and tax that were charged, and what was bought is taken out of the cart; anything added to the cart after checking out stays in it. An order replaced by a newer one is still marked paid if its own checkout completes, because that payment bought it.

The checkout's confirmation page shows the order number.

**Order confirmations.** Once a checkout completes and is committed, `OrderConfirmationCheckoutHandler` emails the paid order's confirmation to `Order.CustomerEmail` through `IEmailService`, built by `OrderConfirmationEmailBuilder` (order number, lines, tax, total, billing address; every value HTML-encoded). It sends once (`Order.ConfirmationSentUtc`), only when `OrderSettings.SendConfirmations` is on (the default, under **Settings > Commerce > Orders**, with **Manage orders**) and an email service is registered. A failed send is logged and leaves the order unconfirmed; it never undoes the paid order.

Who the order is for comes from the checkout's `CheckoutContactInfo`: its name, email and phone replace the signed-in account's when the buyer entered them, and its `BillingAddress` is copied onto `Order.BillingAddress` (an immutable `Address`) when the order is paid. With the [Customers](customers) feature on, the checkout's **Contact** step fills it in, for guests and signed-in buyers alike, and the order is linked to its customer (`Order.CustomerId`).

### Order statuses

| Status | Meaning |
| --- | --- |
| Draft | Made from a cart, not yet sent to the checkout. |
| Awaiting payment | Its checkout started and has not completed. |
| Paid | Its checkout completed. |
| Canceled | Replaced by a newer order from the same cart before it was paid. |

An order holds lines, not money. The payment, its tax, the payment attempts and any refund belong to the checkout session the order names, and refunds are made from the payment screens.

### Screens and permissions

| Screen | Permission | Default roles |
| --- | --- | --- |
| **Commerce > Orders**: every order, searchable by order number or customer email, with a status filter | `ManageOrders` | Administrator |
| **Commerce > My Orders**: the signed-in customer's own orders | `ViewOwnOrders` | Authenticated |

## Checkout line details

Carts are the first checkout source with several products, so a `BillingItem` can carry a `Quantity` (its `Amount` is then the unit amount), a `Sku`, a `TaxCategoryCode`, a `TaxClassificationCode` and a `TaxableKind`. The invoice bills every unit, and the tax calculation uses a line's own codes and kind when it has them and the checkout's defaults otherwise. Handlers that set none of these bill and tax exactly as before.

## Not included

Shipping and fulfillment, inventory, saved carts, carts with recurring prices, and order editing. Customer records and address books come with the [Customers](customers) feature.

## Getting started

Enable **Carts** to show **Add to cart** and the cart page. Enable **Orders** and a [payment provider](payments) to check carts out. The **Product listing: online store sample** recipe enables Carts.
