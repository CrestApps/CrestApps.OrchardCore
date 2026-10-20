---
sidebar_label: Point of Sale
title: Point of Sale
description: Registers at inventory locations, shifts with a counted cash drawer, counter sales as orders on the checkout framework with a cash tender and a counter-card payment method, receipts, returns at the till, and the point-of-sale API a till app calls, with Stripe Terminal readers.
user_manual: user-manual/commerce/point-of-sale
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.PointOfSale` (Point of Sale) |
| **Category** | Commerce |
| **Depends on** | Commerce, Orders, Inventory, Checkout API |
| **Works with** | Receipts (printed and emailed receipts), Customers (a named buyer), Gift Cards, Stripe (Terminal readers) |

The **Point of Sale** feature sells at a store's counter from a till app over the [commerce API](../user-manual/commerce/headless-api.md). A sale is an order on the ordinary checkout framework, so prices, coupons, gift cards, tax, stock holds, order events and reports work as they do online. The module adds what a counter needs: registers, shifts with their cash, a cash tender, a payment method for the store's own card terminal, receipts, and returns at the till.

## Model

Registers and shifts live in the `PointOfSale` YesSql collection.

- **`Register`** (`RegisterIndex`): `Name`, `LocationId` (an inventory location), `Address` (an `Address`, which sales are taxed by and billed to), `Currency` (empty sells in the products' own currency), `Roles` (empty lets anyone who may sell at a register), `IsEnabled`, `CreatedUtc`, `ModifiedUtc`.
- **`RegisterShift`** (`RegisterShiftIndex`): `RegisterId`, `RegisterName`, `LocationId`, `Currency`, `Status` (`Open` or `Closed`), who opened and closed it and when, `OpeningFloat`, `CashMovements` (each a `RegisterCashMovement`: `Amount`, positive in and negative out, `Reason`, who and when), and at the close `CountedCash`, `ExpectedCash`, `CloseNote` and `Summary`.
- **`RegisterShiftSummary`**: `Sales`, `Total`, `Cash` (kept: handed over less change), `Card`, `Other` (gift cards and other balances) and `Refunded` (refunds of the shift's sales). `PosShiftService.SummarizeAsync` works it out from the shift's paid orders (`OrderQuery.ShiftId`); a closed shift keeps it.

`RegisterShift.GetExpectedCash(cashKept)` is the float, plus the cash kept for sales, plus the cash movements.

## Orders and the checkout

`PosSaleService.StartAsync` prices the lines as a cart (`ICartService.PriceAsync`, without saving a cart), and makes the order with `IOrderService.CreateFromCartAsync`, setting:

| Order field | Value |
| --- | --- |
| `Channel` | `OrderChannels.PointOfSale` (`pos`) |
| `LocationId` | The register's location, which Inventory holds and sells the stock at (`InventoryOrderEventHandler`, falling back to the main location for an item the store has none of) |
| `RegisterId`, `RegisterName`, `ShiftId` | The register and its open shift |
| `StaffUserId`, `StaffName` | Who rang it up |
| `CustomerId`, `UserId`, customer name, email and phone | The buyer, when one is named |
| `BillingAddress` | The register's address |

It commits that, then starts the order's checkout with `ICheckoutEngine.StartAsync` and puts on the session:

- **`CheckoutCounterSale`** (`RegisterId`, `ShiftId`, `LocationId`), which marks it a counter sale;
- a `CheckoutShippingInfo` for an *In store* rate of zero at the register's address, so the invoice is taxed where it's sold;
- the buyer's `CheckoutContactInfo`, when they have an email.

`OrderChannels.IsCounterSale(order)` makes the Customers' **Contact** step and Shipping's **Shipping** step drop out. Store credit and loyalty points aren't offered for a counter sale. The session belongs to the signed-in staff member who started it.

### Paying

`PosSaleService.PayAsync` pays in this order:

1. **Cash.** `PosCashTenderProvider` (`ICheckoutTenderProvider`, key `cash`) is a balance whose reference is the cash handed over in the currency's minor units (`PosCashTenderProvider.ToReference`). It pays up to what's due, after tax like a gift card, and only on a session with `CheckoutCounterSale`. The change is what was handed over less what the cash tenders paid (`PosSaleService.GetChange`).
2. **A card**, for what's left: `PosCounterCardPaymentProvider` (`ICheckoutPaymentProvider` and `ICheckoutPaymentRefundProvider`, key `pos-card`, *Card on the counter terminal*) records a card the store's own terminal charged; its reference is the approval code given (`pos_card_` and the code), and it verifies as paid at once. A refund through it records the refund; staff make it on the terminal. Or `StripeTerminalPaymentProvider` (key `stripe-terminal`, below), given a reader's id.
3. **Completion.** `ICheckoutEngine.TryCompleteAsync`. A reader payment answers pending until Stripe reports the PaymentIntent succeeded.

### Receipts

`PosReceipts.ToApi` gives a receipt as data (`ApiPosReceipt`: lines, totals, payments, change) in the completed payment's answer. With the Receipts feature on, `PosReceipts.RenderAsync` builds an `IReceiptService` document (source *Point of sale*, the payments in its notes) and renders it with `IReceiptHtmlRenderer`, for `GET pos/sales/{orderId}/receipt` and its email through `IEmailService`.

### Returns

`POST pos/returns` refunds a paid order through `OrderOperationsService.RefundAsync` (to how it was paid, restocking at the order's location unless `restock` is `false`). The refund goes to the card first and to the sale's cash last, so the cash handed back is what the refund put back on the sale's cash tenders (read from `ICheckoutTenderLedger` before and after): it's recorded as a negative cash movement on the register's open shift, which a return requires. Only register sales are returned at a till, unless the caller has **Manage orders**.

## API

`PosApiEndpoints` maps these into the commerce JSON API at `api/commerce/v1`, under the tag **Point of Sale**. Each needs a signed-in caller who may sell at the register concerned (`PosShiftService.MaySellAtAsync`: the register is on, the caller has **Sell at a register**, and is in one of its roles or has **Manage registers and shifts**); another register answers `404`.

| Method | Path | What it does |
| --- | --- | --- |
| `GET` | `pos/registers` | The registers the caller may sell at (`ApiRegister`, with `openShiftId`). |
| `POST` | `pos/registers/{registerId}/shifts` | Opens a shift (`openingFloat`); `409 shift-open` when one is open. Idempotent. |
| `GET` | `pos/shifts/{shiftId}` | The shift (`ApiShift`), with its summary and expected cash. |
| `POST` | `pos/shifts/{shiftId}/cash` | A cash movement (`amount`, `reason`); `409 shift-closed`. Idempotent. |
| `POST` | `pos/shifts/{shiftId}/close` | Closes it with `countedCash` and `note`. |
| `POST` | `pos/sales` | Rings up `lines` (`PosSaleLine`: `productId`, `variantId`, `priceId`, `quantity`, `addOns`) for a `customerId`, or a `customerEmail` and `customerName`; `201` with `orderId`, `orderNumber` and the `checkout` (`ApiCheckout`). Idempotent. |
| `POST` | `pos/sales/{checkoutId}/pay` | `cash`, then `card` (`approvalCode`) or `reader` (`readerId`); answers `ApiPosPayment` with `status` `completed`, `due` or `pending`, `amountDue`, `change` and, once completed, `receipt`. Idempotent. |
| `GET` | `pos/sales/{orderId}/receipt` | The receipt's HTML; `409 unsupported` without Receipts. |
| `POST` | `pos/sales/{orderId}/receipt/email` | Emails it to `email`; `204`. |
| `POST` | `pos/returns` | `orderId`, `registerId`, `amount`, `reason`, `restock`; answers `refunded`, `cashBack` and the refund's `messages`. Idempotent. |

Codes and gift cards go on the sale's checkout through the [checkout endpoints](checkout#the-commerce-api), and `DELETE checkout/{id}` cancels an unpaid sale.

## Stripe Terminal

With the [Stripe](payments) feature and Point of Sale both on, Stripe's `TerminalStartup` adds `StripeTerminalPaymentProvider` (`ICheckoutPaymentProvider` and `ICheckoutPaymentRefundProvider`, key `stripe-terminal`, *Card on a Stripe reader*), offered only for a session with `CheckoutCounterSale`. It uses Stripe's server-driven integration, so the till app needs no Stripe SDK:

- **Begin** creates a `card_present` PaymentIntent for the attempt's amount and tax (idempotency key `terminal-pi-` and the attempt id, or the attempt's existing PaymentIntent when begun again) and tells the reader in `ProviderData["readerId"]` to process it.
- **Verify** reads the PaymentIntent: `succeeded` is paid, `canceled` failed, anything else still waiting.
- **Cancel** cancels the PaymentIntent; **refund** goes through `IStripeRefundService`.

`StripeTerminalApiEndpoints` adds, under the same tag:

| Method | Path | Permission | What it does |
| --- | --- | --- | --- |
| `GET` | `pos/terminal/readers` | Sell at a register | Lists the account's readers (`ApiStripeReader`: `id`, `label`, `deviceType`, `status`, `locationId`, `action`, `actionStatus`). |
| `POST` | `pos/terminal/readers` | Manage registers and shifts | Pairs a reader by `registrationCode`, with a `label`, at `stripeLocationId` or a Stripe location made from `address`. `simulated-wpe` pairs a simulated reader in test mode. |
| `POST` | `pos/terminal/readers/{readerId}/test-card` | Sell at a register | In test mode, presents a test card on a simulated reader (`TestHelpers.Terminal.ReaderService.PresentPaymentMethodAsync`); `409 unsupported` on a live account. |

## Admin

- **Commerce > Point of sale > Registers** (`point-of-sale/registers`, `RegistersAdminController`) lists registers through the `AdminList` shape, with **Add register** and each register's editor.
- **Commerce > Point of sale > Shifts** (`point-of-sale/shifts`) lists shifts, newest first, with a register filter in the list toolbar; a shift's page shows its sales by tender and its cash drawer (float, put in less taken out, expected, counted, difference), its cash movements and its close note.

## Permissions

| Permission | Allows |
| --- | --- |
| **Sell at a register** (`SellAtRegister`) | The point-of-sale API at the registers the user's roles may use: shifts, sales, payments, receipts and returns. |
| **Manage registers and shifts** (`ManageRegisters`) | The registers and shifts admin pages, selling at every register, and pairing Stripe readers. |

Administrators have both by default.

## Related modules

- [Carts and Orders](carts-and-orders#sales-channels): the order's channel, location, register and shift.
- [Inventory](inventory): the locations registers sell from.
- [Checkout](checkout): the checkout a sale is paid through, and balances that pay after tax.
- [Payments](payments#stripe-terminal-readers): Stripe Terminal readers.
- [Receipts](receipts): the receipt's layout.
