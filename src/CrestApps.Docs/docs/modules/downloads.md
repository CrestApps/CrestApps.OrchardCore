---
sidebar_label: Digital Downloads
title: Digital Downloads
description: Files kept privately on products, entitlements given when an order is paid, expiring download links with a download limit, and revoking them on cancel or refund.
user_manual: user-manual/commerce/digital-downloads
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Downloads` |
| **Category** | Commerce |
| **Depends on** | Commerce, Products, Orders, `OrchardCore.Contents` |

The **Digital Downloads** feature delivers digital products. A product carries its files in the **Digital files** part; a paid order gives its buyer an entitlement to each file; the buyer downloads through a link that works for a number of downloads and days, on their order page and in the confirmation email; canceling or fully refunding the order takes the files back.

## Private file store

Files never go to the media library. `IDigitalFileStore` keeps them under the tenant's App_Data, in `DigitalFiles/{fileId}/{fileName}`, which no URL serves. `SaveAsync` makes the id (letters and digits only, so an id can't leave the folder) and cleans the name; `FindAsync` and `OpenAsync` find and read a file by id.

Before a file is written, `SaveAsync` runs it through Orchard Core's file creation pipeline (`FileCreationService`), so every registered `IFileEventHandler`, such as the ClamAV scanner of `OrchardCore.Antivirus.ClamAV`, can check it. When a handler refuses the file, or the scanner can't be reached, nothing is kept: the upload answers `400` with the reason, and the recipe step reports an error for that file.

The part's editor uploads each file straight to the store through `POST /Admin/downloads/files/upload` (permission **Manage digital files and downloads**, up to 1 GB), then saving the product keeps the ids the store knows. Removing a file from a product doesn't delete it: orders that have it keep their downloads. `GET /Admin/downloads/files/{fileId}` lets the store open a file.

## Model

- **`DigitalFilesPart`** (attachable): `Files` (`FileId`, `FileName`, `Size`), `DownloadLimit` and `LinkLifetimeDays`, which override the site's settings when set (`0` means no limit, or links that never expire).
- **`DownloadSettings`** (group `downloads`, **Settings > Commerce > Downloads**): `DownloadLimit` (5) and `LinkLifetimeDays` (30).
- **`DownloadEntitlement`** (collection `Downloads`, `DownloadEntitlementIndex`): the order, order line, product and file, `DownloadLimit`, `DownloadCount`, `ExpiresUtc`, `RevokedUtc` and `RevokeReason`, and `LastDownloadedUtc`.

## Granting and revoking

`DownloadOrderEventHandler` is an order event handler:

- **Paid:** `DownloadService.GrantAsync(order)` creates an entitlement per file of each line's product, read from the product version the order bought, in the checkout's unit of work, so the confirmation email sent after it can carry the links. Granting again adds only what is missing.
- **Canceled:** revokes the order's entitlements.
- **Refunded:** revokes them once the order is refunded in full. After a partial refund the buyer keeps their files.

From the order's page, the store can **Take back** one file or **Give downloads back**, which resets the count, extends the link by the site's lifetime and restores a revoked right.

## Links

A link is `/downloads/{token}`, where the token is the entitlement's id signed with data protection (purpose `CrestApps.OrchardCore.Downloads.Link`). Nothing secret is stored, so the order page and a resent email can show a link again; whether it works is decided by the entitlement. The site's base URL is used when set, so links in emails work outside a request.

Opening a link shows the file with a **Download** button; the download is a `POST`, so the link checkers some mail services run never use up a download. `DownloadService.RecordDownloadAsync` counts it under a lock per entitlement, so two downloads at once can't both take the last one. A link that no longer works answers `410` (`404` when it names nothing) with why: expired, used up, or the order was canceled or refunded.

## Order pages and email

- `OrderDownloadsDisplayDriver` adds a **Downloads** section to the order's admin page (counts, status, the buyer's link, and the actions) and **Your downloads** to the buyer's page, with links that still work.
- `DownloadEmailSectionProvider` adds **Your downloads** to the confirmation email through the Orders feature's `IOrderEmailSectionProvider` seam, with each link and how many downloads and days it allows.

## Recipe step

The `DigitalFiles` step puts files in the store and on products, so a recipe can ship a product ready to sell. Each file names its product and gives its bytes as `Base64`, or as `Text` for a small text file, up to 10 MB. A product that already has a file of that name keeps it, so running the recipe again adds nothing.

```json
{
  "name": "DigitalFiles",
  "Files": [
    { "ProductId": "4dgtlprod0trailguide00000a", "FileName": "trail-field-guide.pdf", "Base64": "JVBERi0xLjQK..." },
    { "ProductId": "4dgtlprod0notes0000000000a", "FileName": "read-me.txt", "Text": "Thanks for buying." }
  ]
}
```

The step runs with the tenant's recipe services (feature `OrchardCore.Recipes.Core`). Put it after the `Content` step that creates the products.

## Sample

The **Digital downloads sample** recipe makes a store that sells and delivers ebooks once a payment provider is on:

- a **Digital Product** type (product type `Digital`, with the Digital files part) and two ebooks, each with a PDF;
- an **Ebooks** listing at */ebooks*;
- the Customers feature, whose contact step asks a guest for the email the links go to;
- links that work for 3 downloads and 14 days;
- the coupons **WELCOME10** (10% off, once per customer) and **EBOOKS5** ($5 off orders of $15 or more).

An order of digital products only has no shipping step. Running the online store sample first makes a store that sells both.
