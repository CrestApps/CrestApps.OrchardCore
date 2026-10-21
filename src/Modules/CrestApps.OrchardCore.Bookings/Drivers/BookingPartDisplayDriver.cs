using System.Globalization;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Bookings.ViewModels;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Bookings.Drivers;

/// <summary>
/// The Booking part: its editor, and the date picker on its product's page, whose fields post with Add to cart.
/// </summary>
internal sealed class BookingPartDisplayDriver : ContentPartDisplayDriver<BookingPart>
{
    private const string ProductActionDisplayType = "ProductAction";
    private const int OccupancyRows = 4;
    private const int TierRows = 4;

    private readonly BookingCapacityService _capacity;
    private readonly IBookingSlotStore _slots;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly IShopperCurrency _shopperCurrency;

    internal readonly IStringLocalizer S;

    public BookingPartDisplayDriver(
        BookingCapacityService capacity,
        IBookingSlotStore slots,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<BookingPartDisplayDriver> stringLocalizer,
        IShopperCurrency shopperCurrency)
    {
        _capacity = capacity;
        _slots = slots;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _shopperCurrency = shopperCurrency;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(BookingPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != ProductActionDisplayType ||
            _httpContextAccessor.HttpContext?.Request.RouteValues["contentItemId"] is not string shown ||
            shown != part.ContentItem.ContentItemId)
        {
            return null;
        }

        return Initialize<BookingPickerViewModel>("BookingPicker", model => BuildPickerAsync(part, model))
            .Location(ProductActionDisplayType, "Actions:4");
    }

    public override IDisplayResult Edit(BookingPart part, BuildPartEditorContext context)
        => Initialize<BookingPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Kind = part.Kind;
            model.Capacity = part.Capacity;
            model.PricePerPerson = part.PricePerPerson;
            model.Occupancies = [.. (part.Occupancies ?? []).Select(occupancy => new BookingOccupancyViewModel
            {
                Id = occupancy.Id,
                Label = occupancy.Label,
                People = occupancy.People,
                PriceAdjustment = occupancy.PriceAdjustment == 0m ? null : occupancy.PriceAdjustment.ToString(CultureInfo.InvariantCulture),
            })];

            while (model.Occupancies.Count < OccupancyRows)
            {
                model.Occupancies.Add(new BookingOccupancyViewModel());
            }

            model.MinNights = part.MinNights;
            model.MaxNights = part.MaxNights;
            model.WeekendPrice = part.WeekendPrice?.ToString(CultureInfo.InvariantCulture);
            model.Hours = [.. Enum.GetValues<DayOfWeek>().Select(day => (part.Hours ?? []).FirstOrDefault(hours => hours.Day == day) is { } hours
                ? new BookingHoursViewModel { Day = day, Open = true, Opens = hours.Opens, Closes = hours.Closes }
                : new BookingHoursViewModel { Day = day, Opens = "09:00", Closes = "17:00" })];
            model.SlotMinutes = part.SlotMinutes;
            model.DaysAhead = part.DaysAhead;
            model.DepositPercent = part.DepositPercent == 0m ? null : part.DepositPercent.ToString(CultureInfo.InvariantCulture);
            model.BalanceDueDaysBefore = part.BalanceDueDaysBefore;
            model.CancellationTiers = [.. (part.CancellationTiers ?? []).OrderByDescending(tier => tier.DaysBefore).Select(tier => new BookingCancellationTierViewModel
            {
                DaysBefore = tier.DaysBefore,
                FeePercent = tier.FeePercent.ToString(CultureInfo.InvariantCulture),
            })];

            while (model.CancellationTiers.Count < TierRows)
            {
                model.CancellationTiers.Add(new BookingCancellationTierViewModel());
            }

            model.RequestToBook = part.RequestToBook;
            model.AskForTravelers = part.AskForTravelers;
            model.ContentItemId = context.IsNew ? null : part.ContentItem.ContentItemId;
            model.Currency = part.ContentItem.TryGet<ProductPart>(out var product) ? product.Currency : null;
        });

    public override async Task<IDisplayResult> UpdateAsync(BookingPart part, UpdatePartEditorContext context)
    {
        var model = new BookingPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Kind = Enum.IsDefined(model.Kind) ? model.Kind : BookingKind.Departure;
        part.Capacity = Math.Max(0, model.Capacity);
        part.PricePerPerson = model.PricePerPerson;
        part.MinNights = Math.Max(1, model.MinNights);
        part.MaxNights = Math.Max(part.MinNights, model.MaxNights);
        part.SlotMinutes = Math.Clamp(model.SlotMinutes, 5, 24 * 60);
        part.DaysAhead = Math.Clamp(model.DaysAhead, 1, 730);
        part.BalanceDueDaysBefore = Math.Max(0, model.BalanceDueDaysBefore);
        part.RequestToBook = model.RequestToBook;
        part.AskForTravelers = model.AskForTravelers;

        // An occupancy keeps its id, so carts holding it still match.
        var occupancies = new List<BookingOccupancy>();

        foreach (var row in (model.Occupancies ?? []).Where(row => !string.IsNullOrWhiteSpace(row.Label)))
        {
            if (!TryParse(row.PriceAdjustment, out var adjustment, allowNegative: true))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Occupancies), S["Enter an option's price change as a number, such as 300 or -150."]);
            }

            occupancies.Add(new BookingOccupancy
            {
                Id = string.IsNullOrEmpty(row.Id) ? global::OrchardCore.IdGenerator.GenerateId() : row.Id,
                Label = row.Label.Trim(),
                People = Math.Clamp(row.People ?? 1, 1, 50),
                PriceAdjustment = adjustment,
            });
        }

        part.Occupancies = occupancies;

        if (!TryParse(model.WeekendPrice, out var weekend, allowNegative: false))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.WeekendPrice), S["Enter the weekend price as a number, such as 140."]);
        }

        part.WeekendPrice = string.IsNullOrWhiteSpace(model.WeekendPrice) ? null : weekend;

        if (!TryParse(model.DepositPercent, out var deposit, allowNegative: false) || deposit >= 100m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DepositPercent), S["Enter the deposit as a percent below 100, such as 15, or leave it empty to be paid in full."]);
        }

        part.DepositPercent = Math.Clamp(deposit, 0m, 99m);

        part.Hours = [.. (model.Hours ?? [])
            .Where(hours => hours.Open && IsTime(hours.Opens) && IsTime(hours.Closes) && string.CompareOrdinal(hours.Opens, hours.Closes) < 0)
            .Select(hours => new BookingOpeningHours { Day = hours.Day, Opens = hours.Opens, Closes = hours.Closes })];

        if (part.Kind == BookingKind.TimeSlot && part.Hours.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Hours), S["Open at least one day, with when its first slot starts and its last one ends."]);
        }

        var tiers = new List<BookingCancellationTier>();

        foreach (var row in (model.CancellationTiers ?? []).Where(row => row.DaysBefore is not null || !string.IsNullOrWhiteSpace(row.FeePercent)))
        {
            if (row.DaysBefore is not >= 0 || !TryParse(row.FeePercent, out var fee, allowNegative: false) || fee > 100m)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.CancellationTiers), S["Give each cancellation rule its days before the date and a fee from 0 to 100 percent."]);

                continue;
            }

            tiers.Add(new BookingCancellationTier { DaysBefore = row.DaysBefore.Value, FeePercent = fee });
        }

        part.CancellationTiers = [.. tiers.OrderByDescending(tier => tier.DaysBefore)];

        return Edit(part, context);
    }

    private async ValueTask BuildPickerAsync(BookingPart part, BookingPickerViewModel model)
    {
        var item = part.ContentItem;
        var timeZone = await _capacity.GetTimeZoneAsync();
        var localNow = await _capacity.GetLocalNowAsync();
        var today = DateOnly.FromDateTime(localNow);
        var currency = item.TryGet<ProductPart>(out var product) ? product.Currency : null;
        var query = _httpContextAccessor.HttpContext?.Request.Query;

        model.FormId = "add-to-cart-" + item.ContentItemId;
        model.Title = item.DisplayText;
        model.Kind = part.Kind;
        model.Occupancies = [.. BookingCartLineHandler.GetOccupancies(part)];
        model.MinNights = Math.Max(1, part.MinNights);
        model.MaxNights = Math.Max(part.MinNights, part.MaxNights);
        model.MinDate = BookingSchedule.ToKey(today);
        model.MaxDate = BookingSchedule.ToKey(today.AddDays(Math.Max(1, part.DaysAhead)));
        model.SelectedDate = query?["date"].ToString();
        model.SelectedEnd = query?["end"].ToString();
        model.PricePerPerson = part.PricePerPerson;
        model.RequestToBook = part.RequestToBook;
        model.DepositPercent = part.DepositPercent;
        model.BalanceDueDaysBefore = part.BalanceDueDaysBefore;
        model.CancellationTiers = [.. part.CancellationTiers ?? []];

        var culture = CultureInfo.CurrentCulture;

        if (part.Kind == BookingKind.Departure)
        {
            var slots = await _slots.ListAsync(item.ContentItemId, _clock.UtcNow, _clock.UtcNow.AddDays(800));
            var departures = new List<BookingPickerOption>();

            foreach (var slot in slots.Where(slot => !slot.Closed && DateOnly.FromDateTime(BookingEmailSender.ToLocal(slot.StartsUtc, timeZone)) >= today))
            {
                // A date's own price is shown in the shopper's currency, as the product's is.
                var price = slot.Price is { } own
                    ? await _shopperCurrency.FormatForShopperAsync(own, currency)
                    : null;

                departures.Add(new BookingPickerOption(slot.SlotKey, BookingSchedule.GetLocalStart(slot.SlotKey)?.ToString("D", culture) ?? slot.SlotKey, slot.Remaining, price));
            }

            model.Departures = departures;
        }
        else if (part.Kind == BookingKind.TimeSlot)
        {
            // The next two weeks' times, less those taken.
            var keys = new List<string>();

            for (var day = today; day < today.AddDays(Math.Min(14, Math.Max(1, part.DaysAhead))); day = day.AddDays(1))
            {
                keys.AddRange(BookingSchedule.GetTimes(part, day).Where(time => time > localNow).Select(BookingSchedule.ToKey));
            }

            var taken = (await _slots.ListAsync(item.ContentItemId, keys)).ToDictionary(slot => slot.SlotKey, StringComparer.Ordinal);

            model.Days = [.. keys
                .Select(key => (Key: key, Start: BookingSchedule.GetLocalStart(key).Value, Remaining: taken.TryGetValue(key, out var slot) ? (slot.Closed ? 0 : slot.Remaining) : part.Capacity))
                .Where(entry => entry.Remaining > 0)
                .GroupBy(entry => DateOnly.FromDateTime(entry.Start))
                .Select(group => (group.Key.ToDateTime(TimeOnly.MinValue).ToString("D", culture), (IList<BookingPickerOption>)[.. group.Select(entry => new BookingPickerOption(entry.Key, entry.Start.ToString("t", culture), entry.Remaining, null))]))];
        }
    }

    private static bool TryParse(string value, out decimal amount, bool allowNegative)
    {
        amount = 0m;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out amount) && (allowNegative || amount >= 0m);
    }

    private static bool IsTime(string value)
        => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
