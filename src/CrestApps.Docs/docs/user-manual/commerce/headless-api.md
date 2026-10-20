---
sidebar_label: Headless API
title: Headless commerce API
description: Run your store from your own storefront, a phone app, a point of sale or a back-office system over the commerce JSON API, with the features to turn on, how apps and servers sign in, the conventions every call follows, a guest checkout call by call, and every endpoint.
technical_manual:
  - modules/products
  - modules/carts-and-orders
  - modules/checkout
  - modules/customers
  - modules/inventory
  - modules/shipping
---

The **commerce API** lets software other than your site's own pages sell from your store. It's a JSON API over HTTPS, and it uses the same products, prices, carts, checkout, stock and orders as your storefront, with the same rules: a coupon, a tax rate or a stock hold works the same whether the buyer is on your site or in an app.

Use it to build:

- **A headless storefront**: your own website, in any framework, that shows your products and checks buyers out.
- **A phone app** your buyers sign in to, to shop, pay and follow their orders.
- **A point of sale** at your shop's counter. See [Point of sale](point-of-sale.md).
- **A back-office integration**, such as a warehouse system that ships orders and receives stock, or an accounting system that reads orders and refunds.

To have your store tell another system when something happens, such as an order being paid, instead of that system asking, use [webhooks](webhooks.md).

| | |
| --- | --- |
| **Address** | `https://your-site/api/commerce/v1/...` (on a tenant with a URL prefix, such as *shop*: `https://your-site/shop/api/commerce/v1/...`) |
| **Who calls it** | Guests with no account, signed-in buyers, and staff or servers with a token |
| **Feature** | Catalog API, Cart API, Inventory API, Checkout API, Orders API, Customers API, Shipping API |

<AskYourAdmin />

## Turn it on

Each commerce module has its own **API** feature, under **Configuration > Features**, in the **Commerce** category. Turn on the ones your app needs. Each needs its module turned on first.

| Feature | What it gives your app | It needs |
| --- | --- | --- |
| **Catalog API** | Lists and gets products, priced for the caller, with their variants, add-ons, specifications and stock. | Product listings |
| **Cart API** | Reads and changes the caller's cart. | Carts |
| **Checkout API** | Reads a checkout, puts discount codes on it, lists the ways to pay, takes the payment and completes it. Staff manage coupons. | Checkout |
| **Orders API** | Checks the cart out, puts gift cards toward a checkout, and shows buyers their orders and returns. Staff list, find, refund and cancel orders. | Orders, Cart API, Checkout API |
| **Customers API** | Saves the checkout's Contact step, and puts store credit and points toward a checkout. Buyers manage their account and addresses. Staff list and look up customers. | Customers |
| **Shipping API** | Lists pickup locations and shipping rates, and saves the checkout's Shipping step. Buyers follow their shipments. Staff ship orders. | Shipping, Checkout API |
| **Inventory API** | Says what's in stock at each of your locations. Staff list, receive, adjust, count and move stock. | Inventory |
| **Point of Sale** | Registers, shifts and their cash, sales rung up and paid at the counter, receipts and returns at the till. See [Point of sale](point-of-sale.md). | Orders, Inventory, Checkout API |

Some endpoints appear only when another feature is on too:

- Gift cards on a checkout need **Gift Cards**; buyers' returns need **Returns**.
- The Contact step needs **Orders** and the **Checkout API**.
- Store credit and points on a checkout need **Store Credit** or **Loyalty Points**, and the **Checkout API**.

For a typical headless store, turn on all seven.

### Document it with OpenAPI

Orchard Core's **OpenAPI** feature describes every endpoint, with its parameters, its request and response fields, and its errors, so developers can explore the API and generate a client from it.

1. Turn on **OpenAPI** under **Configuration > Features**.
2. Turn on one or more explorers: **Swagger UI** (try calls at `/swagger`), **Scalar UI** (at `/scalar/v1`) or **ReDoc UI** (read-only, at `/redoc`). On a tenant with a URL prefix, put the prefix first, such as `/shop/swagger`.
3. The description itself is at `/openapi/v1.json`.

By default, only a signed-in user with the **View OpenAPI Content** permission can read the description. The OpenAPI settings page has **Allow anonymous access to the API schema**, which opens it to everyone. Leave it off on a live store unless you want your API to be public knowledge.

The commerce endpoints are grouped in the explorers by tag: **Catalog**, **Cart**, **Checkout**, **Inventory**, **Account**, and for staff **Orders**, **Customers**, **Coupons**, **Stock**, **Fulfillment** and **Point of Sale**.

:::tip[Try calls from Swagger or Scalar]
The **OpenAPI Documentation — Bearer/PKCE** recipe, under **Configuration > Recipes**, sets up sign-in for the Swagger and Scalar pages, so an admin can try the staff endpoints from the browser. After running it, open the **openapi** application it creates (see below) and change `https://localhost:5001` in its redirect addresses to your site's address. The recipe points token validation at the *Default* tenant; on another tenant, change it to that tenant's name.
:::

## Set up sign-in

Guests need no sign-in: the endpoints a shopper uses before they have an account work without a token. Everything else needs a **bearer token** from your site's own OpenID server, sent on every call:

```http
Authorization: Bearer eyJhbGciOi...
```

The API never uses the site's sign-in cookie, so a token is the only way to call it as a signed-in buyer or as staff.

### Turn on the OpenID server

1. Under **Configuration > Features**, turn on **OpenID Authorization Server**, **OpenID Token Validation** and **OpenID Management Interface**.
2. Open **Security > OpenID Connect > Settings > Authorization Server**, and turn on:
   - the **token endpoint**, and the **authorization endpoint** for apps that sign buyers in;
   - **Allow authorization code flow**, with **Require proof key for code exchange (PKCE)**, for apps;
   - **Allow client credentials flow**, for servers;
   - **Allow refresh token flow**, if your apps should stay signed in.
3. Open **Security > OpenID Connect > Settings > Token Validation**, and choose **this tenant** as the one whose tokens are accepted. Without this, every call with a token is refused with `401`.

Your site then issues tokens at `/connect/token` and signs buyers in at `/connect/authorize` (with the tenant's prefix first, if it has one).

### An app that buyers sign in to

A storefront, a phone app or a point of sale signs the person in through your site's own sign-in page, with the **authorization code flow and PKCE**. The app never sees their password, and needs no secret of its own.

1. Under **Security > OpenID Connect > Management > Applications**, click **Add an application**.
2. Give it a **Client Id**, such as *contoso-shop-app*, and a **Display Name**.
3. Choose the **Public** type: an app on a phone or in a browser can't keep a secret.
4. Tick **Allow Authorization Code Flow** and **Require PKCE**. Tick **Allow Refresh Token Flow** if it should stay signed in.
5. Add the app's **Redirect URIs**: where your site sends the person back after they sign in, such as `https://shop.contoso.com/signin-callback` for a website or `com.contoso.shop:/signin-callback` for a phone app.
6. Allow the scopes `openid`, `profile` and `roles`. The `roles` scope puts the person's roles in the token; staff permissions come from those roles. Add `offline_access` for refresh tokens.
7. Save.

The app then uses any OpenID Connect library (such as AppAuth on iOS and Android, or oidc-client-ts in a browser) with your site's address, the client id and the redirect address. Signed-in buyers get what the **Authenticated** role allows, such as **View own orders**. Staff signing in to a point of sale get what their roles allow.

### A server that works on the store's behalf

A back-office system, such as a warehouse or accounting integration, has no person signing in. It uses the **client credentials flow**: it signs in as itself with a secret, and the roles you give the application decide what it can do.

1. Create a role for it under **Security > Roles**, such as *Warehouse integration*, with only the permissions it needs, such as **Ship orders and update their shipments**, **View inventory** and **Manage inventory**.
2. Under **Security > OpenID Connect > Management > Applications**, add an application of the **Confidential** type, with a **Client Id** and a strong **Client Secret**.
3. Tick **Allow Client Credentials Flow**, and choose the role under **Client Credentials Roles**.
4. Save, and keep the secret in the server's secret store, never in an app or a web page.

The server asks for a token like this, and sends it on each call until it expires:

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id=contoso-warehouse&client_secret=YOUR-CLIENT-SECRET
```

### What each caller needs

| Caller | Endpoints | Permission |
| --- | --- | --- |
| A guest, no token | Catalog, availability, cart, checkout and its steps (except store credit and points) | None |
| A signed-in buyer | Everything a guest can do, plus `me` (their account and addresses), their orders, returns and shipments, store credit and points at checkout, and moving a guest cart into their own | **View own orders** for orders, returns and shipments; **Manage own customer profile and addresses** for their account. Both are given to the **Authenticated** role by default. |
| Staff or a server | The staff endpoints below | The same permissions as the matching admin pages: **Manage orders**, **View customers**, **View inventory**, **Manage inventory**, **Manage coupons**, **Ship orders and update their shipments** |

A call without a token to an endpoint that needs one is answered `401`. A token whose user or application lacks the permission is answered `403`.

### Apps in a browser on another address

A storefront that runs in the browser on another address than your site, such as `https://shop.contoso.com` calling `https://admin.contoso.com`, needs your site to allow it. Turn on Orchard Core's **CORS** feature, and add a policy under **Configuration > Settings > CORS**:

| Setting | Value |
| --- | --- |
| **Allowed origins** | Your storefront's address, such as `https://shop.contoso.com`. |
| **Allowed methods** | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| **Allowed headers** | `Authorization`, `Content-Type`, `X-Cart-Token`, `X-Checkout-Token`, `Idempotency-Key` |
| **Exposed headers** | `X-Cart-Token`, `X-Checkout-Token`, `Location`. Without these, the browser hides the guest's tokens from your code. |
| **Allow credentials** | Off. The API doesn't use cookies. |

A phone app, a point of sale or a server doesn't need CORS.

## How every call works

### JSON, names and dates

Requests and responses are JSON, with `camelCase` names, such as `productId` and `billingAddress`. Values that name a state are `camelCase` strings too, such as `awaitingPayment` or `lowStock`. Dates are UTC, in ISO 8601, in fields that end in `Utc`, such as `createdUtc`.

### Money

Every amount comes with its currency: the amount in major units (dollars, not cents), rounded to the currency's precision, and the three-letter currency code.

```json
{ "amount": 49.99, "currency": "USD" }
```

### Pages of results

A list that can be long comes in pages. Ask for one with `skip` (how many to pass over, from 0) and `take` (how many to return, 20 when not given, at most 100). The answer says how many match in all:

```json
{
  "items": [ ... ],
  "total": 134,
  "skip": 40,
  "take": 20
}
```

### Errors

An error is answered with a **problem** (RFC 9457), a JSON object with the HTTP `status`, a `title` and usually a `detail` you can show:

| Status | What it means |
| --- | --- |
| `400` | Something in the request isn't right. `errors` lists the problems by the name of the field, such as `email` or `billingAddress.city`, so your app can show each next to its field. |
| `401` | The endpoint needs a token, and there was none, or it wasn't valid. |
| `403` | The token's user or application doesn't have the permission. |
| `404` | It doesn't exist, or it isn't the caller's. Another buyer's checkout, order or cart line answers the same as one that doesn't exist. |
| `409` | It can't be done right now. `code` says why (below). |
| `429` | Too many tries in a short time, such as payment attempts. Wait a moment and try again. |

A field problem:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "address.country": [ "Give the address's country, at least." ]
  }
}
```

A conflict:

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "Mug is out of stock.",
  "code": "out-of-stock"
}
```

| `code` | What happened | What your app does |
| --- | --- | --- |
| `empty-cart` | The cart is empty, so there's nothing to check out. | Send the buyer back to shopping. |
| `out-of-stock` | Something ran out, or a location hasn't that many. | Show `detail`, and let the buyer change the cart. |
| `not-available` | The change isn't possible now, such as a product no longer sold, an order that can't be refunded, or a return already received. | Show `detail`. |
| `invoice-changed` | The total changed since the buyer last saw it. | Get the checkout again, show the new total, and ask the buyer to pay again. |
| `concurrency` | Somebody else changed the same thing at the same moment. | Read it again, and retry. |
| `unsupported` | This checkout doesn't take that, such as a code on a checkout that refuses discounts, or a gift card on a subscription. | Hide that option. |
| `idempotency-in-progress` | A request with the same idempotency key is still running. | Wait a moment, and send it again. |

### Sending a request again safely

A phone loses its signal just as it sends **Pay**. Did the payment go through? To make it safe to send the request again, send an `Idempotency-Key` header with a value you make up for that action, such as a new GUID, at most 128 characters:

```http
POST /api/commerce/v1/checkout/9f3c.../payment
Idempotency-Key: 6a1f4b8e-2d7c-4f0a-9b3e-5c8d1e2f7a90
```

If you send the same request with the same key again within 24 hours, you get the first answer back, and nothing is done twice: no second order, payment, refund or shipment. The answer comes back whole, with the `X-Cart-Token`, `X-Checkout-Token` and `Location` headers the first one had, so a guest whose first try was lost still gets their checkout token. Keys are kept per caller and per endpoint, so two buyers can't collide.

- Make a **new key for each new action**, and reuse a key only to send the very same request again.
- The endpoints that take a key are those that place, pay, refund or move something: starting a checkout, beginning a payment, asking for a return, refunding and canceling an order, packing a shipment, creating a coupon, and receiving, adjusting and moving stock. Other endpoints ignore the header. Completing a checkout needs no key: it's safe to call again as it is.
- Only a final answer is kept, an error such as a `400` too. After you change a request to fix it, send it with a new key.
- An answer that may turn out differently a moment later isn't kept, so the same key tries the work again: a server error (`500` and up), `429`, and a `409` with the code `concurrency` or `idempotency-in-progress`.

### Guests: the cart and checkout tokens

A guest has no account, so their cart and checkout are known by a **token** in a header, never by a cookie:

1. The answer that creates a guest's cart has an `X-Cart-Token` header. Keep it, and send it as `X-Cart-Token` on every cart call and when starting the checkout.
2. The answer that starts a guest's checkout has an `X-Checkout-Token` header. Send it as `X-Checkout-Token` on every call about that checkout.

Treat the tokens like a password: whoever has one can see and change that cart or checkout. Keep them in the app's secure storage, not in a link.

When a guest signs in, send `POST cart/merge` with their new bearer token **and** the guest's `X-Cart-Token`: the guest cart's lines move into their own cart.

## A guest checkout, call by call

Here is a whole purchase, as a storefront makes it. Paths are shown without `https://your-site/api/commerce/v1/`, and the answers leave out some fields to stay short; the OpenAPI description lists them all.

### 1. Find a product

```http
GET products?q=mug&take=2
```

```json
{
  "items": [
    {
      "id": "4b2kx7m9qz1c",
      "type": "Product",
      "title": "Contoso mug",
      "sku": "MUG-01",
      "price": { "amount": 12.0, "currency": "USD" },
      "compareAtPrice": null,
      "isPurchasable": true,
      "imageUrl": "/media/products/mug.jpg",
      "url": "/shop/contoso-mug"
    }
  ],
  "total": 1,
  "skip": 0,
  "take": 2
}
```

`GET products/4b2kx7m9qz1c` gives the full product: its images, prices, options and variants, add-ons, specifications, quantity rules and availability.

### 2. Add it to a cart

```http
POST cart/lines
Content-Type: application/json

{ "productId": "4b2kx7m9qz1c", "quantity": 2 }
```

The answer is the whole cart, priced now, with a new cart's token in a header:

```http
HTTP/1.1 200 OK
X-Cart-Token: CfDJ8N...
```

```json
{
  "id": "4c9vb1n2mx8k",
  "currency": "USD",
  "lines": [
    {
      "lineId": "a81f",
      "productId": "4b2kx7m9qz1c",
      "title": "Contoso mug",
      "quantity": 2,
      "unitPrice": { "amount": 12.0, "currency": "USD" },
      "lineTotal": { "amount": 24.0, "currency": "USD" },
      "isAvailable": true,
      "problem": null
    }
  ],
  "itemCount": 2,
  "subtotal": { "amount": 24.0, "currency": "USD" },
  "canCheckout": true
}
```

A product sold in variants needs `variantId`; a product with several prices takes `priceId`; add-ons go in `addOns`, by add-on id (the text typed, or `true` for a ticked box).

### 3. Start the checkout

```http
POST checkout
X-Cart-Token: CfDJ8N...
Idempotency-Key: 0b6e...
```

This places an order for what's in the cart, holds its stock, and starts the order's checkout. The answer is `201 Created`, with the guest's checkout token:

```http
HTTP/1.1 201 Created
X-Checkout-Token: CfDJ8P...
```

```json
{
  "id": "9f3c2a7d",
  "status": "pending",
  "orderId": "4d1k8s0p2w",
  "orderNumber": "1042",
  "currency": "USD",
  "steps": [
    { "key": "Order", "title": "Order", "isDone": true },
    { "key": "Contact", "title": "Contact", "isDone": false },
    { "key": "Shipping", "title": "Shipping", "isDone": false },
    { "key": "Payment", "title": "Payment", "isDone": false }
  ],
  "nextStep": "Contact",
  "totals": {
    "subtotal": { "amount": 24.0, "currency": "USD" },
    "shipping": { "amount": 0.0, "currency": "USD" },
    "discount": { "amount": 0.0, "currency": "USD" },
    "tax": { "amount": 0.0, "currency": "USD" },
    "total": { "amount": 24.0, "currency": "USD" },
    "tendered": { "amount": 0.0, "currency": "USD" },
    "amountDue": { "amount": 24.0, "currency": "USD" }
  }
}
```

Every checkout call answers with the checkout as it now stands, so your app always shows the current total. `nextStep` is the key of the step to do next, such as `Contact` or `Shipping`; it's `Payment` once only paying is left, and empty once the checkout is finished. The Payment step shows as done once the checkout is completed. `orderNumber` is the number the buyer knows the order by. Send `X-Checkout-Token: CfDJ8P...` on every call from here on.

### 4. Who is buying

```http
PUT checkout/9f3c2a7d/contact
Content-Type: application/json

{
  "name": "Avery Brooks",
  "email": "avery@example.com",
  "phone": "+1 555 0100",
  "billingAddress": {
    "name": "Avery Brooks",
    "addressLine1": "1 Main Street",
    "city": "Springfield",
    "region": "IL",
    "postalCode": "62701",
    "country": "US"
  }
}
```

An address has `name`, `company`, `addressLine1`, `addressLine2`, `dependentLocality`, `city`, `region`, `postalCode`, `sortingCode`, `country` (the two-letter code) and `phone`. Which fields a country needs follows its own address format. The billing address is needed only when your store asks for one.

### 5. Shipping

Ask for the ways to ship to the buyer's address:

```http
POST checkout/9f3c2a7d/shipping-rates
Content-Type: application/json

{ "address": { "addressLine1": "1 Main Street", "city": "Springfield", "region": "IL", "postalCode": "62701", "country": "US" } }
```

```json
[
  { "rateId": "standard", "name": "Standard", "description": "3 to 5 business days", "provider": "Flat rate", "amount": { "amount": 5.0, "currency": "USD" } },
  { "rateId": "express", "name": "Express", "description": "Next business day", "provider": "Flat rate", "amount": { "amount": 15.0, "currency": "USD" } }
]
```

Then save the buyer's choice:

```http
PUT checkout/9f3c2a7d/shipping
Content-Type: application/json

{ "address": { ... }, "rateId": "standard" }
```

With only one rate, `rateId` can be left out. With several and none chosen, the answer is a `400` on `rateId`.

To pick the order up instead, list the locations with `GET checkout/9f3c2a7d/pickup-locations`, and save `{ "pickupLocationId": "main-store" }`.

### 6. Codes and balances (optional)

```http
POST checkout/9f3c2a7d/codes
Content-Type: application/json

{ "code": "WELCOME10" }
```

A code that doesn't work is a `400` on `code`, with why. `DELETE checkout/9f3c2a7d/codes/WELCOME10` takes it off.

A gift card goes on with `POST checkout/9f3c2a7d/gift-cards` and `{ "code": "7KQM-4F2K-XN8D-P3VA" }`. A signed-in buyer can also put their store credit (`POST checkout/9f3c2a7d/store-credit`) or loyalty points (`POST checkout/9f3c2a7d/points`) toward the order. Each shows in the checkout's `tenders`, and lowers `totals.amountDue`. See [Gift cards](gift-cards.md) and [Store credit, loyalty points and referrals](store-credit-and-rewards.md).

### 7. The ways to pay

```http
GET checkout/9f3c2a7d/payment-methods
```

```json
[
  {
    "key": "Stripe",
    "title": "Credit or debit card (Stripe)",
    "description": null,
    "isDefault": true,
    "kind": "gateway",
    "client": {
      "publishableKey": "pk_test_...",
      "mode": "test",
      "amount": "2970",
      "currency": "usd"
    }
  },
  {
    "key": "bank-transfer",
    "title": "Bank transfer",
    "description": "...",
    "isDefault": false,
    "kind": "offline",
    "client": null
  }
]
```

These are the same methods the checkout page offers for this order. `kind` is `gateway` for a method paid now through a provider, and `offline` for one confirmed now and paid later, such as bank transfer or cash on delivery.

`client` holds what your app needs to collect the payment itself with the provider's own SDK. For Stripe: the `publishableKey` to start Stripe's SDK with, whether the account is in `test` or `live` mode, and the amount due in the currency's smallest unit (cents) with the lowercase currency, which Apple Pay and Google Pay sheets show. It never holds a secret key.

### 8. Pay

```http
POST checkout/9f3c2a7d/payment
Idempotency-Key: 3c51...
Content-Type: application/json

{ "paymentMethod": "Stripe" }
```

```json
{
  "requiresAction": true,
  "steps": [
    {
      "obligationId": "onetime",
      "attemptId": "4h7d...",
      "clientSecret": "pi_..._secret_...",
      "redirectUrl": null,
      "requiresAction": true
    }
  ]
}
```

Each step is one charge. For each, your app either confirms it with the provider's SDK using `clientSecret`, or sends the buyer to `redirectUrl`. For Stripe, confirm the PaymentIntent with the client secret: `stripe.confirmPayment` on the web, or the Payment Sheet in Stripe's iOS and Android SDKs. Stripe asks the buyer for 3D Secure when their bank wants it.

A provider that takes the buyer to its own page uses the request's `returnUrl` and `cancelUrl` to send them back. `providerData` passes values a provider's client SDK produced, when a provider needs them.

An `offline` method, such as bank transfer, has nothing to confirm: go straight to the next call.

If the total changed since the buyer saw it, the answer is a `409` with the code `invoice-changed`: get the checkout again, show the new total, and pay again.

### 9. Complete

```http
POST checkout/9f3c2a7d/complete
```

```json
{
  "status": "completed",
  "completed": true,
  "blockingStep": null,
  "error": null,
  "orderId": "4d1k8s0p2w",
  "orderNumber": "1042"
}
```

| `status` | What it means |
| --- | --- |
| `completed` | Paid, and the order is placed. Show the thank-you screen. |
| `alreadyCompleted` | It was completed before, such as by an earlier try. Treat it as `completed`. |
| `pending` | The provider hasn't confirmed the money yet. Ask again in a couple of seconds. |
| `blocked` | A step isn't done. `blockingStep` says which. |
| `failed` | The payment didn't go through. `error` says why. Let the buyer try another way. |
| `canceled` | The checkout was canceled. |

Show the buyer `orderNumber`: it's the number on their confirmation email, which they get the same as after the checkout page. Completing is safe to call again as often as you need: a checkout already completed answers `alreadyCompleted`.

`DELETE checkout/9f3c2a7d` cancels a checkout that isn't paid.

:::note[The store's own checkout fields]
If your store asks for its own fields on the Contact step (the **Checkout details** feature), the API has no fields for them. Collect them on the checkout page, or leave the feature off for a store sold through the API.
:::

## Endpoint reference

All paths start with `api/commerce/v1/`. **Guest** means anybody can call it, with or without a token. **Buyer** means a signed-in buyer's token. Staff endpoints name the permission.

### Catalog

| Method | Path | Who | What it does |
| --- | --- | --- | --- |
| `GET` | `products` | Guest | Lists the products the caller may see. Takes `q` (search), `type` (a product content type), `sort` (`newest`, `price-asc`, `price-desc`, `name` or `featured`; your listing's recommended order when not given), `skip` and `take`. |
| `GET` | `products/{id}` | Guest | Gets a product, priced for the caller, with its images, prices, options, variants, add-ons, specifications, quantity rules and availability. |
| `GET` | `products/{id}/availability` | Guest | Whether the product, or the variant given in `variantId`, is in stock at each of your locations. For a product sold in variants and no `variantId`, each location answers for all its variants together. Needs the **Inventory API**. |

Prices are what the caller pays, with sales and their role's discount included. Availability gives a count only when few are left, the way the product page says *Only 3 left*.

### Cart

| Method | Path | Who | What it does |
| --- | --- | --- | --- |
| `GET` | `cart` | Guest | Gets the caller's cart, priced now. A caller with none gets an empty cart, with no `id`. |
| `POST` | `cart/lines` | Guest | Adds a product (`productId`, and `variantId`, `priceId`, `quantity`, `addOns` as needed), creating the cart when there's none. |
| `PATCH` | `cart/lines/{lineId}` | Guest | Changes a line's `quantity`. Zero removes it. |
| `DELETE` | `cart/lines/{lineId}` | Guest | Removes a line. |
| `DELETE` | `cart` | Guest | Deletes the caller's cart. |
| `POST` | `cart/merge` | Buyer | Moves the guest cart named by `X-Cart-Token` into the signed-in buyer's cart. |

### Checkout

| Method | Path | Who | What it does |
| --- | --- | --- | --- |
| `POST` | `checkout` | Guest | Checks out the caller's cart: places the order, holds its stock and starts its checkout. Takes an idempotency key. Needs the **Orders API**. |
| `GET` | `checkout/{id}` | Guest | Gets the checkout: its steps, what's saved on them, the order and its number, and the invoice with what's left to pay. |
| `PUT` | `checkout/{id}/contact` | Guest | Saves the Contact step: `name`, `email`, `phone`, and `billingAddress` or a signed-in buyer's `billingAddressId`. Needs the **Customers API**. |
| `GET` | `checkout/{id}/pickup-locations` | Guest | Lists the locations the order can be picked up from, and what picking up costs. Needs the **Shipping API**. |
| `POST` | `checkout/{id}/shipping-rates` | Guest | Lists the ways to ship to an `address` (or a signed-in buyer's `addressId`), with what each costs. Needs the **Shipping API**. |
| `PUT` | `checkout/{id}/shipping` | Guest | Saves the Shipping step: a `pickupLocationId`, or an address and its `rateId`. Needs the **Shipping API**. |
| `POST` | `checkout/{id}/codes` | Guest | Puts a discount code on the checkout. |
| `DELETE` | `checkout/{id}/codes/{code}` | Guest | Takes a discount code off. |
| `POST` | `checkout/{id}/gift-cards` | Guest | Puts a gift card toward the checkout, by its `code`. Needs the **Orders API** and **Gift Cards**. |
| `DELETE` | `checkout/{id}/gift-cards/{giftCardId}` | Guest | Takes a gift card off. Its id is the `reference` of its tender. |
| `POST` | `checkout/{id}/store-credit` | Buyer | Puts the buyer's store credit toward the checkout. Needs the **Customers API** and **Store Credit**. |
| `DELETE` | `checkout/{id}/store-credit` | Buyer | Takes the store credit off. |
| `POST` | `checkout/{id}/points` | Buyer | Spends the buyer's loyalty points on the checkout. Needs the **Customers API** and **Loyalty Points**. |
| `DELETE` | `checkout/{id}/points` | Buyer | Takes the points off. |
| `GET` | `checkout/{id}/payment-methods` | Guest | Lists the ways to pay, with what a client needs to collect each payment itself. |
| `POST` | `checkout/{id}/payment` | Guest | Begins paying what's due with a `paymentMethod`. Takes an idempotency key. |
| `POST` | `checkout/{id}/complete` | Guest | Completes the checkout once it's paid, placing the order, and gives the order's number. Ask again while it says `pending`; it's safe to call again. |
| `DELETE` | `checkout/{id}` | Guest | Cancels a checkout that isn't paid. |

A guest sends `X-Checkout-Token` on every checkout call except `POST checkout`, which takes `X-Cart-Token`.

### The buyer's account

| Method | Path | Who | What it does |
| --- | --- | --- | --- |
| `GET` | `me` | Buyer | Gets the buyer's customer record: their details and addresses. Needs the **Customers API**. |
| `PUT` | `me` | Buyer | Changes the buyer's details: `kind` (`person` or `business`), `displayName`, `firstName`, `lastName`, `companyName`, `taxId`, `phone`, `acceptsMarketing`. Send them all: a field left out is cleared. The email belongs to their user account and isn't changed here. |
| `POST` | `me/addresses` | Buyer | Adds an address (`label`, `address`, `isDefaultBilling`, `isDefaultShipping`). |
| `PUT` | `me/addresses/{addressId}` | Buyer | Changes an address. |
| `DELETE` | `me/addresses/{addressId}` | Buyer | Deletes an address. |
| `GET` | `me/orders` | Buyer | Lists the buyer's placed orders, newest first, in pages. Needs the **Orders API**. |
| `GET` | `me/orders/{orderId}` | Buyer | Gets one of the buyer's orders, or an order the store sent them to pay, with a `payUrl` while money is still owed on it. |
| `GET` | `me/orders/{orderId}/returnable` | Buyer | What can still be returned from a paid order, the reasons to choose from, whether exchanges are allowed, and the return window. Needs **Returns**. |
| `GET` | `me/orders/{orderId}/returns` | Buyer | Lists the returns asked for on the order. |
| `POST` | `me/orders/{orderId}/returns` | Buyer | Asks for a return: `lines` (each `lineId`, `quantity`, `reason`), `resolution` (`refund` or `exchange`), `exchangeFor` and `comment`. Takes an idempotency key. |
| `POST` | `me/orders/{orderId}/returns/{returnId}/withdraw` | Buyer | Withdraws a return the store hasn't received yet. |
| `GET` | `me/orders/{orderId}/shipments` | Buyer | Lists the shipments of a paid order, with their tracking. Needs the **Shipping API**. |

Another buyer's order answers `404`. See [Returns and exchanges](returns.md).

### Staff: orders

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `orders` | **Manage orders** | Lists orders, newest first. Takes `status` (one or more of `draft`, `awaitingPayment`, `paid`, `canceled`, separated by commas), `channel` (`online`, `pos` or `draft`), `locationId` (the store a register sale was made at), `q` (order number, name or email), `customerId`, `from`, `to`, `skip` and `take`. |
| `GET` | `orders/{orderId}` | **Manage orders** | Gets an order, with what can still be refunded, what's still owed, whether it can be refunded as store credit, and its notes. |
| `GET` | `orders/by-number/{number}` | **Manage orders** | Gets an order by the number the buyer was given. |
| `POST` | `orders/{orderId}/refunds` | **Manage orders** | Refunds an order: everything refundable, or an `amount`, with a `reason`; `restock` puts its stock back; `asStoreCredit` gives it as store credit. Takes an idempotency key. |
| `POST` | `orders/{orderId}/cancel` | **Manage orders** | Cancels an order, with a `note`; `refund` gives back what was paid; `restock` puts its stock back. Takes an idempotency key. |

Every order, here and under `me/orders`, says where it was made in `channel`: `online` (the storefront, a headless storefront or an app), `pos` (a store's register) or `draft` (made by staff and sent to the buyer to pay). A register sale also has its store's `locationId`, and the order has the `register` it was rung up on.

A refund or a cancel answers with the amount given back, what happened (`messages`), and the order as it now is. See [Carts and orders](carts-and-orders.md).

### Staff: fulfillment

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `orders/{orderId}/shipments` | **Ship orders and update their shipments** | Lists the order's shipments, and how many of each line are still to ship. |
| `POST` | `orders/{orderId}/shipments` | **Ship orders and update their shipments** | Packs lines of a paid order into a shipment: `lines` (each `lineId` and `quantity`; none packs everything left), `ship` (shipped now, the default, or kept to ship later), `carrier`, `trackingNumber`, `trackingUrl`, `note`. Takes an idempotency key. |
| `POST` | `shipments/{shipmentId}/ship` | **Ship orders and update their shipments** | Marks a kept shipment shipped, with its carrier and tracking. The buyer is emailed. |
| `POST` | `shipments/{shipmentId}/deliver` | **Ship orders and update their shipments** | Marks a shipment delivered. |

These need the **Shipping API**. See [Shipping](shipping.md).

### Staff: customers

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `customers` | **View customers** | Lists active customers. Takes `q` (name, email or phone), `kind` (`person` or `business`), `hasAccount`, `skip` and `take`. |
| `GET` | `customers/{customerId}` | **View customers** | Gets a customer with their addresses. |

### Staff: stock

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `inventory` | **View inventory** | Lists the stock you count. Takes `q` (SKU or name), `low=true` for only what's running low, `skip` and `take`. |
| `GET` | `inventory/{stockItemId}` | **View inventory** | Gets one stock item, with what's on hand, held and available at each location. |
| `POST` | `inventory/{stockItemId}/receive` | **Manage inventory** | Adds units received at a location: `locationId`, `quantity`, `reason`. Takes an idempotency key. |
| `POST` | `inventory/{stockItemId}/adjust` | **Manage inventory** | Changes what's on hand at a location by a `quantity`, up or down, with the `reason`. Takes an idempotency key. |
| `PUT` | `inventory/{stockItemId}/count` | **Manage inventory** | Sets what's on hand at a location to what was counted: `locationId`, `count`, `reason`. |
| `POST` | `inventory/{stockItemId}/transfer` | **Manage inventory** | Moves a `quantity` from `fromLocationId` to `toLocationId`. Takes an idempotency key. |

A change that leaves out `locationId` applies to your default location; an id that isn't one of your locations is a `400` on that field (`locationId`, `fromLocationId` or `toLocationId`). A change that collides with a checkout holding the same stock answers `409` with the code `concurrency`: read it again and retry. See [Inventory](inventory.md).

### Staff: coupons

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `coupons` | **Manage coupons** | Lists coupons and automatic discounts, newest first. Takes `q` (code or description), `skip` and `take`. |
| `GET` | `coupons/{couponId}` | **Manage coupons** | Gets a coupon. |
| `POST` | `coupons` | **Manage coupons** | Creates a coupon or an automatic discount, held to the same rules as the coupon's admin page. Takes an idempotency key. |
| `PUT` | `coupons/{couponId}` | **Manage coupons** | Changes a coupon. Send every field: one left out is cleared, as when the coupon's form is saved. |
| `DELETE` | `coupons/{couponId}` | **Manage coupons** | Deletes a coupon and its generated codes. Orders that used it keep their discount. |

A coupon's `kind` is `percentage`, `fixedAmount`, `freeShipping` or `buyXGetY`, and its `method` is `code` or `automatic`; anything else is a `400`. A coupon comes back with every field it takes, `sources` and `referenceTypes` included, so you can read one, change a field, and send it all back. See [Coupons](coupons.md).

### Point of sale

With the **Point of Sale** feature on, a till app rings up sales at your stores' registers. Every call needs a staff member's token with **Sell at a register**.

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `pos/registers` | **Sell at a register** | Lists the registers the caller may sell at, each with its open shift. |
| `POST` | `pos/registers/{registerId}/shifts` | **Sell at a register** | Opens a shift with its `openingFloat`. |
| `GET` | `pos/shifts/{shiftId}` | **Sell at a register** | Gets a shift, with its sales and the cash its drawer should hold. |
| `POST` | `pos/shifts/{shiftId}/cash` | **Sell at a register** | Records cash put in or taken out. |
| `POST` | `pos/shifts/{shiftId}/close` | **Sell at a register** | Closes a shift with the `countedCash`. |
| `POST` | `pos/sales` | **Sell at a register** | Rings up products at a register. |
| `POST` | `pos/sales/{checkoutId}/pay` | **Sell at a register** | Pays a sale in cash, by card or on a Stripe reader. |
| `GET` | `pos/sales/{orderId}/receipt` | **Sell at a register** | Gets the receipt as HTML. |
| `POST` | `pos/sales/{orderId}/receipt/email` | **Sell at a register** | Emails the receipt. |
| `POST` | `pos/returns` | **Sell at a register** | Refunds a sale at the till. |
| `GET` | `pos/terminal/readers` | **Sell at a register** | Lists the Stripe Terminal readers. |
| `POST` | `pos/terminal/readers` | **Manage registers and shifts** | Pairs a Stripe Terminal reader. |

See [Point of sale](point-of-sale.md) for a shift call by call, and every field.

## Generate a typed client

The OpenAPI description at `/openapi/v1.json` lists every endpoint with its fields, so you can generate a client for your app instead of writing the calls by hand. Each endpoint has a stable operation name, such as `StartCheckout`, `BeginCheckoutPayment` or `CompleteCheckout`, which becomes the method's name.

1. Save the description: open `/openapi/v1.json` while signed in as a user with **View OpenAPI Content**, and save it as *commerce.json*. (Or allow anonymous access to it for a moment, as above.)
2. Run a generator on it. With [NSwag](https://github.com/RicoSuter/NSwag):

```bash
# TypeScript, for a storefront or a React Native app
nswag openapi2tsclient /input:commerce.json /output:src/commerce-client.ts

# C#, for a .NET point of sale or back-office service
nswag openapi2csclient /input:commerce.json /output:CommerceClient.cs /namespace:Contoso.Commerce
```

Any OpenAPI generator works the same way, such as OpenAPI Generator for Swift and Kotlin apps. Regenerate the client when you turn on another API feature or update the site.

The generated client sends what you give it, so add the headers your app keeps: `Authorization`, the guest's `X-Cart-Token` and `X-Checkout-Token`, and an `Idempotency-Key` on the calls that take one.

## Related pages

- [Webhooks](webhooks.md)
- [Point of sale](point-of-sale.md)
- [Checkout](checkout.md)
- [Carts and orders](carts-and-orders.md)
- [Payment providers](payment-providers.md)
- [Customers](customers.md)
- [Shipping](shipping.md)
- [Inventory](inventory.md)
