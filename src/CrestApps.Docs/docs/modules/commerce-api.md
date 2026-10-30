---
sidebar_label: Commerce API
title: Commerce API
description: The commerce JSON API at api/commerce/v1 that headless storefronts, phone apps, points of sale and back-office systems call, with its features, OpenAPI description, bearer-token sign-in, CORS, shared conventions, problems and conflict codes, idempotency keys, guest cart and checkout tokens, a guest checkout call by call, and every endpoint.
user_manual: user-manual/commerce/headless-api
---

| | |
| --- | --- |
| **Base path** | `api/commerce/v1` (on a tenant with a URL prefix, such as *shop*: `shop/api/commerce/v1`) |
| **Format** | JSON over HTTPS, `camelCase` names, problems as RFC 9457 |
| **Authentication** | None for guest endpoints; otherwise an OpenID Connect bearer token from the site's own authorization server (the `Api` authentication scheme). The site's sign-in cookie is never used. |
| **Explorer** | Orchard Core's OpenAPI feature, at `/openapi/v1.json` |

The **commerce API** lets software other than the site's own pages sell from the store: a headless storefront, a phone app, a [point of sale](point-of-sale.md) or a back-office integration. It runs on the same services as the storefront (products, prices, carts, `ICheckoutEngine`, stock and orders), so a coupon, a tax rate or a stock hold behaves the same whether the buyer is on the site or in an app.

Each commerce module maps its own endpoints into the shared route group, from its own **API** feature. `CrestApps.OrchardCore.Commerce.Api.Core` holds what they share; see [Checkout](checkout.md#the-commerce-api).

To have the store push events to another system instead of being polled, use [Commerce Webhooks](commerce-webhooks.md).

<YouTube video="headless-api" title="Narrated video overview of the headless commerce API setup" />

## Features

| Feature | Name | It needs | Maps |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Products.Api` | Catalog API | Product Listing | `products`, `products/{id}` ([Products](products.md#api)) |
| `CrestApps.OrchardCore.Carts.Api` | Cart API | Carts | `cart`, `cart/lines`, `cart/merge` ([Carts and Orders](carts-and-orders.md#api)) |
| `CrestApps.OrchardCore.Checkout.Api` | Checkout API | Checkout | `checkout/{id}`, its codes, payment methods, payment, completion and cancel; staff `coupons` ([Checkout](checkout.md#the-commerce-api)) |
| `CrestApps.OrchardCore.Orders.Api` | Orders API | Orders, Cart API, Checkout API | `POST checkout`, `checkout/{id}/gift-cards` (with Gift Cards), `me/orders` and its returns (with Returns), staff `orders` ([Carts and Orders](carts-and-orders.md#api)) |
| `CrestApps.OrchardCore.Customers.Api` | Customers API | Customers | `me`, `me/addresses`, staff `customers`; with Orders and the Checkout API, `checkout/{id}/contact`; with Store Credit or Loyalty Points and the Checkout API, `checkout/{id}/store-credit` and `checkout/{id}/points` ([Customers](customers.md#api)) |
| `CrestApps.OrchardCore.Shipping.Api` | Shipping API | Shipping, Checkout API | `checkout/{id}/pickup-locations`, `shipping-rates`, `shipping`; `me/orders/{orderId}/shipments`; staff fulfillment ([Shipping](shipping.md#api)) |
| `CrestApps.OrchardCore.Inventory.Api` | Inventory API | Inventory | `products/{id}/availability`; staff `inventory` ([Inventory](inventory.md#api)) |
| `CrestApps.OrchardCore.PointOfSale` | Point of Sale | Orders, Inventory, Checkout API | `pos/...` ([Point of Sale](point-of-sale.md#api)) |

A typical headless store turns on the first seven.

## OpenAPI description

With Orchard Core's **OpenAPI** feature (`OrchardCore.OpenApi`) on, every endpoint is described with its parameters, its request and response fields, and its errors, at `/openapi/v1.json` (with the tenant's prefix first, if it has one). The explorers are separate features: **Swagger UI** (`OrchardCore.OpenApi.SwaggerUI`) at `/swagger`, **Scalar UI** (`OrchardCore.OpenApi.ScalarUI`) at `/scalar/v1`, and **ReDoc UI** (`OrchardCore.OpenApi.ReDocUI`, read-only) at `/redoc`.

By default only a signed-in user with **View OpenAPI Content** can read the description. The OpenAPI settings' **Allow anonymous access to the API schema** opens it to everyone.

The commerce endpoints are tagged **Catalog**, **Cart**, **Checkout**, **Inventory**, **Account**, and for staff **Orders**, **Customers**, **Coupons**, **Stock**, **Fulfillment** and **Point of Sale**. Each has a stable operation name, such as `StartCheckout`, `BeginCheckoutPayment` or `CompleteCheckout`.

The **OpenAPI Documentation — Bearer/PKCE** recipe sets up sign-in for the Swagger and Scalar pages. It creates an **openapi** OpenID application whose redirect addresses use `https://localhost:5001`; change them to the site's address. It points token validation at the *Default* tenant; on another tenant, change it to that tenant's name.

### Generate a typed client

Generate a client from the description instead of writing the calls by hand. Save `/openapi/v1.json` as *commerce.json* (signed in with **View OpenAPI Content**, or with anonymous access allowed for a moment), then run a generator on it. With [NSwag](https://github.com/RicoSuter/NSwag):

```bash
# TypeScript, for a storefront or a React Native app
nswag openapi2tsclient /input:commerce.json /output:src/commerce-client.ts

# C#, for a .NET point of sale or back-office service
nswag openapi2csclient /input:commerce.json /output:CommerceClient.cs /namespace:Contoso.Commerce
```

Any OpenAPI generator works the same way, such as OpenAPI Generator for Swift and Kotlin apps. Regenerate the client when another API feature is turned on or the site is updated. The generated client sends what it's given, so add the headers the app keeps: `Authorization`, the guest's `X-Cart-Token` and `X-Checkout-Token`, and an `Idempotency-Key` on the calls that take one.

## Authentication

Guest endpoints need no token. Everything else needs a **bearer token** from the site's own OpenID server, sent on every call:

```http
Authorization: Bearer eyJhbGciOi...
```

The site needs Orchard Core's **OpenID Authorization Server** (`OrchardCore.OpenId.Server`), **OpenID Token Validation** (`OrchardCore.OpenId.Validation`) and **OpenID Management Interface** (`OrchardCore.OpenId.Management`). The authorization server issues tokens at `/connect/token` and signs people in at `/connect/authorize`. Token validation must name **this tenant** as the one whose tokens it accepts; without it, every call with a token is refused with `401`.

The [User Manual](../user-manual/commerce/headless-api.md#set-up-sign-in) walks through the admin screens.

### Apps people sign in to

A storefront, a phone app or a point of sale signs the person in with the **authorization code flow and PKCE**, as a **public** client: the app never sees the password and holds no secret. Allow the scopes `openid`, `profile` and `roles` (the `roles` claim is what staff permissions are evaluated from), and `offline_access` for refresh tokens. The redirect URI is the app's callback, such as `https://shop.contoso.com/signin-callback` for a website or `com.contoso.shop:/signin-callback` for a phone app. Any OpenID Connect client library works, such as AppAuth on iOS and Android, or oidc-client-ts in a browser.

Signed-in buyers get what the **Authenticated** role allows, such as **View own orders**. Staff get what their roles allow.

### Servers

A back-office integration uses the **client credentials flow** as a **confidential** client with a client secret. The application's **Client Credentials Roles** decide what it may do, so give it a role with only the permissions it needs. The server asks for a token, and sends it on each call until it expires:

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id=contoso-warehouse&client_secret=YOUR-CLIENT-SECRET
```

Keep the secret in the server's secret store, never in an app or a web page.

### Permissions

| Caller | Endpoints | Permission |
| --- | --- | --- |
| A guest, no token | Catalog, availability, cart, checkout and its steps (except store credit and points) | None |
| A signed-in buyer | Everything a guest can do, plus `me` (their account and addresses), their orders, returns and shipments, store credit and points at checkout, and `cart/merge` | **View own orders** for orders, returns and shipments; **Manage own customer profile and addresses** for their account. Both are given to the **Authenticated** role by default. |
| Staff or a server | The staff endpoints | The same permissions as the matching admin pages: **Manage orders**, **View customers**, **View inventory**, **Manage inventory**, **Manage coupons**, **Ship orders and update their shipments** |

A call without a token to an endpoint that needs one answers `401`. A token whose user or application lacks the permission answers `403`.

### CORS

A storefront in the browser on another origin than the site, such as `https://shop.contoso.com` calling `https://admin.contoso.com`, needs Orchard Core's **CORS** feature (`OrchardCore.Cors`) with a policy:

| Setting | Value |
| --- | --- |
| **Allowed origins** | The storefront's origin, such as `https://shop.contoso.com`. |
| **Allowed methods** | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| **Allowed headers** | `Authorization`, `Content-Type`, `X-Cart-Token`, `X-Checkout-Token`, `Idempotency-Key` |
| **Exposed headers** | `X-Cart-Token`, `X-Checkout-Token`, `Location`. Without these, the browser hides the guest's tokens from the app's code. |
| **Allow credentials** | Off. The API doesn't use cookies. |

A phone app, a point of sale or a server doesn't need CORS.

## Conventions

### JSON, names and dates

Requests and responses are JSON, with `camelCase` names, such as `productId` and `billingAddress`. Values that name a state are `camelCase` strings too, such as `awaitingPayment` or `lowStock`. Dates are UTC, in ISO 8601, in fields that end in `Utc`, such as `createdUtc`.

### Money

Every amount is an `ApiMoney`: the amount in major units (dollars, not cents), rounded to the currency's precision, and the three-letter currency code.

```json
{ "amount": 49.99, "currency": "USD" }
```

### Markets

With the [Markets](markets.md) feature on, a store sells to groups of countries in their own currencies. An app says which market the buyer is in on **every** call, the catalog, the cart and the checkout alike, with either:

- the `market` query value: `GET products?market=DE`, or
- the `X-Market` header: `X-Market: DE`.

The value is a market's id or a two-letter country code that a market covers. Prices, compare-at prices, variant and add-on prices, the cart, shipping rates and the checkout are then in the market's currency, converted at its rate and rounded by its rule unless the product lists its own price in that currency. A product left out of the market has `isPurchasable: false`, and adding it to the cart answers `409` with the code `not-available`.

API calls are never placed in a market by a browser cookie or language: without `market` or `X-Market`, prices are in the products' own currencies. A cart that moves to another market's currency is priced again in it.

```http
GET products/4b2kx7m9qz1c
X-Market: DE
```

```json
{
  "id": "4b2kx7m9qz1c",
  "price": { "amount": 11.99, "currency": "EUR" },
  "isPurchasable": true
}
```

### Pages of results

A list that can be long is an `ApiPage<T>`. Ask for a page with `skip` (how many to pass over, from 0) and `take` (how many to return, 20 when not given, at most 100). The answer says how many match in all:

```json
{
  "items": [ ... ],
  "total": 134,
  "skip": 40,
  "take": 20
}
```

### Problems

An error is a **problem** (RFC 9457), a JSON object with the HTTP `status`, a `title` and usually a `detail` an app can show:

| Status | What it means |
| --- | --- |
| `400` | Something in the request isn't right. `errors` lists the problems by field name, such as `email` or `billingAddress.city`, so an app can show each next to its field. |
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

| `code` | What happened | What the app does |
| --- | --- | --- |
| `empty-cart` | The cart is empty, so there's nothing to check out. | Send the buyer back to shopping. |
| `out-of-stock` | Something ran out, or a location hasn't that many. | Show `detail`, and let the buyer change the cart. |
| `not-available` | The change isn't possible now, such as a product no longer sold, an order that can't be refunded, or a return already received. | Show `detail`. |
| `invoice-changed` | The total changed since the buyer last saw it. | Get the checkout again, show the new total, and ask the buyer to pay again. |
| `concurrency` | Somebody else changed the same thing at the same moment. | Read it again, and retry. |
| `unsupported` | This checkout doesn't take that, such as a code on a checkout that refuses discounts, or a gift card on a subscription. | Hide that option. |
| `idempotency-in-progress` | A request with the same idempotency key is still running. | Wait a moment, and send it again. |

The point of sale adds `shift-open` and `shift-closed`; see [Point of Sale](point-of-sale.md#api).

### Idempotency keys

A phone loses its signal just as it sends **Pay**. To make it safe to send the request again, send an `Idempotency-Key` header with a value made up for that action, such as a new GUID, at most 128 characters:

```http
POST /api/commerce/v1/checkout/9f3c.../payment
Idempotency-Key: 6a1f4b8e-2d7c-4f0a-9b3e-5c8d1e2f7a90
```

The same request with the same key within 24 hours gets the first answer back, and nothing is done twice: no second order, payment, refund or shipment. The answer comes back whole, with the `X-Cart-Token`, `X-Checkout-Token` and `Location` headers the first one had, so a guest whose first try was lost still gets their checkout token. Keys are kept per caller and per endpoint, so two buyers can't collide.

- Make a **new key for each new action**, and reuse a key only to send the very same request again.
- The endpoints that take a key (`WithIdempotency`) are those that place, pay, refund or move something: starting a checkout, beginning a payment, asking for a return, refunding and canceling an order, packing a shipment, creating a coupon, receiving, adjusting and moving stock, and the point of sale's shifts, cash movements, sales, payments and returns. Other endpoints ignore the header. Completing a checkout needs no key: it's safe to call again as it is.
- Only a final answer is kept, an error such as a `400` too. After changing a request to fix it, send it with a new key.
- An answer that may turn out differently a moment later isn't kept, so the same key tries the work again: a server error (`500` and up), `429`, and a `409` with the code `concurrency` or `idempotency-in-progress`.

### Guest cart and checkout tokens

A guest has no account, so their cart and checkout are known by a **token** in a header, never by a cookie:

1. The answer that creates a guest's cart has an `X-Cart-Token` header. Send it as `X-Cart-Token` on every cart call and when starting the checkout.
2. The answer that starts a guest's checkout has an `X-Checkout-Token` header. Send it as `X-Checkout-Token` on every call about that checkout.

Whoever has a token can see and change that cart or checkout, so keep it in the app's secure storage, not in a link.

When a guest signs in, `POST cart/merge` with their new bearer token **and** the guest's `X-Cart-Token` moves the guest cart's lines into their own cart.

## A guest checkout, call by call

A whole purchase, as a storefront makes it. Paths are shown without `https://your-site/api/commerce/v1/`, and the answers leave out some fields; the OpenAPI description lists them all.

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

Every checkout call answers with the checkout as it now stands (`ApiCheckout`), so the app always shows the current total. `nextStep` is the key of the step to do next, such as `Contact` or `Shipping`; it's `Payment` once only paying is left, and empty once the checkout is finished. The Payment step shows as done once the checkout is completed. `orderNumber` is the number the buyer knows the order by. Send `X-Checkout-Token: CfDJ8P...` on every call from here on.

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

An address (`ApiAddress`) has `name`, `company`, `addressLine1`, `addressLine2`, `dependentLocality`, `city`, `region`, `postalCode`, `sortingCode`, `country` (the two-letter code) and `phone`. Which fields a country needs follows its own address format. The billing address is needed only when the store asks for one.

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

A gift card goes on with `POST checkout/9f3c2a7d/gift-cards` and `{ "code": "7KQM-4F2K-XN8D-P3VA" }`. A signed-in buyer can also put their store credit (`POST checkout/9f3c2a7d/store-credit`) or loyalty points (`POST checkout/9f3c2a7d/points`) toward the order. Each shows in the checkout's `tenders`, and lowers `totals.amountDue`.

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

`client` holds what the app needs to collect the payment itself with the provider's own SDK, from the provider's `ICheckoutPaymentClientConfigurationProvider`. For Stripe: the `publishableKey` to start Stripe's SDK with, whether the account is in `test` or `live` mode, and the amount due in the currency's smallest unit (cents) with the lowercase currency, which Apple Pay and Google Pay sheets show. It never holds a secret key.

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

Each step is one charge. For each, the app either confirms it with the provider's SDK using `clientSecret`, or sends the buyer to `redirectUrl`. For Stripe, confirm the PaymentIntent with the client secret: `stripe.confirmPayment` on the web, or the Payment Sheet in Stripe's iOS and Android SDKs. Stripe asks the buyer for 3D Secure when their bank wants it.

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

Show the buyer `orderNumber`: it's the number on their confirmation email, which they get the same as after the checkout page. Completing is safe to call again as often as needed: a checkout already completed answers `alreadyCompleted`.

`DELETE checkout/9f3c2a7d` cancels a checkout that isn't paid.

:::note[The store's own checkout fields]
The API has no fields for the store's own Contact-step fields (the **Checkout details** feature). Collect them on the checkout page, or leave the feature off for a store sold through the API.
:::

## Endpoint reference

All paths start with `api/commerce/v1/`. **Guest** means anybody can call it, with or without a token. **Buyer** means a signed-in buyer's token. Staff endpoints name the permission.

### Catalog

| Method | Path | Who | What it does |
| --- | --- | --- | --- |
| `GET` | `products` | Guest | Lists the products the caller may see. Takes `q` (search), `type` (a product content type), `sort` (`newest`, `price-asc`, `price-desc`, `name` or `featured`; the listing's recommended order when not given), `skip` and `take`. |
| `GET` | `products/{id}` | Guest | Gets a product, priced for the caller, with its images, prices, options, variants, add-ons, specifications, quantity rules and availability. |
| `GET` | `products/{id}/availability` | Guest | Whether the product, or the variant given in `variantId`, is in stock at each location. For a product sold in variants and no `variantId`, each location answers for all its variants together. Needs the **Inventory API**. |

Prices are what the caller pays, with sales and their role's discount included, in the caller's [market](#markets) when it names one. `prices` lists each of the product's prices as the product lists it, in its own currency; `price` is the one the caller pays. Availability gives a count only when few are left, the way the product page says *Only 3 left*.

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

Another buyer's order answers `404`.

### Staff: orders

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `orders` | **Manage orders** | Lists orders, newest first. Takes `status` (one or more of `draft`, `awaitingPayment`, `paid`, `canceled`, separated by commas), `channel` (`online`, `pos` or `draft`), `locationId` (the store a register sale was made at), `q` (order number, name or email), `customerId`, `from`, `to`, `skip` and `take`. |
| `GET` | `orders/{orderId}` | **Manage orders** | Gets an order, with what can still be refunded, what's still owed, whether it can be refunded as store credit, and its notes. |
| `GET` | `orders/by-number/{number}` | **Manage orders** | Gets an order by the number the buyer was given. |
| `POST` | `orders/{orderId}/refunds` | **Manage orders** | Refunds an order: everything refundable, or an `amount`, with a `reason`; `restock` puts its stock back; `asStoreCredit` gives it as store credit. Takes an idempotency key. |
| `POST` | `orders/{orderId}/cancel` | **Manage orders** | Cancels an order, with a `note`; `refund` gives back what was paid; `restock` puts its stock back. Takes an idempotency key. |

Every order, here and under `me/orders`, says where it was made in `channel`: `online` (the storefront, a headless storefront or an app), `pos` (a store's register) or `draft` (made by staff and sent to the buyer to pay). A register sale also has its store's `locationId`, and the order has the `register` it was rung up on.

An order also has the `culture` the buyer shopped in, such as `de-DE`, which its emails are written in, and its `taxes`: each tax it was charged, once per `name`, `type`, `jurisdiction`, `rate` and `includedInPrice`, with what it was charged on (`taxable`) and the `amount`.

```json
"culture": "de-DE",
"taxes": [
  {
    "name": "VAT",
    "type": "VAT",
    "jurisdiction": "Germany",
    "rate": 0.19,
    "taxable": { "amount": 20.16, "currency": "EUR" },
    "amount": { "amount": 3.83, "currency": "EUR" },
    "includedInPrice": true
  }
]
```

A refund or a cancel answers with the amount given back, what happened (`messages`), and the order as it now is.

### Staff: fulfillment

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `orders/{orderId}/shipments` | **Ship orders and update their shipments** | Lists the order's shipments, and how many of each line are still to ship. |
| `POST` | `orders/{orderId}/shipments` | **Ship orders and update their shipments** | Packs lines of a paid order into a shipment: `lines` (each `lineId` and `quantity`; none packs everything left), `ship` (shipped now, the default, or kept to ship later), `carrier`, `trackingNumber`, `trackingUrl`, `note`. Takes an idempotency key. |
| `POST` | `shipments/{shipmentId}/ship` | **Ship orders and update their shipments** | Marks a kept shipment shipped, with its carrier and tracking. The buyer is emailed. |
| `POST` | `shipments/{shipmentId}/deliver` | **Ship orders and update their shipments** | Marks a shipment delivered. |

These need the **Shipping API**.

### Staff: customers

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `customers` | **View customers** | Lists active customers. Takes `q` (name, email or phone), `kind` (`person` or `business`), `hasAccount`, `skip` and `take`. |
| `GET` | `customers/{customerId}` | **View customers** | Gets a customer with their addresses. |

### Staff: stock

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `inventory` | **View inventory** | Lists the stock the store counts. Takes `q` (SKU or name), `low=true` for only what's running low, `skip` and `take`. |
| `GET` | `inventory/{stockItemId}` | **View inventory** | Gets one stock item, with what's on hand, held and available at each location. |
| `POST` | `inventory/{stockItemId}/receive` | **Manage inventory** | Adds units received at a location: `locationId`, `quantity`, `reason`. Takes an idempotency key. |
| `POST` | `inventory/{stockItemId}/adjust` | **Manage inventory** | Changes what's on hand at a location by a `quantity`, up or down, with the `reason`. Takes an idempotency key. |
| `PUT` | `inventory/{stockItemId}/count` | **Manage inventory** | Sets what's on hand at a location to what was counted: `locationId`, `count`, `reason`. |
| `POST` | `inventory/{stockItemId}/transfer` | **Manage inventory** | Moves a `quantity` from `fromLocationId` to `toLocationId`. Takes an idempotency key. |

A change that leaves out `locationId` applies to the default location; an id that isn't one of the store's locations is a `400` on that field (`locationId`, `fromLocationId` or `toLocationId`). A change that collides with a checkout holding the same stock answers `409` with the code `concurrency`: read it again and retry.

### Staff: coupons

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `coupons` | **Manage coupons** | Lists coupons and automatic discounts, newest first. Takes `q` (code or description), `skip` and `take`. |
| `GET` | `coupons/{couponId}` | **Manage coupons** | Gets a coupon. |
| `POST` | `coupons` | **Manage coupons** | Creates a coupon or an automatic discount, held to the same rules as the coupon's admin page. Takes an idempotency key. |
| `PUT` | `coupons/{couponId}` | **Manage coupons** | Changes a coupon. Send every field: one left out is cleared, as when the coupon's form is saved. |
| `DELETE` | `coupons/{couponId}` | **Manage coupons** | Deletes a coupon and its generated codes. Orders that used it keep their discount. |

A coupon's `kind` is `percentage`, `fixedAmount`, `freeShipping` or `buyXGetY`, and its `method` is `code` or `automatic`; anything else is a `400`. A coupon comes back with every field it takes, `sources` and `referenceTypes` included, so a client can read one, change a field, and send it all back.

### Point of sale

With the **Point of Sale** feature on, a till app rings up sales at the stores' registers under `pos/`. Every call needs a staff member's token with **Sell at a register**; pairing a Stripe reader needs **Manage registers and shifts**. See [Point of Sale](point-of-sale.md#api) for the endpoints and [a shift, call by call](point-of-sale.md#a-shift-call-by-call).

## Related modules

- [Checkout](checkout.md#the-commerce-api): the shared API core, and the checkout endpoints.
- [Products](products.md#api), [Carts and Orders](carts-and-orders.md#api), [Customers](customers.md#api), [Shipping](shipping.md#api), [Inventory](inventory.md#api): what each API feature maps.
- [Point of Sale](point-of-sale.md): the till app's endpoints.
- [Commerce Webhooks](commerce-webhooks.md): events pushed to other systems, in the API's shapes.
