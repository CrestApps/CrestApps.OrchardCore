---
sidebar_label: Digital downloads
title: Digital downloads
description: Sell files such as ebooks or software, keep them private, and let buyers download them after paying through links that expire and can be used a set number of times.
technical_manual:
  - modules/downloads
---

**Digital downloads** let you sell files, such as an ebook or a software installer. You upload the file on the product; after paying, the buyer downloads it from their order page or the link in their confirmation email. Links work a set number of times and for a set number of days, and stop working when you cancel or refund the order.

| | |
| --- | --- |
| **Menu** | The product's editor; Settings > Commerce > Downloads |
| **Permission** | **Manage digital files and downloads** |
| **Feature** | Digital Downloads |

<AskYourAdmin />

## Set up a digital product

Your content type needs the **Product** part with the product type **Digital**, and the **Digital files** part. The **Digital downloads sample** recipe adds a ready **Digital Product** type with two sample ebooks and their PDF files, an **Ebooks** page at */ebooks*, and two coupons. See [Try a store sample](index.md#try-a-store-sample).

:::note[Guests need to give an email]
Download links are sent to the email the buyer gives at checkout, which the **Customers** feature asks for. The sample turns it on. If you set up your own store, turn it on too.
:::

1. Open the product and find **Digital files**.
2. Under **Files**, choose one or more files. Each uploads right away, up to 1 GB, and appears in the list.
3. Optionally set **Downloads per buyer** and **Days the links work** for this product. Leave them blank to use the site's settings.
4. **Publish** the product.

The files are kept privately: they are not in the media library, and nobody can open them without a buyer's link. When the site checks uploads for viruses, a file that fails the check, or that can't be checked, isn't kept and an error shows under **Files**. To replace a file, tick **Remove** next to the old one and upload the new one. Buyers who already have the old file keep it.

A buyer of digital products only isn't asked for a shipping address.

## How buyers download

When the order is paid:

- The confirmation email has a **Your downloads** section with a link to each file, and how many downloads and days it allows.
- Their order page shows **Your downloads** with a **Download** button for each file.

A link opens a page with the file's name, how many downloads are left and until when, and a **Download** button. When a link no longer works, the page says why: it expired, all its downloads were used, or the order was canceled or refunded.

## Settings

Under **Settings > Commerce > Downloads**:

| Setting | What it does |
| --- | --- |
| **Downloads per buyer** | How many times a buyer can download each file they bought. 0 means no limit. |
| **Days the links work** | How long after paying the links work. 0 means they never expire. |

A product's own values, when set, win.

## Manage a buyer's downloads

On the order's page, **Downloads** lists each file with how often it was downloaded, until when the link works, and whether it still does.

- **Take back** stops a link from working.
- **Give downloads back** lets the buyer download again: the count starts over, the link works for another period, and a link you took back works again.
- **Buyer's link** is the link to send a buyer who lost their email.

Canceling an order, or refunding all of it, takes its downloads back. After a partial refund the buyer keeps them.
