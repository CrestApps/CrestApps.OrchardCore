# Carts and orders plan

Status: K0 to K3 built on `claude/commerce-carts-orders`, stacked on the product listing PR (#834).

## Goal

A visitor adds products sold online to a cart from a listing card or a product page, reviews the cart, and pays
through the existing checkout. The purchase is recorded as an order that names what was bought.

## Decisions (agreed 2026-10-13)

| # | Decision | Why |
| --- | --- | --- |
| K-D1 | Build **Carts** and a **thin Orders** module now. A cart's owner is a `CustomerOwner` (a signed-in user, or a guest identified by a cart cookie). The Customers module comes later and plugs into the same owner contract. | Reaches a working Add to cart without waiting for profiles and address books, which a cart does not need. |
| K-D2 | A cart holds **one-time prices only**. Subscription plans keep their own Sign up flow. | Mixed carts create several payment obligations that not every provider supports; the roadmap lists them as out of scope at first. |
| K-D3 | A separate PR stacked on #834, retargeted to `main` when #834 merges. | Keeps #834 reviewable. |

The rest follows the foundation decisions (F5) and the e-commerce plan:

- A cart is a tenant YesSql document, keyed by a guest token or the user id. Signing in merges the guest cart.
  Carts expire after a configured window.
- A cart never accepts a client total. Lines are re-resolved through `IProductSnapshotResolver` and
  `IPriceResolver` whenever the cart is shown and when it becomes an order.
- A cart becomes a **draft order**, and the checkout runs against the order (`CheckoutReferenceTypes.Order`).
  An order is marked paid only by the checkout's completion, never from session state alone.
- One currency per cart. Prices are not converted.
- Carts do not call payment providers and do not touch stock.

## Slices

### K0. Checkout lines carry quantity and product details

Today a `BillingItem` is one amount, the invoice forces quantity 1, and the whole invoice is taxed with one
category. A cart needs real lines.

- `BillingItem` gains `Quantity` (default 1; `Amount` is then the unit amount), `Sku`, `TaxCategoryCode`,
  `TaxClassificationCode` and `TaxableKind`. Existing handlers set none of them, so their invoices do not change.
- `CheckoutLineItem` carries the same values, and the invoice builder copies them.
- `CheckoutTaxContextFactory` uses a line's own tax codes and kind when it has them, and the profile's defaults
  otherwise.

### K1. Carts

Projects: `CrestApps.OrchardCore.Carts.Core` (model, store, service) and the `CrestApps.OrchardCore.Carts` module.

- `Cart` document: `CartId`, owner (user id, or the hash of a guest token), `Currency`, `Lines`, `CreatedUtc`,
  `ModifiedUtc`, `ExpiresUtc`. `CartIndex` by owner and expiry.
- `CartLine`: `LineId`, `ContentItemId`, `PriceId`, `Quantity`, `AddedUtc`, and the unit price shown when added,
  so a changed price can be pointed out.
- `ICartService`: get the current cart, add, change a quantity, remove, clear, and merge a guest cart into a
  user's cart at sign-in. Adding refuses a product that cannot be bought, a recurring price, a price in another
  currency than the cart's, and a quantity the price does not allow.
- Guest identity: a `cart_owner` cookie through `GuestSessionTokenManager`; only its hash is stored.
- Endpoints: `POST cart/add`, `POST cart/update`, `POST cart/remove`, and the `GET cart` page. They are
  antiforgery-protected, rate limited, and redirect back (or answer JSON for scripts).
- Shapes: `Cart` (the page), `CartLine`, `CartSummary` (totals) and `CartLink` (a header link with the item
  count, for themes to place), each overridable in Razor or Liquid.
- A background task deletes expired carts.

### K2. Thin orders

Projects: `CrestApps.OrchardCore.Orders.Core` and the `CrestApps.OrchardCore.Orders` module.

- `Order` document: `OrderId`, `OrderNumber`, owner, `Currency`, frozen `Lines` (product, version, title, SKU,
  quantity, unit price, line total, tax codes), `Subtotal`, `CheckoutSessionId`, `Status`
  (`Draft`, `AwaitingPayment`, `Paid`, `Canceled`), `CreatedUtc`, `PaidUtc`.
- Checkout: `POST cart/checkout` re-resolves the cart, freezes it into a draft order, and starts a checkout
  session for the order. An `OrderCheckoutHandler` bills one `BillingItem` per line. Completing the checkout
  marks the order paid and empties the cart it came from. A canceled or expired checkout leaves the order a
  draft and the cart intact.
- Screens: an admin **Orders** list and detail page under Commerce, and a customer **My Orders** list.

### K3. Add to cart, docs and samples

- The Carts module places an `AddToCart` shape in the Actions zone of the product's `ProductAction` display
  when the product can be bought and has a one-time price, so it shows on listing cards and product pages.
- The store sample recipe enables Carts. Orders also needs a payment provider, so a site enables it with one.
- Docs: Carts and Orders pages, the Products page's actions section, and the changelog.

## Out of scope

Customer profiles and address books, shipping and fulfillment, inventory, saved carts, mixed carts with
recurring prices, order editing, and refunds from the order screen (refunds stay on the payment screens).
