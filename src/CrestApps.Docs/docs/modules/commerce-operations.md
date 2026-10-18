---
sidebar_label: Running a Store
title: Running a Store
description: What a store needs in production — the features for each kind of store, background tasks, what to back up, hosting on several nodes, throttling, and the permissions for store staff.
user_manual: user-manual/commerce/index
---

This page collects what an operator needs to run the commerce modules in production: which features make up each kind of store, what runs in the background, what to back up, what changes on several nodes, and which permissions to give store staff.

## Features for each kind of store

| Store | Features | Sample recipe |
| --- | --- | --- |
| **Goods you ship** | Products, Product Listing, Carts, Orders, Customers, Inventory, Shipping, Checkout, a payment provider; optionally Taxation, Product Reviews and Reports | **Product listing: online store sample** |
| **Files to download** | Products, Product Listing, Carts, Orders, Customers, Digital Downloads, Checkout, a payment provider | **Digital downloads sample** |
| **Both** | Both sets | The online store sample, then the digital downloads sample |

Customers matters for a digital store: its contact step asks a guest for the email the download links go to. Without it, a guest's order has no email.

The samples turn on no payment provider. **Test payments** (`CrestApps.OrchardCore.Checkout.TestProvider`) confirms every payment without moving money. It registers only in the Development environment unless `CrestApps:Checkout:TestProvider:AllowOutsideDevelopment` is `true`, so it can't be left on in production by accident.

## Background tasks

Each runs per tenant, on the schedule shown, while its feature is on.

| Task | Feature | Schedule | What it does |
| --- | --- | --- | --- |
| Checkout Payment Reconciliation | Checkout | Every 5 minutes | Asks the provider about payment attempts still pending, so a payment whose webhook never arrived is still confirmed or failed. |
| Expired Carts Cleanup | Carts | Hourly | Deletes carts nobody changed within their lifetime. |
| Abandoned orders | Orders | Every 15 minutes | Cancels unpaid orders whose checkout was canceled, failed, expired or never started, |
| Expired stock holds | Inventory | Every 5 minutes | Releases stock held for orders not paid before the hold expired. |
| Customer data retention | Customers | Daily at 03:30 | Erases inactive guests' personal data and anonymizes old guest orders, per **Settings > Commerce > Customers**. |
| Product Search Indexing | Products | Every minute | Rebuilds the product search records when product types or their attributes change. |
| Pay Later Renewals | Pay Later | Every 30 minutes | Creates the next balance for recurring Pay Later commitments. |
| Transaction Payment Reminders | Transactions | Every 6 hours | Sends reminders for outstanding balances. |

They can be paused, rescheduled or run under **Configuration > Tasks** (feature `OrchardCore.BackgroundTasks`).

## What to back up

- **The tenant's database.** Orders, carts, customers, coupons and their uses, stock and its movements, shipments, download rights and reviews are all documents in it.
- **The digital files.** Digital Downloads keeps files outside the database and the media library, in `App_Data/Sites/{tenant}/DigitalFiles/{fileId}/{fileName}`. A restored database without this folder has download rights to files that are gone.
- **The data protection keys.** Download links and guest order links are signed with the tenant's data protection keys, not stored. With new keys, every link already emailed stops working. Resending an order's confirmation from its page sends links that work again.

## Several nodes

On a web farm, every node must see the same locks, cache, files and keys:

- Turn on the Redis features (`OrchardCore.Redis`, `OrchardCore.Redis.Lock`, `OrchardCore.Redis.Cache`, `OrchardCore.Redis.Bus`). Checkout finalization, webhook handling, coupon uses, order numbers and download counts are all decided under `IDistributedLock`. Without a shared lock, two nodes can both take a coupon's last use or a file's last download.
- Share the `DigitalFiles` folder, for example with a network share mounted at `App_Data`, or every node only serves the files uploaded through it.
- Share the data protection keys (for example with `OrchardCore.Redis.DataProtection` or the Azure data protection feature), or a link signed on one node fails on the others.

## Throttling

| What | Limit | Where it is counted |
| --- | --- | --- |
| Cart forms (`POST cart/add`, `cart/update`, `cart/remove`) | The `Cart` group of Orchard Core's Rate Limits feature | Its configuration |
| Starting a payment and checking its status | 20 attempts per 5 minutes per client address and checkout (`PaymentRateLimitOptions`) | The distributed cache, per tenant |
| Entering coupon codes | The same limit, per client address | The distributed cache, per tenant |
| Looking up a guest order | The same limit, per client address and per email | The distributed cache, per tenant |

Counts are kept per tenant, so a busy tenant never throttles another, even with a shared cache.

## Tenant isolation

Every commerce record is a document in the tenant's own database, and digital files sit in the tenant's own folder. A coupon used up on one tenant is still available on another, a download link signed by one tenant names nothing on another, and throttling counts never cross tenants.

## Permissions for store staff

| Permission | Feature | For |
| --- | --- | --- |
| **Manage orders** | Orders | Seeing every order, and refunding, canceling, annotating and resending them |
| **View own orders** | Orders | Customers, for **My Orders** |
| **Ship orders and update their shipments** | Shipping | Warehouse staff |
| **Manage shipping zones, methods and settings** | Shipping | Whoever sets shipping prices |
| **Manage inventory** / **View inventory** | Inventory | Receiving and counting stock / looking it up |
| **Manage customers** / **View customers** | Customers | Customer service |
| **Manage own customer profile and addresses** | Customers | Customers |
| **Manage coupons** | Checkout | Marketing |
| **Manage digital files and downloads** | Digital Downloads | Uploading files, and taking back or giving back downloads |
| **Moderate product reviews** | Product Reviews | Approving reviews |
| **Review products bought** | Product Reviews | Customers (signed-in users have it) |
| **Manage taxation** | Taxation | Whoever sets tax rates and the store's origin |

## Related

- [Commerce overview](commerce.md)
- [Carts and orders](carts-and-orders.md)
- [Digital Downloads](downloads.md)
- [Payments](payments.md), for webhooks and multi-instance payment hosting
