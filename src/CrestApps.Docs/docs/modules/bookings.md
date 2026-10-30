---
sidebar_label: Bookings
title: Bookings
description: "Bookings: products sold by date as fixed departures, nightly stays or time slots, capacity per date held like stock, occupancy pricing, deposits whose balance is charged to the saved card, cancellation policies, requests the store confirms, travelers' details, date search, a calendar, booking hooks and workflow events, and the Bookings Calendar feature."
user_manual: user-manual/commerce/bookings
---

| | |
| --- | --- |
| **Features** | `CrestApps.OrchardCore.Bookings` (Bookings), `CrestApps.OrchardCore.Bookings.Calendar` (Bookings Calendar) |
| **Category** | Commerce |
| **Depends on** | Commerce, Orders, Customers, Transactions; Bookings Calendar also needs Bookings and Calendars (`CrestApps.OrchardCore.Calendars`) |
| **Seams it fills** | `ICartLineHandler` (Carts), `IOrderEventHandler` (Orders), `ICheckoutTenderProvider` and two `CheckoutFlowDisplayDriver`s (Checkout), `ICartActionProvider` (Carts); with Bookings Calendar, a calendar type and `ICalendarAvailabilityService` (Calendars) |
| **Seams it offers** | `IBookingHandler`, `IBookingSlotFilter` |

The **Bookings** feature sells products by date. The attachable **Booking** part (`BookingPart`) makes a product a booking of one of three kinds (`BookingKind`):

- **Departure:** a fixed date with seats, such as a tour, an event or a class. The store adds its dates.
- **Nightly:** nights from a check-in to a check-out, such as a room or a rental.
- **TimeSlot:** a time on a day, such as an appointment, offered by opening hours.

Admin screens are described in the [User Manual](../user-manual/commerce/bookings.md). The **Bookings sample** recipe adds a tour, an appointment and a room.

## The Booking part

| Property | Meaning |
| --- | --- |
| `Kind` | Departure, Nightly or TimeSlot. |
| `Capacity` | How much of each date can be booked: seats on a departure, units a night, bookings in a time slot. A departure's own date can say otherwise. |
| `PricePerPerson` | Whether the product's price is per person (a tour) rather than per unit (a room). |
| `Occupancies` | `BookingOccupancy` (`Id`, `Label`, `People`, `PriceAdjustment`): what a unit holds, such as a double room for two, and what it adds or takes off the price once, such as a single supplement. |
| `MinNights`, `MaxNights`, `WeekendPrice` | A stay's length, and the price of a Friday or Saturday night. |
| `Hours`, `SlotMinutes` | When time slots run, by day of the week (`BookingOpeningHours`), and their length. |
| `DaysAhead` | How far ahead stays and time slots can be booked. |
| `DepositPercent`, `BalanceDueDaysBefore` | The share paid now, and how many days before the date the rest is due. |
| `CancellationTiers` | `BookingCancellationTier` (`DaysBefore`, `FeePercent`): what canceling costs, by how long before the date. |
| `RequestToBook` | Whether the store confirms each booking before it's paid. |
| `AskForTravelers` | Whether checkout asks each person's details from the `Traveler` content type. |

`CartOptions.ChooseOnProductPageParts` holds `BookingPart`, so a product card links to the product's page, where the date is chosen. The part's display for the `ProductAction` display type is the `BookingPicker` shape on the product's own page. Its fields post with the *Add to cart* form through the `form` attribute, as `details[booking.date]`, `details[booking.end]` and `details[booking.occupancy]`. `?date=` and `?end=` on the page choose them.

## Dates and capacity

`BookingSlot` (collection `Bookings`, store `IBookingSlotStore`, checked for concurrency) is one date of a product: `SlotKey` (`yyyy-MM-dd`, or `yyyy-MM-ddTHH:mm` for a time slot, in the site's time zone), `StartsUtc`, `Capacity`, `Held`, `Sold`, an optional `Price` in the product's currency, and `Closed`.

A departure is booked only on the dates the store added. A night or a time slot is made the first time it's held, at the part's capacity, so every date the rules offer can be booked. `BookingSchedule` holds the rules as pure functions:

- `GetSlotKeys` gives the dates a booking takes: the departure, every night of the stay, or the time.
- `Check` says why dates can't be booked: past, beyond `DaysAhead`, outside the stay length, or not an opening time.
- `GetTimes` lists a day's time slots.
- `GetUnitPrice` adds each date's price, for each person when the price is per person, and the occupancy's adjustment.
- `GetSeatsPerUnit` counts a departure's seats per person and other kinds' per unit.
- `GetCancellationFeePercent` and `GetRefund` work out a cancellation.

`BookingCapacityService` holds a booking's dates all or none (`HoldAsync`). It works under a distributed lock per product and writes before letting go, so the next checkout sees the hold. It turns a hold into seats taken when the order is paid (`CommitAsync`) and gives them back (`ReleaseAsync`). `GetRemainingAsync` counts what the cart's own unpaid orders hold as available to it, the way stock does.

## The cart line

`BookingCartLineHandler` is an `ICartLineHandler` (order 10, before add-ons):

- **Prepare:** keeps the date (and check-out and occupancy) after `BookingSchedule.Check`, or refuses the line.
- **Evaluate:** prices one unit from each date's own price, the weekend price or the product's price, converted from the product's currency with `CartLineEvaluateContext.ConvertAsync`. It sets `PeoplePerUnit`, so add-ons priced per person charge each one. It limits `MaxQuantity` to what's left on the fullest date, and describes the line ("Date: Wednesday, 9 December 2026", "Double room (2 people)").

## Orders and bookings

`BookingOrderEventHandler`, through `BookingService`, does the following:

- **Created:** makes a `Booking` of each booking line and holds its dates. The order is refused when they're gone, or when a product that's booked by request is checked out without a quote.
- **Paid:** confirms the bookings, takes their seats and emails the buyer (`BookingEmailSender`).
- **Canceled:** gives the dates back.
- **LinesChanged:** books the changed lines again.

`Booking` keeps the order and line, the dates (`SlotKeys`, `StartsUtc`, `EndsUtc`), `Units`, `People`, `Seats`, the occupancy, the buyer, the price, the travelers' details and the cancellation policy as it was. `BookingStatus` is one of:

- **Held:** an unpaid order, released after `BookingService.HoldTime` (60 minutes).
- **Requested:** held until the quote is declined or expires.
- **Confirmed**, **Completed** or **NoShow**.
- **Canceled** or **Expired**.

`BookingsBackgroundTask` runs every five minutes. It releases expired holds and charges balances that are due.

The handler resolves `BookingService` when an order changes rather than when it's built, since the booking service writes notes through `IOrderService`, which is built with the handlers.

## Deposits and the balance

`BookingDepositService.GetOfferAsync` works out a checkout's deposit. The balance is each booking line's total less its deposit. It's due at 09:00 local `BalanceDueDaysBefore` days before the earliest booking that takes a deposit, and a booking whose due date has come is paid in full.

`BookingDepositCheckoutFlowDisplayDriver` shows **Pay a deposit** on the Payment step. Applying it chooses the `booking-balance` tender and sets `CheckoutSession.SavePaymentMethod`, so the card is kept. `ICheckoutTenderProvider.PaysWithSavedPaymentMethod` lets this tender alone pay toward a checkout that keeps the card.

`BookingBalanceTenderProvider.DebitAsync` puts the balance on an outstanding ledger transaction, with the transactions' reminders and pay link, due on that date. It keeps a `BookingBalance` record of the transaction and the deposit's checkout session. `CreditAsync` takes refunds off what's owed.

`BookingBalanceCharger.ChargeAsync` charges the kept card through the checkout without the buyer present, as installments do:

1. It reads the card through `ICheckoutSavedPaymentMethodProvider` from the deposit's succeeded attempts.
2. It starts a checkout on the transaction, and begins it with `CheckoutPaymentDataKeys.ForOffSessionCharge`.

A declined card is tried again the next day, three times in all. The charger reads the balance again after the checkout commits: saving the document read before that commit would store it twice.

## Cancellation

`BookingService.QuoteCancellationAsync` works out a cancellation:

- **Fee:** the policy's percent for the days left before the date.
- **Paid:** the booking's share of what can be refunded for the order. The balance tender's amount isn't counted, since it was never paid.
- **Refund:** what was paid less the fee.

`CancelAsync` does the cancellation:

1. Refunds through `IOrderRefunder` (Orders.Core, provided by the Orders feature), or staff's own amount.
2. Waives what's still owed on the balance.
3. Gives the dates back, notes the order and emails the buyer.

## Requests to book

`BookingCartActionProvider` adds **Request to book** to a cart holding a booking that `RequestToBook` marks. `BookingsController` (`bookings/request`) calls `IOrderRequestService` (Orders.Core). It makes the same quote as a quote request, taken whether or not the store takes quote requests. The booking is held as *Requested*. The store confirms it by sending the quote from the draft order, which re-prices the lines with their details, and the buyer pays from its link.

## Travelers

`BookingTravelersCheckoutFlowDisplayDriver` adds editors to the Contact step: one `Traveler` content item for each person of each booking whose part asks for travelers, when the type has fields. Only the items' field zones show. Their content is kept on `Booking.Travelers` once the step is valid, and shown on the booking's admin page.

## Search and the calendar

`bookings` (`BookingSearch`) finds what has room for a number of people between two dates:

- Departures on those days.
- Stays of those nights, as rooms enough for the people.
- Times on the first day.

Each result links to the product with its date. **Commerce > Bookings > Calendar** shows each day's dates with what's taken.

## Booking hooks

`IBookingHandler` lets other features follow bookings. `BookingStore` runs every handler when a `Booking` is about to be saved (`SavingAsync(BookingSavingContext)`) or deleted (`DeletingAsync(Booking)`). A handler runs inside the save, so what it writes is committed with the booking; for work that must only happen after the commit, add a `ShellScope` deferred task, as the workflow handler does.

`BookingSavingContext` holds the `Booking`, `IsNew`, and what it was when it was last saved: `PreviousStatus`, `PreviousStartsUtc` and `PreviousEndsUtc`, with `StatusChanged` and `Rescheduled` worked out from them. The store keeps that snapshot on the booking itself (`BookingChangeMarker` in its properties) after each save. A booking saved before the hooks existed has no snapshot, so its previous values are `null` on its first save.

The store resolves the handlers on first use, so a handler may depend on services that depend on the store.

```csharp
internal sealed class BookingAuditHandler : IBookingHandler
{
    public Task SavingAsync(BookingSavingContext context)
    {
        if (context.StatusChanged && context.Booking.Status == BookingStatus.Canceled)
        {
            // React to a cancellation, inside the same session.
        }

        return Task.CompletedTask;
    }

    public Task DeletingAsync(Booking booking)
        => Task.CompletedTask;
}

// In a startup:
services.AddScoped<IBookingHandler, BookingAuditHandler>();
```

`IBookingSlotFilter` closes some of a product's dates or times for a reason Bookings doesn't track itself. `GetClosedSlotKeysAsync(contentItemId, part, slotKeys, timeZone)` returns which of the slot keys (`yyyy-MM-dd` or `yyyy-MM-ddTHH:mm`, in the given time zone) are closed. `BookingCapacityService` asks every filter in `GetRemainingAsync`, so a closed date shows as full, and again in `HoldAsync`, so it can't be held.

## Workflow events

With `OrchardCore.Workflows` enabled, Bookings adds these events in the **Commerce** category. `BookingWorkflowHandler` (an `IBookingHandler`) picks them when a booking is saved and triggers them in a deferred task once the session commits, correlated by the booking id. A save can raise more than one, such as **Booking Created** and **Booking Confirmed** for a booking paid at once.

| Event | Raised when |
| --- | --- |
| Booking Created (`BookingCreatedEvent`) | A booking is made (held, requested or confirmed). |
| Booking Confirmed (`BookingConfirmedEvent`) | A booking becomes `Confirmed`, such as when its order is paid. |
| Booking Rescheduled (`BookingRescheduledEvent`) | A booking moves to other dates. |
| Booking Canceled (`BookingCanceledEvent`) | A booking becomes `Canceled`. |
| Booking Completed (`BookingCompletedEvent`) | A booking is marked complete. |
| Booking No-Show (`BookingNoShowEvent`) | A booking is marked as a no-show. |
| Booking Expired (`BookingExpiredEvent`) | A hold or request runs out. |

The input of every event:

| Key | Value |
| --- | --- |
| `Booking` | The `Booking` record. |
| `BookingId` | Its id. |
| `OrderId`, `OrderNumber` | The order it belongs to. |
| `ContentItemId`, `Title` | The product. |
| `StartsUtc`, `EndsUtc` | When it starts and ends. |
| `Status`, `PreviousStatus` | Its status now and at its last save. |
| `CustomerName`, `CustomerEmail` | The buyer. |

## Bookings Calendar

The **Bookings Calendar** feature (`CrestApps.OrchardCore.Bookings.Calendar`, `CalendarStartup`) puts bookings on the shared [calendar](./calendars.md) and lets a calendar close booking dates.

**One calendar for every booking.** The feature registers the `Bookings` calendar type, managed by code (`ManagedByCode()`, so it isn't in **Add calendar**), with one event type, `Booking`. `BookingCalendarHandler` is an `IBookingHandler`: on every booking save it creates, moves or cancels the booking's calendar event on the **Bookings** calendar, which it creates the first time (`Source = Bookings`, `SourceId = Bookings`, found again with `ICalendarService.FindBySourceAsync`). The event:

- has `Source = Bookings`, `SourceId` = the booking id and `Editable = false`, so it can only be changed by Bookings;
- is titled with the product and the buyer's name, starts and ends with the booking, is busy, and names the buyer as an attendee;
- is `Tentative` while the booking is held or requested, `Confirmed` once booked, and `Cancelled` when the booking is canceled or expires;
- is deleted with the booking.

The handler writes with `SkipConflictCheck` (Bookings checks capacity itself) and `SendInvitations = false`, inside the booking's session. The calendar is private by default, so only calendar administrators and the people it is shared with see it.

**Sync to calendar.** `BookingCalendarController.Sync` (`bookings/calendar/sync`, **Manage bookings**) adds or updates the event of every booking from today on, held ones included, for bookings made before the feature was on. It changes only what differs, so running it again is harmless. **Commerce > Bookings > Calendar** shows a **Sync to calendar** button, and **Open in calendar** (the calendar view on the Bookings calendar, same month) once the calendar exists.

**Block with calendar.** The feature adds a **Block with calendar** setting to the Booking part (`BookingPart.BlockingCalendarId`, edited by `BookingPartCalendarDisplayDriver`), listing every calendar except the Bookings calendar itself. `BookingCalendarSlotFilter` is its `IBookingSlotFilter`: it reads busy time on that calendar through `ICalendarAvailabilityService.GetBusyAsync`, tentative events included and busy-time sources left out. A time slot is closed when anything busy overlaps it; a departure date or a night is closed when anything busy falls on that day. Use it to keep a room, a guide or a vehicle from being sold twice across products, or to close dates that a team calendar marks as out of office.

## Recipes

The `BookingDates` recipe step adds dates to booking products: each with `ContentItemId`, `Date` (or `DaysFromToday`), an optional `Time`, `Capacity` and `Price`. A date the product has already is left as it is.

## Report Builder data source

With **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) on, which the Report Builder needs, Bookings adds the **Bookings** data source (`Bookings`) to the Report Builder and to Orchard Core data pipelines. Its data sets need **Manage bookings** (`ManageBookings`), the permission of **Commerce > Bookings**. The travelers' details, the reason a booking was canceled, the cancellation policy, the card a balance is charged to and the payment provider's error messages are never exposed.

The **Bookings** data set (`Bookings`) has one row per booking, newest first by when it was booked, which is its default date. Date ranges on that date, joins on the booking, order, product and cart IDs, and status filters are applied in the database, and simple groups and totals of those fields and of the start date are counted there too. Money is in the booking's currency, which the `Currency` field names.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Booking ID | Identifies the booking. |
| `Status` | Status | `Held`, `Requested`, `Confirmed`, `Completed`, `NoShow`, `Canceled` or `Expired`. |
| `Kind` | Kind | `Departure`, `Nightly` or `TimeSlot`. |
| `ProductId` | Product ID | References the product (`Products` > `Products` > `ContentItemId`). |
| `Title` | Product | |
| `OccupancyLabel` | Occupancy | Such as a double room. |
| `Units` | Units | Such as rooms. |
| `People` | People | |
| `Seats` | Seats | How much of each date it takes: seats for a departure, units otherwise. |
| `DateCount` | Dates taken | One for a departure or a time slot, one per night of a stay. |
| `TravelerCount` | Travelers named | How many travelers' details the buyer gave. |
| `OrderId` | Order ID | References `Orders` > `Orders`. |
| `OrderNumber` | Order number | |
| `LineId` | Order line ID | References `Orders` > `OrderLines` > `LineId`. |
| `CartId` | Cart ID | References `Carts` > `Carts`. |
| `UserId` | User ID | References the user (`Users`). |
| `Currency` | Currency | |
| `Total` | Price | |
| `CancellationFee` | Cancellation fee | What the cancellation policy kept; empty unless it was canceled. |
| `Refunded` | Refunded | What was refunded when it was canceled. |
| `CreatedUtc` | Booked | The default date. |
| `StartsUtc` | Starts | |
| `EndsUtc` | Ends | A stay's check-out. |
| `HoldExpiresUtc` | Hold ends | |
| `ConfirmedUtc`, `CompletedUtc`, `CanceledUtc` | Confirmed, Completed, Canceled | |
| `ModifiedUtc` | Last changed | |

The **Booking dates** data set (`BookingSlots`) has one row per date of a booking product (a departure, a night or a time slot), newest first by when it starts, which is its default date. Date ranges on that date and joins on the date and product IDs are applied in the database, and simple groups and totals of the product, date, start, places left and closed fields are counted there too. A date's own price is in the product's currency, which the date doesn't record, so only whether it has one is exposed.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Date ID | Identifies the date. |
| `ProductId` | Product ID | References the product (`Products` > `Products` > `ContentItemId`). |
| `SlotKey` | Date or time | The date (`yyyy-MM-dd`) or the time (`yyyy-MM-ddTHH:mm`) in the site's time zone. |
| `StartsUtc` | Starts | The default date. |
| `Capacity` | Capacity | |
| `Held` | Held | How much unpaid orders hold. |
| `Sold` | Sold | How much paid bookings take. |
| `Remaining` | Left | |
| `Closed` | Closed | |
| `HasOwnPrice` | Has its own price | |
| `ModifiedUtc` | Last changed | |

The **Booking balances** data set (`BookingBalances`) has one row per balance left to pay on an order whose bookings were paid by deposit, newest first by when it is due, which is its default date. Date ranges on that date, joins on the balance and order IDs, and status filters are applied in the database, and simple groups and totals of those fields are counted there too. Money is in the balance's currency, which the `Currency` field names.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Balance ID | Identifies the balance. |
| `Status` | Status | `Scheduled`, `Paid`, `Failed` or `Canceled`. |
| `Currency` | Currency | |
| `Amount` | Amount | |
| `Attempts` | Charge attempts | |
| `OrderId` | Order ID | References `Orders` > `Orders`. |
| `OrderNumber` | Order number | |
| `TransactionId` | Transaction ID | The ledger transaction the balance is owed on; references `Transactions` > `Transactions`. |
| `DepositSessionId` | Deposit checkout ID | The checkout that paid the deposit; references `Checkout` > `CheckoutSessions`. |
| `UserId` | User ID | References the user (`Users`). |
| `DueUtc` | Due | The default date. |
| `LastAttemptUtc` | Last charge attempt | |
| `NextAttemptUtc` | Next charge attempt | |
| `CreatedUtc` | Created | |
