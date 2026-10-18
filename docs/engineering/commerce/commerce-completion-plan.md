# Commerce completion plan

Status: decisions agreed 2026-10-18 (Mike). The remaining work of the [e-commerce plan](../../ecommerce-plan.md)
(phases 8 to 12) after [inventory and shipping](inventory-and-shipping-plan.md), from an audit of the code on
2026-10-18.

## Where things stand

Built and working: catalog and listings with variants, durable carts, orders with a checkout, customers and address
books, stock with reservations, shipping with zones, a checkout step and shipments, coupons at checkout, Stripe and Pay
Later, refunds through the payment ledger, guest order links, and subscription reports.

The audit found these gaps, ranked by what they cost a store owner:

| # | Gap | Today |
| --- | --- | --- |
| 1 | **Tax on discounted orders** | Discounts come off the total, but each line is taxed on its full price (`CheckoutTaxContextFactory` never sets `TaxableItem.DiscountAmount`), so a coupon order is over-taxed. A one-time coupon also discounts shipping, and its minimum counts shipping. |
| 2 | **Discounts on the order** | `Order` keeps no discount, so its page, the guest page and the emails show lines + shipping + tax that don't add up to the total. |
| 3 | **Coupon integrity** | The last use can be taken twice (no hold, no re-check), a use is lost when the lock times out, there is no per-customer limit, no maximum spend, no product or category limits, no free-shipping coupon, no attempt throttling, no recipe or deployment step, and a canceled order keeps its use. |
| 4 | **Order operations** | No notes, no cancel or refund from the order, no Refunded status, no resend, no order workflow events beyond stock and shipments. |
| 5 | **Order reports** | Every commerce report reads subscription payments; nothing reports order sales, products, tax, discounts, refunds, stock or fulfillment. |
| 6 | **Digital delivery** | A digital product can be sold but never delivered: no files, entitlements or download links. |
| 7 | **Customer tax profile** | The checkout never passes the customer's exemption or tax id, or the store's origin, to tax. |
| 8 | **Reviews** | None. |
| 9 | **Release readiness** | No store profile recipes beyond the physical sample, no coupon or order recipe steps, no commerce tenant-isolation tests. |

## Decisions (agreed 2026-10-18)

| # | Decision |
| --- | --- |
| CC-D1 | **A coupon's discount is spread across item lines only, in proportion to their price.** Shipping is never discounted except by a free-shipping coupon. Each line is taxed on what's left (`TaxableItem.DiscountAmount`), and a coupon's minimum counts items only. |
| CC-D2 | **Digital files live in a private store**, not the public media library: uploaded on the product, never reachable by URL, delivered through expiring, per-order download links with a download limit, revoked when the order is refunded or canceled. |
| CC-D3 | **A coupon's use is held when payment starts**, like a stock hold, and released if the checkout is canceled or expires; it becomes a redemption when the order is paid. A buyer whose code was used up meanwhile is told before paying. |
| CC-D4 | **Product reviews are in scope**: verified-purchase reviews with moderation, ratings on product pages, and an average rating listings can filter and sort by. |
| CC-D5 | **Out of scope for now:** JSON APIs for headless storefronts, front-end account pages (accounts stay in the admin area, plus the order sections customers already see), and a service-fulfillment lifecycle. |

## Slices

| Slice | Scope | Exit gate |
| --- | --- | --- |
| **D1 Discounts** | Allocate discounts to item lines, tax the discounted amount, items-only coupon base, discount lines on the order, its pages and emails | A 10% coupon on a $50 item with $15 shipping and a 10% tax charges $4.50 tax on the items (not $5.00) and $1.50 on shipping, and the order adds up. |
| **D2 Coupons** | Hold a use at payment, redemption records, per-customer limits, maximum spend, product and category limits, free-shipping coupons, attempt throttling, use given back on cancel, recipe and deployment steps | Two buyers racing for a coupon's last use: one pays, the other is told it's used up; a canceled order frees its use. |
| **O1 Order operations** | Order notes, cancel, refund from the order, Refunded and Partially refunded statuses, resend confirmation, order workflow events | An administrator refunds part of an order from its page; the order shows *Partially refunded* and the note says who did it. |
| **R1 Reports** | Order reports: sales over time, sales by product, by country, tax, discounts, refunds, stock on hand, unfulfilled orders | The sales report for a day with a coupon order and a refund matches the orders. |
| **G1 Digital delivery** | Digital part with private files, entitlements on payment, download links with expiry and limits, the downloads section on order pages and in the confirmation email, revoke on refund | A buyer of a digital product downloads it from the confirmation email; after the limit or a refund, the link stops working. |
| **T1 Customer tax** | The customer's exemption and tax id, and the store's origin address, reach checkout tax | A tax-exempt business customer is charged no tax. |
| **V1 Reviews** | Reviews module: verified purchase, moderation, ratings, listing filter and sort | A buyer reviews a product; after approval its stars show and the listing sorts by rating. |
| **RR Release** | Store profile recipes (digital, mixed), coupon samples, commerce tenant-isolation tests, operations docs | A fresh digital store sample sells and delivers a download. |

Each slice is a PR with tests, opened when its tests pass, stacked on the previous one until they merge.

## As built

### D1 Discounts

- `CheckoutDiscountBase` (Checkout abstractions) says what a discount can take off: for `OneTime`, lines without a plan that aren't shipping; for `FirstCycle`, plan lines collected now. `GetAmount` sums them and falls back to the whole bucket for an invoice built without lines, so existing hand-built invoices behave as before.
- `DefaultCheckoutDiscountService` caps each bucket's discount at that amount, then spreads it over the eligible lines by line total into `CheckoutLineItem.DiscountAmount`, the last line taking the rounding remainder. `CouponDiscountProvider` uses `GetAmount` as the coupon base, so percentages and minimums ignore shipping.
- `CheckoutTaxContextFactory` copies the share to `TaxableItem.DiscountAmount`; `DefaultTaxableBaseCalculator` already subtracted it.
- `Order.Discounts` (`OrderDiscount`: code, description, amount, provider key) is copied from the invoice when the order is paid; the admin and guest order pages, the checkout summary and the confirmation email list each discount.
- Found while verifying: the Checkout module registered `NullCheckoutTaxService` with `AddScoped`, so when its startup ran after `TaxationStartup` no tax was charged at all. It is now `TryAddScoped`, with a test for both startup orders.
- Verified live: a $129 item with $6 shipping to Seattle and SAVE10 shows *Discount (SAVE10)* −$12.90, $11.61 tax on the item and $0.60 on shipping, $134.31 due; the order page and email show the same and add up.

### D2 Coupons

- Holds: `ICheckoutPaymentGate` runs in the engine inside the session lock, before any provider is contacted in `BeginPaymentAsync`, and before completion when nothing is owed. `CouponHoldPaymentGate` holds each coupon's use; on refusal it removes `CheckoutCoupon`, rebuilds the invoice and sets `InvoiceChanged`, which the begin endpoint answers with 409 + `invoiceChanged` and the script loads the page again (a GET, not a reload, since the page may answer the Apply post).
- Ledger: `CouponRedemption` documents in the coupon collection, indexed by code, session, customer id and email (`CouponMigrations.UpdateFrom1Async`, indexes through `IndexTableIndexCreation`). `CouponRedemptionService` commits inside the coupon lock. A redemption that can't take the lock is saved uncounted and folded into the count by the next locked one, which fixes the lost use.
- Coupon fields: `CouponKind.FreeShipping` (`DiscountTarget.Shipping`), `MaximumAmount`, `MaxRedemptionsPerCustomer`, `ProductIds`, `CategoryIds`. Discounts are now allocated per discount line (`DiscountLine.LineItemIds`), capped by what each line has left.
- Orders: `OrderLine.CategoryIds` captured from the product's taxonomy fields at checkout (`ProductCategories`); billing items carry `ProductId` and `CategoryIds`; `OrderCouponEventHandler` returns uses on cancel; `CouponItemLimitsDisplayDriver` plus two search endpoints for the pickers (Orders now depends on CrestApps Resources for the item selector).
- `Coupons` recipe and deployment steps. Personal data erasure forgets who used a coupon (`ForgetCustomersAsync`).
- Found while verifying: the limits editor rendered empty because its view model was sealed (Orchard Core proxies it); the payment page reload re-posted the Apply form.
- Verified live: CONTOSO15 (Contoso brand) took $19.35 off a $129 watch and nothing off a $79 backpack, with the watch taxed on $109.65; SHIPFREE took $15 of Express shipping off with no shipping tax; LAST1 (one use) applied by a guest and the store's account: the account paid, and the guest's Pay now was refused with the new total; a code disabled while the guest was paying was taken off the same way; the coupon page listed the use.

### O1 Order operations

- Refunds are a dimension beside the status (`Order.Refunds`, `RefundedAmount`, `RefundStatus`), not new statuses: sixteen places check for a paid order and keep working.
- `OrderOperationsService` refunds money paid online through `ICheckoutRefundService` (confirmed attempts of the order's checkout that moved money; Pay Later's deferred attempts are skipped) and Pay Later money through `ITransactionCommandService.RefundAsync` on the order's transactions. Card payments create no transaction; only Pay Later and installments do.
- `OrderRefundRecorder` (`IPaymentRefundHandler`) records every succeeded refund on its order, once, found through the refund's checkout session (Order, or Transaction whose reference is an order). `OrderOperationScope` carries the actor and the restock choice into it.
- Cancel: refund first (a failed refund leaves the order), cancel outstanding Pay Later transactions, cancel the order as `CanceledByStore` (allowed on a paid order only for that reason), then cancel an unpaid order's checkout so holds go back. `IInventoryService.RestockAsync` returns sold units with a `Returned` movement and marks the reservation `Returned`.
- Order Paid, Canceled and Refunded workflow events; `OrderConfirmationSender` shared by the checkout handler and the resend action.
- Verified live: a test-provider order of $236.50 refunded $50 from its page showed *Partially refunded*, a Refunded line, a Net line and the note *Refunded $50: Scratched strap* by listingadmin; canceling it refunded the other $186.50, showed *Refunded Canceled* and put the backpack back (7 to 8 on hand); the Pay Later order with LAST1 was resent, then canceled: its transaction became Canceled and LAST1 went back to 0 of 1 used; the workflow editor lists the three events.

### R1 Reports

- Seven `IReport`s in the Orders module (`[RequireFeatures(Reports)]`), Stock on hand in Inventory. They follow the Subscriptions pattern: a base class, a static `OrderReportAggregator` that is tested on its own, sections per currency.
- No new index: `OrderReportReader` pages through `OrderIndex` by `CreatedUtc` (from the period start less seven days, the longest payment lag) and keeps orders whose `PaidUtc` falls in the period; `OrderQuery.CreatedOnOrAfter` is new. Paid and canceled orders both count, so a refund reads as a refund. Refunds are listed by refund date for orders paid up to 180 days before the period.
- Orders to ship counts paid orders whose fulfillment is Unfulfilled or Partially fulfilled, or not recorded yet when the order has a shipping method (the status is only written from the first shipment).
- Verified live: Sales for today on the test store showed 4 orders, $624 gross, $33.70 discounts, $34 shipping, $62.43 tax, $686.73 charged, $236.50 refunded and $450.23 net, which match the four orders line by line; the other reports render with matching figures (both refunds listed, LAST1 and SAVE10 with their amounts, stock out-of-stock first).

### G1 Digital delivery

- New module `CrestApps.OrchardCore.Downloads` (feature *Digital Downloads*), one project. Files sit in a `FileSystemStore` under `App_Data/Sites/{tenant}/DigitalFiles/{fileId}/{fileName}`; ids are letters and digits only.
- The part editor uploads through an admin endpoint (`[RequestSizeLimit]` 1 GB, antiforgery token from the content form) rather than the content form, so large files aren't capped by the default request limit; saving keeps only ids the store knows, and takes name and size from the store.
- Entitlements per order line and file, granted in `IOrderEventHandler.PaidAsync` from the product version ordered, revoked on cancel and on a full refund (`RefundedAsync`, `RefundStatus == Refunded`).
- Links are data-protection signatures of the entitlement id, not stored tokens, so they can be shown again; the landing page is a GET and the download a POST, so mail link scanners don't spend downloads. Counting is under a per-entitlement lock.
- `IOrderEmailSectionProvider` (Orders.Core) lets modules add link sections to the confirmation email.
- Found while verifying: the admin Take back / Give back redirect lost the tenant prefix (`Request.Path` without `PathBase`).
- Verified live: the sample recipe made two ebooks; a PDF uploaded from the product editor; a guest bought it with no shipping step; the confirmation email had the link (3 downloads, 14 days); three POSTs returned the PDF and the fourth answered 410 *This download link no longer works*; Give downloads back restored it; a full refund from the order page revoked it (*Taken back: The order was refunded*) and the link answered 410 with the reason; the guest order page shows the refund and the downloads section.

### T1 Customer tax

- `ICheckoutTaxProfileHandler` (Checkout.Abstractions), run by `DefaultCheckoutTaxProfileProvider` after it builds the profile, at checkout and for recurring charges, so modules add to the profile without replacing the provider.
- Customers: `Customer.IsTaxExempt` and `TaxExemptionReason` (editor, recipe step, erasure); `CustomerCheckoutTaxProfileHandler` fills `CheckoutTaxProfile.Customer` for a signed-in buyer (B2B or B2C, tax id, exemption, certificate ids from the Taxation certificate store when present). Guests aren't trusted with an exemption.
- Taxation: `TaxOriginSettings` with a plain-field editor at Commerce > Taxation > Origin; Checkout's Taxation startup adds `TaxOriginCheckoutTaxProfileHandler`.
- Verified live: marking the store account's business customer exempt took the WA sales tax off a $129 watch with $6 shipping to Seattle ($135 due instead of $148.50); clearing it brought the tax back; the Origin settings page renders.
