---
sidebar_label: Learning Commerce
title: Learning Commerce
description: "Sell courses and learning paths as products: a paid order enrolls the buyer, an account buys seats its admins give to members or invite people to, and a refund takes it all back."
user_manual:
  - user-manual/learning/selling-courses
---

**Learning Commerce** (`CrestApps.OrchardCore.Learning.Commerce`, with `Learning.Commerce.Abstractions` and `Learning.Commerce.Core`) sells [Learning](./learning) through the commerce suite. It builds on [Products](./products), [Orders](./orders), [Carts](./carts) and [Accounts](./accounts) and adds no store of its own: a course is a product, a purchase is an order line, and a business buyer is an account.

| Feature | Id | Depends on |
| --- | --- | --- |
| Learning Commerce | `CrestApps.OrchardCore.Learning.Commerce` | Learning, Product Listing, Orders, Account Members |

## Courses as products

The feature's migration attaches `ProductPart` and `LearningProductPart` to the **Course** type and, when Learning Paths is on, to the **Learning Path** type (also when Learning Paths is turned on later, through `LearningPathProductMigrations`), with the product page details turned on (`ProductPageSettings.ShowDetails`), so a priced course or path shows its price and an **Add to cart** button on its page and appears in the Products listing. The price may be left empty (`ProductPartSettings.PriceOptional`): a course or path without a price is simply not for sale and shows no price. Migration step 3 applies both settings to sites made before them. `LearningProductPart` can also be attached to any other product type, for a bundle of courses or a pack of seats.

`LearningProductPart.Grants` says what buying one unit grants:

| Grant property | Meaning |
| --- | --- |
| `Kind` | `Course` enrolls the buyer, `LearningPath` starts the buyer on a path (needs Learning Paths), and `Seats` gives the buyer's account a licence pool. |
| `TargetId` | The course or path. Empty means the item itself. |
| `AccessDays` | How long the access lasts from the purchase; empty for no end. On seats, how long each seat's enrollment lasts. |
| `Seats` | The seats per unit. A line of quantity 3 for a product with 10 seats per unit gives 30 seats. |

With no grant, buying a course enrolls the buyer in it and buying a learning path starts the buyer on it.

## Cart rules

`LearningCartLineHandler` (an `ICartLineHandler`) refuses a learning product to a guest, because learning is for a signed-in person, and refuses a product with a seat grant to anyone who is not an admin of a business account (`AccountMemberRole.Admin` on the account's `AccountMembersPart`). It also refuses a course that is no longer published, and a course for the buyer whose prerequisites the buyer has not met (checked with the same `ICoursePrerequisiteEvaluator`s as an enrollment, through `CoursePrerequisites.MeetsAsync`). The cart shows the reason on the line, and checkout stops until the line is removed.

A purchase never asks for a course's enrollment key or for an approval, so a course in the `Key` or `Approval` enrollment mode is not sold: the cart refuses a course grant or a seat grant for it unless the product's `LearningProductPart.EnrollsWithoutKeyOrApproval` is true (**Buying it enrolls without the enrollment key or approval** in the editor). The fulfilment checks again with the copy of that flag on the purchase (`LearningPurchaseGrant.EnrollsWithoutKeyOrApproval`), so a course whose mode changed after it went in the cart moves the purchase to `Failed` instead of enrolling anyone. A `Closed` course (only staff and other sources enroll) is sold, because a purchase is one of those sources; an `Open` course is sold too, although anyone may enroll in it for free. Learning paths keep the enrollment controls of their courses, as before.

A course or a learning path is for the buyer alone, so its line is capped at a quantity of one. A product with a seat grant can be bought in any quantity.

## Grants and refunds

`LearningOrderEventHandler` derives from `OrderLineGrantHandlerBase<LearningProductPart>`, so it reads the product version that was bought, grants each line once however many times the order is paid, and takes back each line when the order is canceled or refunded in full. A partial refund keeps everything.

The order event runs inside the checkout's single unit of work, so it commits nothing. On payment it records each line as a `LearningPurchase` in the state `Pending`, with a copy of what the line buys, and queues a `LearningFulfillmentWork` item on a leased work queue (collection `LearningFulfillmentWork`) in the same unit of work. When the checkout rolls back, neither exists. The grants are made afterwards by `LearningPurchaseFulfillment`: right after the request for the work it queued, and every two minutes by the **Learning purchase fulfilment** background task for whatever is still due. Each purchase is changed under a lock of its own and read fresh, and each grant is recorded as soon as it is made, so a retry never grants twice.

- A paid course enrolls the buyer even when a prerequisite is not met yet (`IgnorePrerequisites`): the cart checked it, and the buyer paid.
- A busy enrollment, or any failure that throws, is retried with a back-off.
- A grant that can never be made (the course was unpublished after it was put in the cart, the buyer left their business account, learning paths are not enabled) moves the purchase to `Failed` with the reason in `LearningPurchase.Error`, is logged, and is added to the order as a note, where staff see it. It is not retried.

A full refund or a cancellation queues the same kind of work with the request to take the grants back.

| Grant | On payment | On a full refund or a cancellation |
| --- | --- | --- |
| Course | `IEnrollmentService.EnrollAsync` with source `Learning.Commerce` and source id `{orderId}:{lineId}`, and `AccessEndsUtc` when the grant has access days. | `RevokeGrantAsync("Learning.Commerce", "{orderId}:{lineId}")` takes the grant off an open or expired enrollment, and ends it when it was its last grant. A completed enrollment is closed, so it keeps its grant and its certificate. |
| Learning path | `ILearningPathService.StartAsync` with the same source. | Not taken back yet: the path progress stays, and its course enrollments stay with it. |
| Seats | `ILicensePoolService.CreateAsync` for the order's account or, without one, the buyer's account. Creating a pool is idempotent per order line. | `ILicensePoolService.RevokeAsync` takes every seat back and ends every enrollment made from the pool. |

Each line's grants are kept in a `LearningPurchase` record (collection `LearningCommerce`) with its state (`Pending`, `Fulfilled`, `Failed` or `Revoked`), the enrollments, path progress and pools it made, and its `RevokedUtc` once it was taken back. An enrollment that is busy when the refund is processed keeps the grant for a moment: the work is retried, and the purchase becomes `Revoked` only when no open or expired enrollment carries the grant any more.

## Licence pools and seats

A `LicensePool` holds the seats an account bought for one course, under the course's title. Its free seats are a quantity ledger (`IQuantityLedger`, collection `LearningSeatLedger`): the purchase posts the seats, each seat given or held posts minus one, and each seat taken back posts plus one, every entry under its own key so a retry never counts twice.

`ILicensePoolService`:

| Method | What it does |
| --- | --- |
| `GiveAsync(poolId, userId, actor)` | Gives a seat to a member of the account and enrolls them with source `Learning.LicenseSeat` and the seat id. A person gets one seat per pool; someone who is not a member is refused. |
| `InviteAsync(poolId, email, actor)` | A member with that e-mail gets the seat at once. Anyone else gets an [account invitation](./accounts#invitations) with source `Learning.LicenseSeat`, and the seat is held for them. When they accept, `LicenseSeatInvitationObserver` assigns the seat and enrolls them. |
| `RemoveAsync(seatId, actor)` | Takes a seat back, ends its enrollment and frees the seat. |
| `RevokeAsync(poolId, reason)` | Marks the pool revoked, takes every seat back and ends every enrollment from it. |
| `GetRemainingAsync(pool)` | The free seats. |

`ILicensePoolObserver` hears `SeatsPurchasedAsync`, `SeatGivenAsync` and `RunningLowAsync` (a tenth of the seats or fewer left); register one with `services.AddLicensePoolObserver<T>()`.

## Course seats page

`/account/learning-seats`, linked as **Course seats** in the storefront account menu, shows an account admin each pool with its free seats, the people holding a seat with their progress, and the invited people. The admin gives a seat to a member, invites someone by e-mail and takes a seat back. Anyone who is not an admin of the account is refused, and a pool of another account is refused.

## Schema.org

With [Advanced SEO](./advanced-seo) on, the Learning module describes a course as a schema.org `Course` with its summary, `courseCode`, `educationalLevel`, `timeRequired` and the site's publisher as `provider`. It runs after the product provider, so a course sold in the store keeps its `offers`. The `provider` refers to the publisher node the site provider adds (`StructuredDataNodeIds.Publisher`), so `CourseProviderStructuredDataProvider` runs after it (`StructuredDataProviderOrder.Site`); a site whose Advanced SEO settings name no publisher gets no `provider`.

## Notifications, workflows and data

| Area | What it adds |
| --- | --- |
| Notifications | **Course seat given** tells a person their account gave them a seat. |
| Workflows | Events **Course Seats Purchased**, **Course Seat Given** and **Licence Pool Running Low**, and the **Give Course Seat** task. |
| Data | The **Learning commerce** data source with the **Licence pools** and **Seat holders** data sets, behind **View learning reports**. Their ids name what they identify (the course and account content items, the pool, the person), so a Report Builder report joins the Contents, Users and Licence pools data sets to show titles and names instead of ids. |
| Personal data | Exports a person's seats and purchases. Erasing the person takes their seats back (each released on the seat ledger, its enrollment or invitation ended) and clears the person from the seats and the purchases, which stay so the ledger and a later refund still find them; the account keeps its pools. |

## Not in this version

- A subscription that grants learning: Subscriptions has no extension point for a custom entitlement yet.
- Taking back a purchased learning path on a refund.
- Selling certification paths and extra quiz attempts.
- A recipe step for licence pools: they are learner records made by orders.
