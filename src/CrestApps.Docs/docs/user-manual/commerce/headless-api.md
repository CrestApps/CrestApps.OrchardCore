---
sidebar_label: Headless API
title: Headless commerce API
description: Run your store from your own storefront, a phone app, a point of sale or a back-office system over the commerce API, with the features to turn on, how to let apps and servers sign in, where developers find the API's documentation, and what the API can and can't do.
technical_manual:
  - modules/commerce-api
  - modules/products
  - modules/carts-and-orders
  - modules/checkout
  - modules/customers
  - modules/inventory
  - modules/shipping
---

The **commerce API** lets software other than your site's own pages sell from your store. It uses the same products, prices, carts, checkout, stock and orders as your storefront, with the same rules: a coupon, a tax rate or a stock hold works the same whether the buyer is on your site or in an app.

Use it to build:

- **A headless storefront**: your own website, in any framework, that shows your products and checks buyers out.
- **A phone app** your buyers sign in to, to shop, pay and follow their orders.
- **A point of sale** at your shop's counter. See [Point of sale](point-of-sale.md).
- **A back-office integration**, such as a warehouse system that ships orders and receives stock, or an accounting system that reads orders and refunds.

To have your store tell another system when something happens, such as an order being paid, instead of that system asking, use [webhooks](webhooks.md).

Your developers build the app. This page covers what you set up in the admin so their app can work, and what the API can do. The [Commerce API](../../modules/commerce-api.md) page in the Technical Manual has everything a developer needs: every endpoint, the requests and answers, and how errors work.

| | |
| --- | --- |
| **Address** | `https://your-site/api/commerce/v1/...` (on a tenant with a URL prefix, such as *shop*: `https://your-site/shop/api/commerce/v1/...`) |
| **Who calls it** | Guests with no account, signed-in buyers, and staff or servers that have signed in |
| **Feature** | Catalog API, Cart API, Inventory API, Checkout API, Orders API, Customers API, Shipping API |

<AskYourAdmin />

## Turn it on

Each commerce module has its own **API** feature, under **Tools > Features**, in the **Commerce** category. Turn on the ones your app needs. Each needs its module turned on first.

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

Some parts of the API appear only when another feature is on too:

- Gift cards on a checkout need **Gift Cards**; buyers' returns need **Returns**.
- The Contact step needs **Orders** and the **Checkout API**.
- Store credit and points on a checkout need **Store Credit** or **Loyalty Points**, and the **Checkout API**.

For a typical headless store, turn on all seven.

## Show developers the API

Orchard Core's **OpenAPI** feature describes every part of the API, so developers can explore it, try calls, and generate the code their app uses to call it.

1. Under **Tools > Features**, turn on **OpenAPI**.
2. Turn on one or more explorers: **Swagger UI** or **Scalar UI**, which let developers try calls from the browser, or **ReDoc UI**, which is read-only. They open at `/swagger`, `/scalar/v1` and `/redoc` on your site. On a tenant with a URL prefix, put the prefix first, such as `/shop/swagger`.

By default, only a signed-in user with the **View OpenAPI Content** permission can read the description. The OpenAPI settings page has **Allow anonymous access to the API schema**, which opens it to everyone. Leave it off on a live store unless you want your API to be public knowledge.

The commerce parts are grouped in the explorers: **Catalog**, **Cart**, **Checkout**, **Inventory**, **Account**, and for staff **Orders**, **Customers**, **Coupons**, **Stock**, **Fulfillment** and **Point of Sale**.

:::tip[Try calls from Swagger or Scalar]
The **OpenAPI Documentation — Bearer/PKCE** recipe, under **Configuration > Recipes**, sets up sign-in for the Swagger and Scalar pages, so an admin can try the staff calls from the browser. After running it, open the **openapi** application it creates (see below) and change the redirect addresses that start with `https://localhost:5001` to your site's address. The recipe points token validation at the *Default* tenant; on another tenant, change it to that tenant's name.
:::

## Set up sign-in

Guests need no sign-in: what a shopper does before they have an account works without one. Everything else needs the app to sign in through your site's own sign-in server, which gives it a **token** to send with every call. The API never uses the site's own sign-in, so an app always signs in this way, whether it's a buyer's app, a till or a server.

### Turn on the sign-in server

1. Under **Tools > Features**, turn on **OpenID Authorization Server**, **OpenID Token Validation** and **OpenID Management Interface**.
2. Open **Security > OpenID Connect > Settings > Authorization Server**, and turn on:
   - the **token endpoint**, and the **authorization endpoint** for apps that sign buyers in;
   - **Allow authorization code flow**, with **Require proof key for code exchange (PKCE)**, for apps;
   - **Allow client credentials flow**, for servers;
   - **Allow refresh token flow**, if your apps should stay signed in.
3. Open **Security > OpenID Connect > Settings > Token Validation**, and choose **this tenant** as the one whose tokens are accepted. Without this, every app that has signed in is still refused.

### An app that buyers sign in to

A storefront, a phone app or a point of sale signs the person in through your site's own sign-in page. The app never sees their password, and needs no secret of its own.

1. Under **Security > OpenID Connect > Management > Applications**, click **Add an application**.
2. Give it a **Client Id**, such as *contoso-shop-app*, and a **Display Name**.
3. Choose the **Public** type: an app on a phone or in a browser can't keep a secret.
4. Tick **Allow Authorization Code Flow** and **Require PKCE**. Tick **Allow Refresh Token Flow** if it should stay signed in.
5. Add the app's **Redirect URIs**: where your site sends the person back after they sign in. Your developers give you these, such as `https://shop.contoso.com/signin-callback` for a website.
6. Allow the scopes `openid`, `profile` and `roles`. The `roles` scope tells the API the person's roles, which staff permissions come from. Add `offline_access` if the app should stay signed in.
7. Save.

Give your developers your site's address, the client id and the redirect address. Signed-in buyers get what the **Authenticated** role allows, such as **View own orders**. Staff signing in to a point of sale get what their roles allow.

### A server that works on the store's behalf

A back-office system, such as a warehouse or accounting integration, has no person signing in. It signs in as itself with a secret, and the role you give it decides what it can do.

1. Create a role for it under **Security > Roles**, such as *Warehouse integration*, with only the permissions it needs, such as **Ship orders and update their shipments**, **View inventory** and **Manage inventory**.
2. Under **Security > OpenID Connect > Management > Applications**, add an application of the **Confidential** type, with a **Client Id** and a strong **Client Secret**.
3. Tick **Allow Client Credentials Flow**, and choose the role under **Client Credentials Roles**.
4. Save, and give the secret to whoever runs the server, to keep in its secret store. Never put it in an app or a web page.

### What each caller can do

| Caller | What it can do | Permission |
| --- | --- | --- |
| A guest, not signed in | Browse products and stock, fill a cart, and check out (everything but store credit and points) | None |
| A signed-in buyer | Everything a guest can do, plus their account and addresses, their orders, returns and shipments, store credit and points at checkout, and keeping the cart they filled as a guest | **View own orders** for orders, returns and shipments; **Manage own customer profile and addresses** for their account. Both are given to the **Authenticated** role by default. |
| Staff or a server | Work with orders, customers, stock, coupons and shipments | The same permissions as the matching admin pages: **Manage orders**, **View customers**, **View inventory**, **Manage inventory**, **Manage coupons**, **Ship orders and update their shipments** |

### A storefront on another address

A storefront that runs in the browser on another address than your site, such as *shop.contoso.com* calling *admin.contoso.com*, needs your site to allow it. Turn on Orchard Core's **CORS** feature, and add a policy under **Configuration > Settings > CORS** for your storefront's address. Your developers know the settings it needs; the [Technical Manual](../../modules/commerce-api.md#cors) lists them. A phone app, a point of sale or a server doesn't need this.

## What the API can do

Through the API, an app can do what a buyer does on your storefront, and much of what your staff do in the admin:

- **Shop**: search and list products, priced for whoever is asking, see what's in stock at each store, and fill a cart. A guest's cart can move into their own when they sign in.
- **Check out**: give the buyer's details, choose shipping or pickup, put on discount codes, gift cards, store credit or points, and pay with the same payment methods as your checkout page. The buyer gets the same confirmation email.
- **Follow orders**: buyers see their orders, ask for returns and track shipments.
- **Run the store**: staff, or a server signed in with their permissions, list and find orders, refund and cancel them, ship orders, look up customers, manage coupons, and receive, adjust, count and move stock.
- **Sell at the counter**, with the [point of sale](point-of-sale.md).

The API lets an app with a poor connection send a request again safely: an app that marks its payment, refund or shipment as the same one, and sends it again after losing its connection, gets the first answer back instead of a second charge or a second shipment.

It can't:

- **Change your store's setup.** Products, prices, payment providers, shipping zones, taxes, registers and features are set up in the admin.
- **Fill in your own checkout fields.** If your store asks for its own fields on the Contact step (the **Checkout details** feature), the API has no place for them. Collect them on the checkout page, or leave the feature off for a store sold through the API.
- **Tell other systems what happened.** An app has to ask. To have your store tell them the moment something happens, use [webhooks](webhooks.md).

## Related pages

- [Commerce API](../../modules/commerce-api.md), in the Technical Manual
- [Webhooks](webhooks.md)
- [Point of sale](point-of-sale.md)
- [Checkout](checkout.md)
- [Carts and orders](carts-and-orders.md)
- [Payment providers](payment-providers.md)
- [Customers](customers.md)
- [Shipping](shipping.md)
- [Inventory](inventory.md)
