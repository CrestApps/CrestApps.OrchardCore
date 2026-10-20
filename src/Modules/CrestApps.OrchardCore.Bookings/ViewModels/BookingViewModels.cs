using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Bookings.ViewModels;

/// <summary>
/// The Booking part's editor. Amounts are text, read in the invariant culture, so an admin in any language enters
/// 24.50 the same way.
/// </summary>
public class BookingPartViewModel
{
    public BookingKind Kind { get; set; }

    public int Capacity { get; set; }

    public bool PricePerPerson { get; set; }

    public List<BookingOccupancyViewModel> Occupancies { get; set; } = [];

    public int MinNights { get; set; }

    public int MaxNights { get; set; }

    public string WeekendPrice { get; set; }

    public List<BookingHoursViewModel> Hours { get; set; } = [];

    public int SlotMinutes { get; set; }

    public int DaysAhead { get; set; }

    public string DepositPercent { get; set; }

    public int BalanceDueDaysBefore { get; set; }

    public List<BookingCancellationTierViewModel> CancellationTiers { get; set; } = [];

    public bool RequestToBook { get; set; }

    public bool AskForTravelers { get; set; }

    [BindNever]
    public string ContentItemId { get; set; }

    [BindNever]
    public string Currency { get; set; }
}

public class BookingOccupancyViewModel
{
    public string Id { get; set; }

    public string Label { get; set; }

    public int? People { get; set; }

    public string PriceAdjustment { get; set; }
}

public class BookingHoursViewModel
{
    public DayOfWeek Day { get; set; }

    public bool Open { get; set; }

    public string Opens { get; set; }

    public string Closes { get; set; }
}

public class BookingCancellationTierViewModel
{
    public int? DaysBefore { get; set; }

    public string FeePercent { get; set; }
}

/// <summary>
/// The date picker on a booking product's page, whose fields post with its Add to cart form.
/// </summary>
public class BookingPickerViewModel
{
    public string FormId { get; set; }

    public string Title { get; set; }

    public BookingKind Kind { get; set; }

    public IList<BookingPickerOption> Departures { get; set; } = [];

    public IList<(string Day, IList<BookingPickerOption> Times)> Days { get; set; } = [];

    public IList<BookingOccupancy> Occupancies { get; set; } = [];

    public string MinDate { get; set; }

    public string MaxDate { get; set; }

    public int MinNights { get; set; }

    public int MaxNights { get; set; }

    public string SelectedDate { get; set; }

    public string SelectedEnd { get; set; }

    public bool PricePerPerson { get; set; }

    public bool RequestToBook { get; set; }

    public decimal DepositPercent { get; set; }

    public int BalanceDueDaysBefore { get; set; }

    public IList<BookingCancellationTier> CancellationTiers { get; set; } = [];
}

/// <summary>
/// A date or time a buyer can pick: its key, how it reads, and what's left.
/// </summary>
public sealed record BookingPickerOption(string Key, string Label, int Remaining, string Price);

public class BookingsIndexViewModel
{
    public string Search { get; set; }

    public BookingStatus? Status { get; set; }

    public bool Upcoming { get; set; }

    [BindNever]
    public AdminListContext<Booking> List { get; set; }
}

public class BookingDetailViewModel
{
    public Booking Booking { get; set; }

    public BookingBalance Balance { get; set; }

    public BookingCancellationQuote Cancellation { get; set; }

    public TimeZoneInfo TimeZone { get; set; }

    public IList<IShape> Travelers { get; set; } = [];

    public string OrderUrl { get; set; }
}

public class BookingDatesViewModel
{
    public string ContentItemId { get; set; }

    public string Title { get; set; }

    public BookingPart Part { get; set; }

    public string Currency { get; set; }

    public IList<BookingSlot> Slots { get; set; } = [];

    public string NewDate { get; set; }

    public string NewTime { get; set; }

    public int? NewCapacity { get; set; }

    public string NewPrice { get; set; }
}

public class BookingCalendarViewModel
{
    public DateOnly Month { get; set; }

    public IDictionary<DateOnly, IList<BookingCalendarEntry>> Days { get; set; } = new Dictionary<DateOnly, IList<BookingCalendarEntry>>();
}

/// <summary>
/// What's on a day of the calendar: a product, when, and how much of it is taken.
/// </summary>
public sealed record BookingCalendarEntry(string ContentItemId, string Title, string Time, int Taken, int Capacity, bool Closed);

public class BookingSearchViewModel
{
    public string From { get; set; }

    public string To { get; set; }

    public int People { get; set; } = 1;

    public bool Searched { get; set; }

    public IList<BookingSearchResult> Results { get; set; } = [];
}

/// <summary>
/// A booking product with room on the dates searched for, and its first date that has.
/// </summary>
public sealed record BookingSearchResult(string ContentItemId, string Title, string Url, string FirstDate, string FirstDateLabel, int Remaining);

public class BookingTravelersViewModel
{
    public IList<(string Title, IList<IShape> Editors)> Bookings { get; set; } = [];
}

public class BookingTravelerData
{
    public JsonObject Content { get; set; }
}
