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
- **`ReviewSettings`** (group `reviews`, **Settings > Commerce > Reviews**): `RequireApproval` (on), `BuyersOnly` (on), `SendReviewRequests` (off) and `ReviewRequestDelayDays` (14).
- **`ProductReview.Photos`**: the media paths of up to three photos.
- **`ProductRating`**: the count and the average (one decimal) of a product's approved reviews, from `ProductRatingReader`.

## Submitting

`ProductReviewsDisplayDriver` adds two shapes to a product's `Detail` display: **`ProductRating`** (stars, average and count, at `Content:before:5`) and **`ProductReviews`** (the approved reviews, newest first, and the form, at `Content:60`, after the specifications at `Content:50`). A signed-in user with **Review products bought** (granted to Authenticated) sees the form when `ReviewService.GetEligibilityAsync` allows it: they bought the product on a paid order, or `BuyersOnly` is off. The form posts to `POST /reviews/{productId}`.

A user has one review per product: reviewing again changes it and, with `RequireApproval`, sends it for approval again. A review written by a buyer is a **verified purchase**, recorded with its order.

## Photos

With Orchard Core's Media feature on, the form takes up to three photos (`ReviewPhotoStore`):

- Each is a JPEG, PNG or WebP of at most 5 MB, checked by its first bytes.
- They're saved under `reviews/{productId}/` in the media store.
- New photos replace a review's, and the old files are deleted.
- The product page and the admin list show them through `IMediaFileStore.MapPathToPublicUrl`.

## Review requests

`ReviewRequestBackgroundTask` (hourly) asks buyers to review what they bought, with `SendReviewRequests` on. It needs the site's email, its base URL and Customers. `ReviewRequestService.SendDueAsync` reads paid orders placed in the last 60 days and at least `ReviewRequestDelayDays` ago, without `Order.ReviewRequestSentUtc`, up to 200 a run. It emails each order's buyer once, with a link to each product's reviews, when their customer accepts marketing. It marks every due order, so a buyer who doesn't accept marketing isn't asked later.

## Moderating

**Commerce > Reviews** (permission **Moderate product reviews**) is an admin list of reviews, waiting for approval first, filtered by status, with **Approve**, **Reject** and **Delete** on each. Approving, rejecting or deleting an approved review calls `IProductSearchIndexRefresher.RefreshAsync` for its product, so the rating in listings follows at once.

## Ratings in listings

With Product Listing on:

- `ReviewSearchDocumentContributor` writes `ProductAttributeKeys.Rating` (the average) and `ProductAttributeKeys.ReviewCount` to each product's search record. A product with no approved review has neither.
- `ReviewListingEntryProvider` adds **Customer rating** to listings with automatic filters (a range filter with ready-made ranges from 4, 3 and 2 stars, key `avg-rating`) and **Customer rating: highest first** to listings with automatic sorts (key `avg-rating-desc`); products without a rating sort last. A listing that chooses its own filters and sorts can add them by attribute key `Product.Rating`.

`IProductListingEntryProvider` is the Products seam for this: any module that writes an attribute to the search records can add automatic filters and sorts for it.

## Report Builder data source

With **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) on, which the Report Builder needs, Product Reviews adds the **Reviews** data source (`Reviews`) to the Report Builder and to Orchard Core data pipelines. Its data set needs **Moderate product reviews** (`ManageProductReviews`), the permission of **Commerce > Reviews** and of the built-in **Product reviews** report, which keeps working as before.

The **Product reviews** data set (`ProductReviews`) has one row per review, newest first by when it was written, which is its default date. Date ranges on that date, joins on the review, product and user IDs, and status filters are applied in the database, and simple groups and totals of the fields its index holds (status, rating, product, user and date) are counted there too. The text of a review and its photos are never exposed: only the number of photos is.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Review ID | Identifies the review. |
| `Rating` | Rating | The stars given, from 1 to 5. |
| `Title` | Title | As the reviews page shows it. |
| `Status` | Status | `Pending`, `Approved` or `Rejected`. |
| `IsApproved` | Approved | Whether it shows on the product. |
| `VerifiedPurchase` | Verified purchase | Whether the reviewer bought the product on a paid order. |
| `PhotoCount` | Photos | How many photos the reviewer added. |
| `ProductId` | Product ID | References the product (`Products` > `Products` > `ContentItemId`). |
| `ProductTitle` | Product | The product's title when it was reviewed. |
| `UserId` | User ID | References the user (`Users`). |
| `AuthorName` | Reviewer name | The name shown with the review. |
| `OrderId` | Order ID | The order the product was bought on, for a verified purchase; references `Orders` > `Orders`. |
| `CreatedUtc` | Written | The default date. |
| `ModifiedUtc` | Last changed | |
| `ModeratedUtc` | Approved or rejected | When the store last decided on it. |

## Reports to adapt

The **Reports: product review reports to adapt** recipe (`ReviewsReportBuilderReports`) turns on the Report Builder and adds reports built from this data set, in the **Store** report group. Together they show what the built-in **Product reviews** report shows, so you can open one in the builder, change it, and save it as your own. The person who runs the recipe owns them, and they read with that person's access.

| Report | Built from | Matches the built-in report |
| --- | --- | --- |
| Reviews by stars | **Product reviews** written in the last 30 days, grouped by stars and status | Product reviews: the stars given and the reviews that wait for approval |
| Reviews by product | **Product reviews** written in the last 30 days, grouped by product, with calculated fields for the reviews that wait or are approved, the average rating of the approved reviews, and the share of verified purchases | Product reviews: the products |

A designed report has one set of groups, so the recipe splits the built-in report in two. No part of it stays built-in only.
