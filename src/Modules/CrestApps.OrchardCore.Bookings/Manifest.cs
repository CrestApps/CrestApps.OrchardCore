using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Bookings;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Transactions;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Bookings",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version
)]

[assembly: Feature(
    Id = BookingsConstants.Feature,
    Name = "Bookings",
    Description = "Sells bookings: fixed departures such as tours and classes, nightly stays such as rooms and rentals, and time slots such as appointments, each with its capacity per date, prices per person or per room, a deposit with the balance charged to the saved card later, cancellation policies, requests the store confirms before the buyer pays, travelers' details, and a calendar of what's booked.",
    Category = "Commerce",
    Dependencies =
    [
        CommerceConstants.Features.Area,
        OrderConstants.Features.Orders,
        CustomersConstants.Features.Customers,
        TransactionsConstants.Features.Area,
    ]
)]

[assembly: Feature(
    Id = BookingsConstants.CalendarFeature,
    Name = "Bookings Calendar",
    Description = "Keeps every booking on a Bookings calendar: tentative while held or requested, confirmed once booked, moved when the booking moves and cancelled when it's canceled, so bookings show next to the rest of the calendar and count toward free/busy.",
    Category = "Commerce",
    Dependencies =
    [
        BookingsConstants.Feature,
        CalendarsConstants.Features.Calendars,
    ]
)]
