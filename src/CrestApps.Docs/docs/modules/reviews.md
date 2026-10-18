---
sidebar_label: Product Reviews
title: Product Reviews
description: Verified-purchase product reviews with moderation, stars on product pages, and an average rating that product listings filter and sort by.
user_manual: user-manual/commerce/reviews
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Reviews` |
| **Category** | Commerce |
| **Depends on** | Commerce, Products, Orders, `OrchardCore.Contents` |

The **Product Reviews** feature lets buyers rate and review the products they bought. The store approves each review under **Commerce > Reviews**; approved reviews show on the product's page with its stars, and the product's average rating goes into its listing search record, where listings filter and sort by it.

## Model

- **`ProductReview`** (collection `Reviews`, `ProductReviewIndex`): the product, the user and the name shown, `Rating` (1 to 5), `Title`, `Body`, `Status` (`Pending`, `Approved` or `Rejected`), `VerifiedPurchase` and the `OrderId` it was bought on, and who moderated it when.
- **`ReviewSettings`** (group `reviews`, **Settings > Commerce > Reviews**): `RequireApproval` (on) and `BuyersOnly` (on).
- **`ProductRating`**: the count and the average (one decimal) of a product's approved reviews, from `ProductRatingReader`.

## Submitting

`ProductReviewsDisplayDriver` adds two shapes to a product's `Detail` display: **`ProductRating`** (stars, average and count, at `Content:before:5`) and **`ProductReviews`** (the approved reviews, newest first, and the form, at `Content:60`, after the specifications at `Content:50`). A signed-in user with **Review products bought** (granted to Authenticated) sees the form when `ReviewService.GetEligibilityAsync` allows it: they bought the product on a paid order, or `BuyersOnly` is off. The form posts to `POST /reviews/{productId}`.

A user has one review per product: reviewing again changes it and, with `RequireApproval`, sends it for approval again. A review written by a buyer is a **verified purchase**, recorded with its order.

## Moderating

**Commerce > Reviews** (permission **Moderate product reviews**) is an admin list of reviews, waiting for approval first, filtered by status, with **Approve**, **Reject** and **Delete** on each. Approving, rejecting or deleting an approved review calls `IProductSearchIndexRefresher.RefreshAsync` for its product, so the rating in listings follows at once.

## Ratings in listings

With Product Listing on:

- `ReviewSearchDocumentContributor` writes `ProductAttributeKeys.Rating` (the average) and `ProductAttributeKeys.ReviewCount` to each product's search record. A product with no approved review has neither.
- `ReviewListingEntryProvider` adds **Customer rating** to listings with automatic filters (a range filter with ready-made ranges from 4, 3 and 2 stars, key `avg-rating`) and **Customer rating: highest first** to listings with automatic sorts (key `avg-rating-desc`); products without a rating sort last. A listing that chooses its own filters and sorts can add them by attribute key `Product.Rating`.

`IProductListingEntryProvider` is the Products seam for this: any module that writes an attribute to the search records can add automatic filters and sorts for it.
