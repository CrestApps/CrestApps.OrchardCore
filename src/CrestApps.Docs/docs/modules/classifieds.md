---
sidebar_label: Classifieds
title: Classifieds
description: "Classifieds: listings visitors post from the site, moderated before they show, running for a term then expiring and renewed, found by words and distance on a map, and featured or bumped by paid upgrades bought through the cart."
user_manual: user-manual/commerce/classifieds
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Classifieds` (Classifieds) |
| **Category** | Commerce |
| **Depends on** | Commerce, Orders |
| **Seams it fills** | `ICartLineHandler` (Carts), `IOrderEventHandler` (Orders) |

The **Classifieds** feature lets visitors post content from the site, such as ads, directory entries or properties. The attachable **Classified** part makes a content type one they post. The **Classified upgrade** part makes a product an upgrade that features or bumps a listing. Admin screens are described in the [User Manual](../user-manual/commerce/classifieds.md). The **Classifieds sample** recipe adds a Classified Ad type and a $5 upgrade that features an ad for 7 days.

## The parts

`ClassifiedPart` holds the listing's state:

- `ClassifiedStatus`: Pending, Approved, Rejected or Expired, with the `RejectReason`.
- `PostedUtc`, `ExpiresUtc`, `FeaturedUntilUtc` and `BumpedUtc`.
- Where it is: `Place`, `Latitude` and `Longitude`.

Its settings on the type (`ClassifiedPartSettings`) are `TermDays` (30) and `RequireApproval`. Owners edit the place on the storefront form, with **Use where I am** filling the coordinates from the browser. Only staff see the status and dates, in the admin editor (`AdminAttribute.IsApplied`).

`ClassifiedUpgradePart` holds `Kind` (Feature or Bump) and `Days`.

`ClassifiedIndex` maps each listing's latest and published versions: type, owner, status, dates and coordinates.

## The service

`ClassifiedService` does the work:

- **`PostAsync`** sets a new listing to waiting, or approves and publishes it when its type doesn't need approval.
- **`ChangedAsync`** takes an owner's edit. When the type needs approval, the listing waits for it again and is taken down until then.
- **`ApproveAsync`** publishes it for its term. **`RejectAsync`** unpublishes it with a reason. Both email the owner.
- **`RenewAsync`** adds a term to a listing that's up, from its end, or starts a new one for an expired listing, and publishes it.
- **`UpgradeAsync`** features a listing for the upgrade's days, from the current end when it's already featured, or bumps it to the top.
- **`ExpireDueAsync`** unpublishes listings past their end and emails their owners. `ClassifiedsBackgroundTask` runs it every five minutes.
- **`SearchAsync`** finds published listings by type and words, and within a distance. It narrows by a bounding box in the index, then by the Haversine distance (`GeoDistance`). Featured listings come first, then the most recently bumped or posted.

`ClassifiedUpgradeLineHandler` puts the listing on the upgrade's cart line as the `classified.listing` detail, and refuses a listing that isn't the buyer's own. `ClassifiedUpgradeOrderHandler` applies the upgrade when the order is paid.

## Pages

- `listings/post/{type}` and `listings/edit/{id}` (`PostClassifieds`, granted to Authenticated): the type's editor on the storefront.
- `account/listings` (the `ClassifiedsOwn` shape): the owner's listings with their status, **Renew**, and a button for each upgrade.
- `listings` (the `ClassifiedsSearch` shape): the search by type, words, and distance from where the visitor is, with a map. The map is Leaflet with OpenStreetMap tiles, loaded from cdnjs.
- **Content > Posted listings** (`ModerateClassifieds`): the admin list of listings by status, with **Approve** and **Reject**.
